'use strict';
import React, {Component} from 'react';
import { View, NativeModules, NativeEventEmitter, Dimensions, StyleSheet, DeviceEventEmitter } from 'react-native';
import PropTypes from 'prop-types'

import ResourceLoader from '../ResourceLoader';
import Native from '../Native';
import PlaybackInfo from './PlaybackInfo';
import RenderScene from './RenderScene'; 
import RenderView from './RenderView';
import {Debug} from '../ToolBox';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

function ShowPlaybackInfo(props)
{
  return( <PlaybackInfo {...props} />);
}

export default class PlaybackView extends Component 
{
  constructor(props)
  {
    super(props);
    this.state = {playbackInfo: null};   
    
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = null;
    
    // Playback functionality
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.onUpdateBufferingProgress = this.onUpdateBufferingProgress.bind(this);
    this.onSeekCompleted = this.onSeekCompleted.bind(this);
    this.onPlaybackError = this.onPlaybackError.bind(this);
    this.handleForwardKey = this.handleForwardKey.bind(this);
    this.handleRewindKey = this.handleRewindKey.bind(this);
    this.handleSeek = this.handleSeek.bind(this);
    this.pauseResume = this.pauseResume.bind(this);
    this.onEndOfStream = this.onEndOfStream.bind(this);
    this.startPlayback = this.startPlayback.bind(this);

    // PlaybackInfo display
    this.removePlaybackInfo = this.removePlaybackInfo.bind(this);
    this.displayPlaybackInfo = this.displayPlaybackInfo.bind(this);

    // StreamSelectionView functionality
    this.selectedAudio = null;
    this.selectedVideo = null;
    this.selectedSubtitles = null;
    this.onStreamChanged = this.onStreamChanged.bind(this);
    this.onHideStreamSelectionView = this.onHideStreamSelectionView.bind(this);
  }

  componentDidMount()
  {
    console.debug('PlaybackView.componentDidMount():');
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    // NativeEventEmitter events do not seem to be synchronised with JS event loop.
    // Run handlers via setImmediate() to get them more syncish.
    this.JuvoEventEmitter.addListener('onUpdateBufferingProgress', (p) => setImmediate( this.onUpdateBufferingProgress, p ) );
    this.JuvoEventEmitter.addListener('onSeekCompleted', () => setImmediate( this.onSeekCompleted ) );
    this.JuvoEventEmitter.addListener('onPlaybackError', (e) => setImmediate( this.onPlaybackError, e.Message ) );
    this.JuvoEventEmitter.addListener('onEndOfStream', (eos) => setImmediate( this.onEndOfStream, eos ) );

    // Unchecked if DeviceEventEmitter events follow NativeEventEmitter behaviour model.
    DeviceEventEmitter.addListener('PlaybackView/onTVKeyDown', this.onTVKeyDown);

    // Start playback outside of component lifecycle.
    setImmediate( async ()=> await this.startPlayback());
    
    console.debug('PlaybackView.componentDidMount(): done');
  }

  async componentWillUnmount() 
  {
    console.debug('PlaybackView.componentWillUnmount():');

    DeviceEventEmitter.removeAllListeners('PlaybackView/onTVKeyDown');
    this.JuvoEventEmitter.removeAllListeners('onUpdateBufferingProgress');
    this.JuvoEventEmitter.removeAllListeners('onSeekCompleted');
    this.JuvoEventEmitter.removeAllListeners('onPlaybackError');
    this.JuvoEventEmitter.removeAllListeners('onEndOfStream');
    delete this.JuvoEventEmitter;

    console.debug('PlaybackView.componentWillUnmount(): Stopping playback');
    await this.JuvoPlayer.StopPlayback();
    
    console.debug('PlaybackView.componentWillUnmount(): done');
  }

  async startPlayback()
  {
    console.log('PlaybackView.startPlayback():');

    try
    {
      const video = ResourceLoader.clipsData[this.props.selectedIndex];
      const DRM = video.drmDatas ? JSON.stringify(video.drmDatas) : null;
      
      await this.JuvoPlayer.StartPlayback(video.url, DRM, video.type);

      this.displayPlaybackInfo(true);
      RenderScene.setScene(RenderView.viewCurrent, RenderView.viewNone);      
    }
    catch(error)
    {
      console.error(`PlaybackView.startPlayback(): '${error}'`);
      this.onPlaybackError(error.message);
    }

    console.log('PlaybackView.startPlayback(): done');
  }

