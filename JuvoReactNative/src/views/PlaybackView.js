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
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    
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
    this.removePlaybackInfo = this.removePlaybackInfo.bind(this);
    this.displayPlaybackInfo = this.displayPlaybackInfo.bind(this);
  }

  componentDidMount()
  {
    console.debug(`PlaybackView.componentDidMount():`);

    // NativeEventEmitter events do not seem to be synchronised with JS event loop.
    // Run handlers via setImmediate() to get them more syncish.
    this.JuvoEventEmitter.addListener('onUpdateBufferingProgress', (p) => setImmediate( this.onUpdateBufferingProgress, p ) );
    this.JuvoEventEmitter.addListener('onSeekCompleted', () => setImmediate( this.onSeekCompleted ) );
    this.JuvoEventEmitter.addListener('onPlaybackError', (e) => setImmediate( this.onPlaybackError, e ) );
    this.JuvoEventEmitter.addListener('onEndOfStream', (eos) => setImmediate( this.onEndOfStream, eos ) );

    // Unchecked if DeviceEventEmitter events follow NativeEventEmitter behaviour model.
    DeviceEventEmitter.addListener('PlaybackView/onTVKeyDown', this.onTVKeyDown);

    // Start playback outside of component lifecycle.
    setImmediate( async ()=> await this.startPlayback() );

    console.debug(`PlaybackView.componentDidMount(): done`);
  }

  async componentWillUnmount() 
  {
    console.debug(`PlaybackView.componentWillUnmount():`);

    DeviceEventEmitter.removeAllListeners('PlaybackView/onTVKeyDown');
    this.JuvoEventEmitter.removeAllListeners('onUpdateBufferingProgress');
    this.JuvoEventEmitter.removeAllListeners('onSeekCompleted');
    this.JuvoEventEmitter.removeAllListeners('onPlaybackError');
    this.JuvoEventEmitter.removeAllListeners('onEndOfStream');
    delete this.JuvoEventEmitter;

    await this.JuvoPlayer.StopPlayback();
    
    console.debug(`PlaybackView.componentWillUnmount(): done`);
  }

  async startPlayback()
  {
    try
    {
      console.log(`PlaybackView.startPlayback():`);

      const video = ResourceLoader.clipsData[this.props.selectedIndex];
      const DRM = video.drmDatas ? JSON.stringify(video.drmDatas) : null;
      await this.JuvoPlayer.StartPlayback(video.url, DRM, video.type);

      this.displayPlaybackInfo(true);
      RenderScene.setScene(RenderView.viewCurrent, RenderView.viewNone);

      console.log(`PlaybackView.startPlayback(): done`);
    }
    catch(error)
    {
      console.error(`PlaybackView.startPlayback(): ERROR '${error}'`);
    }
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
      console.log(`PlaybackView.handleSeek(): Seek in progress`);
    }
  }

  onSeekCompleted() 
  {
    console.debug(`PlaybackView.onSeekCompleted():`);

    // enable auto hide on playback info.
    this.displayPlaybackInfo(true);

    // Hide InProgress only.
    const current = RenderScene.getScene();
    if(current.modalView.name == RenderView.viewInProgress.name)
      RenderScene.setScene(RenderView.viewCurrent, RenderView.viewNone);

    console.log(`PlaybackView.onSeekCompleted(): done. Seek completed`);
  }

  handleForwardKey() 
  {
    console.debug(`PlaybackView.handleForwardKey():`);
    this.handleSeek();
    this.JuvoPlayer.Forward();
    console.debug(`PlaybackView.handleForwardKey(): done`);
  }

   handleRewindKey() 
  {
    console.debug(`PlaybackView.handleRewindKey():`);
    this.handleSeek();
    this.JuvoPlayer.Rewind();
    console.debug(`PlaybackView.handleRewindKey(): done`);
  }

  removePlaybackInfo()
  {
    console.debug(`PlaybackView.removePlaybackInfo():`);
    this.setState({playbackInfo: null});
    console.log(`PlaybackView.removePlaybackInfo(): done`);
  }

  displayPlaybackInfo(autoHide)
  {
    console.debug(`PlaybackView.displayPlaybackInfo():`);

    const infoView = ShowPlaybackInfo({
      selectedIndex: this.props.selectedIndex,
      autoHide: autoHide,
      onFadeOut: this.removePlaybackInfo });

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
    console.error(`PlaybackView.onPlaybackError(): '${error.Message}'`);
    
    const currentScene = RenderScene.getScene();

    // show first error message only.
    if(currentScene.modalView.name != RenderView.viewPopup.name)
    {
      const catalogView = RenderView.viewContentCatalog;
      catalogView.args = {selectedIndex: this.props.selectedIndex};

      const errorView = RenderView.viewPopup;
      errorView.args = { messageText: error.Message }
      RenderScene.setScene(catalogView, errorView);
    }

    console.error(`PlaybackView.onPlaybackError(): done'`);
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

      case 'Return':
        break;

      case 'XF86AudioPlay':
      case 'XF86PlayBack':
        this.pauseResume();
        break;

      case 'XF86Back':
      case 'XF86AudioStop':
        const catalogView = RenderView.viewContentCatalog;
        catalogView.args = {selectedIndex: this.props.selectedIndex}
        RenderScene.setScene(catalogView,RenderView.viewNone);
        break;

      case 'Up':
        if( this.state.playbackInfo == null)
        {
          // playback info with auto hide.
          this.displayPlaybackInfo(true);
        }
        else
        {
          // playback info with no auto hide + stream selection
          this.displayPlaybackInfo(false);
          const streamSelection = RenderView.viewStreamSelection;
          const onHideStreamSelection = ()=>this.displayPlaybackInfo(true);
          streamSelection.args = {onFadeOut: onHideStreamSelection};
          RenderScene.setScene(RenderView.viewCurrent,RenderView.viewStreamSelection);
        }
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
      console.debug(`PlaybackView.render(): done. PlaybackInfoView '${playbackInfo != null}'`);

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