  handleSeek() 
  {
    // show playback info in non auto hide mode.
    this.displayPlaybackInfo(false);
    
    const current = RenderScene.getScene();
    if(current.modalView.name == RenderView.viewNone.name)
    {
      const seekingView = RenderView.viewInProgress;
      seekingView.args = {messageText: 'Seeking'};

      RenderScene.setScene(RenderView.viewCurrent,seekingView);
      console.log('PlaybackView.handleSeek(): Seek in progress');
    }
  }

  onSeekCompleted() 
  {
    console.debug('PlaybackView.onSeekCompleted():');

    // enable auto hide on playback info.
    this.displayPlaybackInfo(true);

    // Hide InProgress only.
    const current = RenderScene.getScene();
    if(current.modalView.name == RenderView.viewInProgress.name)
      RenderScene.setScene(RenderView.viewCurrent, RenderView.viewNone);

    console.log('PlaybackView.onSeekCompleted(): done. Seek completed');
  }

  handleForwardKey() 
  {
    console.debug('PlaybackView.handleForwardKey():');
    this.handleSeek();
    this.JuvoPlayer.Forward();
    console.debug('PlaybackView.handleForwardKey(): done');
  }

   handleRewindKey() 
  {
    console.debug('PlaybackView.handleRewindKey():');
    this.handleSeek();
    this.JuvoPlayer.Rewind();
    console.debug('PlaybackView.handleRewindKey(): done');
  }

  removePlaybackInfo()
  {
    console.debug('PlaybackView.removePlaybackInfo():');
    this.setState({playbackInfo: null});
    console.log('PlaybackView.removePlaybackInfo(): done');
  }

  displayPlaybackInfo(autoHide)
  {
    console.debug('PlaybackView.displayPlaybackInfo():');

    const infoView = ShowPlaybackInfo({
      selectedIndex: this.props.selectedIndex,
      autoHide: autoHide,
      onFadeOut: this.removePlaybackInfo,
    });

    this.setState({playbackInfo: infoView});
    console.log(`PlaybackView.displayPlaybackInfo(): done. autoHide '${autoHide}'`);
  }

  onUpdateBufferingProgress(buffering) 
  {
    console.debug(`PlaybackView.onUpdateBufferingProgress(): '${buffering.Percent}'`);
    const current = RenderScene.getScene();

    // Show progress if there's no other modal... like error popup.
    if(buffering.Percent == 100)
    {
      if(current.modalView.name == RenderView.viewInProgress.name)
        RenderScene.setScene(RenderView.viewCurrent,RenderView.viewNone);
    }
    else
    {
      if(current.modalView.name == RenderView.viewNone.name)
      {
        const bufferingView = RenderView.viewInProgress;
        bufferingView.args = {messageText: 'Buffering'};
        RenderScene.setScene(RenderView.viewCurrent,RenderView.viewNone);
      }
    }

    console.log(`PlaybackView.onUpdateBufferingProgress(): '${buffering.Percent}' done`);
  }

  onPlaybackError(error) 
  {
    console.error(`PlaybackView.onPlaybackError(): '${error}'`);
    
    if(this.state.playbackInfo)
      this.removePlaybackInfo();

    const catalogView = RenderView.viewContentCatalog;
    catalogView.args = {selectedIndex: this.props.selectedIndex};

    const errorView = RenderView.viewPopup;
    errorView.args = { messageText: error }
    RenderScene.setScene(catalogView, errorView);
    
    console.error('PlaybackView.onPlaybackError(): done');
  }

  onEndOfStream(_) 
  {
    console.debug('PlaybackView.onEndOfStream():');
    
    // About to close PlaybackView. Hide PlaybackInfo first, if visible.
    // PlaybackInfo has PlaybackView.removePlaybackInfo() attached to its tentacles.
    if(this.state.playbackInfo)
      this.removePlaybackInfo();

    const catalogView = RenderView.viewContentCatalog;
    catalogView.args = {selectedIndex: this.props.selectedIndex};
    
    setImmediate(()=>RenderScene.setScene(catalogView, RenderView.viewNone));
    console.log('PlaybackView.onEndOfStream(): done');
  }

  async pauseResume() 
  {
    try
    {
      console.debug('PlaybackView.pauseResume():');

      const playbackState = await this.JuvoPlayer.PauseResumePlayback();
      const autoHideInfo = playbackState != Native.JuvoPlayer.PlaybackState.Paused;
      this.displayPlaybackInfo(autoHideInfo);
     
      console.log(`PlaybackView.pauseResume(): done. playback state '${playbackState}'`);
    }
    catch(error)
    {
      console.error(`PlaybackView.pauseResume(): ERROR '${error}'`);
    }
  }

  onStreamChanged(stream)
  {
    console.debug(`PlaybackView.onStreamChanged(): ${Debug.stringify(stream)}`);

    switch(stream.StreamType)
    {
      case Native.JuvoPlayer.Common.StreamType.Audio:
        this.selectedAudio = stream;
        break;

      case Native.JuvoPlayer.Common.StreamType.Video:
        this.selectedVideo = stream;
        break;

      case Native.JuvoPlayer.Common.StreamType.Subtitle:
        this.selectedSubtitles = stream;
        break;

      default:
        console.warn(`PlaybackView.onStreamChanged(): stream '${stream.StreamType}' unsupported.`);
        return;
    }

    console.debug('PlaybackView.onStreamChanged(): done');
  }

  onHideStreamSelectionView()
  {
    console.debug('PlaybackView.onHideStreamSelectionView():');
    const currentScene = RenderScene.getScene();
    if(currentScene.mainView.name != RenderView.viewPlayback.name)
      console.log('PlaybackView.onHideStreamSelectionView(): playback view removed. Nothing to hide');
    else
      this.displayPlaybackInfo(true);
      
    console.debug('PlaybackView.onHideStreamSelectionView(): done');
  }

  onTVKeyDown(pressed) 
  {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode

    console.debug(`PlaybackView.onTVKeyDown(): ${pressed.KeyName}`);

    switch (pressed.KeyName) {
      case 'Right':
        this.handleForwardKey();
        break;

      case 'Left':
        this.handleRewindKey();
        break;

      case 'XF86AudioPlay':
      case 'XF86PlayBack':
      case 'Return':
        this.pauseResume();
        break;

      case 'XF86Back':
      case 'XF86AudioStop':
        const catalogView = RenderView.viewContentCatalog;
        catalogView.args = {selectedIndex: this.props.selectedIndex}
        RenderScene.setScene(catalogView, RenderView.viewNone);
        break;

      case 'Up':
        if( this.state.playbackInfo == null)
        {
          // No playback info. Show playback info with auto hide.
          this.displayPlaybackInfo(true);
          break;
        }
        
        // playback info visible. Prevent auto hide prior to displaying stream selection.
        this.displayPlaybackInfo(false);
      
        // display stream selection
        const streamSelection = RenderView.viewStreamSelection;
        streamSelection.args = 
        {
          onFadeOut: this.onHideStreamSelectionView,
          onStreamChanged: this.onStreamChanged,
          currentAudio: this.selectedAudio,
          currentVideo: this.selectedVideo,
          currentSubtitles: this.selectedSubtitles,
        };

        RenderScene.setScene(RenderView.viewCurrent,streamSelection);
        break;

      default:
        console.debug(`PlaybackView.onTVKeyDown(): done. key '${pressed.KeyName}' ignored`);
        return;
    }

    console.debug(`PlaybackView.onTVKeyDown(): done. key '${pressed.KeyName}' processed`);
  }

  render() 
  {
    try
    {
      const playbackInfo = this.state.playbackInfo;
      console.log(`PlaybackView.render(): done. PlaybackInfoView '${playbackInfo != null}'`);

      return(  <View style={styles.page}>{playbackInfo && playbackInfo}</View> );
    }
    catch(error)
    {
      console.error(`PlaybackView.render(): ERROR ${error}`);
      return null;
    }
  }
}

PlaybackView.propTypes = {
  selectedIndex: PropTypes.number.isRequired,
};

const styles = StyleSheet.create({
  page: {
    position: 'absolute',
    backgroundColor: 'transparent',
    height: height,
    width: width,
  },
});