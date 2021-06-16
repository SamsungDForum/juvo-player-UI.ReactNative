'use strict';
import React from 'react';
import { View, Image, NativeModules, NativeEventEmitter, Text, Dimensions, StyleSheet, DeviceEventEmitter } from 'react-native';

import ResourceLoader from '../ResourceLoader';
import ContentDescription from './ContentDescription';
import HideableView from './HideableView';
import PlaybackProgressBar from './PlaybackProgressBar';
import InProgressView from './InProgressView';
import PlaybackSettingsView from './PlaybackSettingsView';
import Native from '../Native';
import NotificationPopup from './NotificationPopup';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

export default class PlaybackView extends React.Component {
  constructor(props) {
    super(props);
    this.playbackTimeCurrent = 0;
    this.playbackTimeTotal = 0;
    this.state = {
      selectedIndex: this.props.selectedIndex
    };
    
    this.keysListenningOff = false;

    this.playerState = 'Idle';
    this.operationInProgress = false;
    this.inProgressDescription = 'Please wait...';
    this.infoRedrawCallbackID = -1;
    this.subtitleTextRedrawCallbackID = -1;
    this.infoHideCallbackID = -1;
    this.showSettingsView = false;
    this.showNotificationPopup = false;
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.streamsData = {
      Unknown: [],
      Video: [],
      Audio: [],
      Subtitle: [],
      Count: 4,
      selectedIndex: -1
    };
    this.currentSubtitleText = '';
    this.popupMessage = '';
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.redraw = this.redraw.bind(this);
    this.toggleView = this.toggleView.bind(this);
    this.onUpdateBufferingProgress = this.onUpdateBufferingProgress.bind(this);
    this.onUpdatePlayTime = this.onUpdatePlayTime.bind(this);
    this.resetPlaybackTime = this.resetPlaybackTime.bind(this);
    this.onSeekCompleted = this.onSeekCompleted.bind(this);
    this.onPlaybackError = this.onPlaybackError.bind(this);
    this.handleFastForwardKey = this.handleFastForwardKey.bind(this);
    this.handleRewindKey = this.handleRewindKey.bind(this);
    this.getFormattedTime = this.getFormattedTime.bind(this);
    this.handleInfoHidden = this.handleInfoHidden.bind(this);
    this.requestInfoShow = this.requestInfoShow.bind(this);
    this.clearInfoRedrawCallabckID = this.clearInfoRedrawCallabckID.bind(this);
    this.scheduleTheHideAndRedrawCallbacks = this.scheduleTheHideAndRedrawCallbacks.bind(this);
    this.setIntervalImmediately = this.setIntervalImmediately.bind(this);
    this.handleSeek = this.handleSeek.bind(this);
    this.handleSettingsViewDisappeared = this.handleSettingsViewDisappeared.bind(this);
    this.onGotStreamsDescription = this.onGotStreamsDescription.bind(this);
    this.onSubtitleSelection = this.onSubtitleSelection.bind(this);
    this.handleNotificationPopupDisappeared = this.handleNotificationPopupDisappeared.bind(this);
    this.requestInfoHide = this.requestInfoHide.bind(this);
    this.resetPlaybackStatus = this.resetPlaybackStatus.bind(this);
    this.infoHideWasRequested = this.infoHideWasRequested.bind(this);
    this.clearInfoHideCallabckID = this.clearInfoHideCallabckID.bind(this);
    this.clearSubtitleTextCallbackID = this.clearSubtitleTextCallbackID.bind(this);

    this.getStreamsDescription = this.getStreamsDescription.bind(this);
    this.pauseResume = this.pauseResume.bind(this);
    this.onEndOfStream = this.onEndOfStream.bind(this);
    this.startPlayback = this.startPlayback.bind(this);
    this.updatePlayerState = this.updatePlayerState.bind(this);
  }

  componentWillMount() {
    DeviceEventEmitter.addListener('PlaybackView/onTVKeyDown', this.onTVKeyDown);

    this.JuvoEventEmitter.addListener('onUpdateBufferingProgress', this.onUpdateBufferingProgress);
    this.JuvoEventEmitter.addListener('onUpdatePlayTime', this.onUpdatePlayTime);
    this.JuvoEventEmitter.addListener('onSeekCompleted', this.onSeekCompleted);
    this.JuvoEventEmitter.addListener('onPlaybackError', this.onPlaybackError);
    this.JuvoEventEmitter.addListener('onEndOfStream', this.onEndOfStream);
  }

  componentDidMount()
  {
    this.startPlayback();
  }

  componentWillUnmount() {
    DeviceEventEmitter.removeListener('PlaybackView/onTVKeyDown', this.onTVKeyDown);

    this.JuvoEventEmitter.removeListener('onUpdateBufferingProgress', this.onUpdateBufferingProgress);
    this.JuvoEventEmitter.removeListener('onUpdatePlayTime', this.onUpdatePlayTime);
    this.JuvoEventEmitter.removeListener('onSeekCompleted', this.onSeekCompleted);
    this.JuvoEventEmitter.removeListener('onPlaybackError', this.onPlaybackError);
    this.JuvoEventEmitter.removeListener('onEndOfStream', this.onEndOfStream);
    
    this.clearSubtitleTextCallbackID();
    this.resetPlaybackStatus();
  }

  getFormattedTime(milisecs) {
    var seconds = parseInt((milisecs / 1000) % 60);
    var minutes = parseInt((milisecs / (1000 * 60)) % 60);
    var hours = parseInt((milisecs / (1000 * 60 * 60)) % 24);
    return '%hours:%minutes:%seconds'
      .replace('%hours', hours.toString().padStart(2, '0'))
      .replace('%minutes', minutes.toString().padStart(2, '0'))
      .replace('%seconds', seconds.toString().padStart(2, '0'));
  }

  resetPlaybackStatus() {
    this.JuvoPlayer.Log('resetPlaybackStatus()');
    this.resetPlaybackTime();

    this.showSettingsView = false;
    this.handleInfoHidden();
    this.playerState = 'Idle';
    this.inProgressDescription = 'Please wait...';
    this.JuvoPlayer.StopPlayback();
  }
  infoHideWasRequested() {
    return (this.infoHideCallbackID == -1)
  }
  toggleView() {
    //Reseting the playback info screen to make it ready for starting a video playback again.
    this.resetPlaybackStatus();
    //Manage hide/show state between the content catalog and the playback View
    this.props.switchView('Previous');
  }
  handleSeek() {
    this.operationInProgress = true;
    this.inProgressDescription = 'Seeking...';
    this.requestInfoShow();
  }
  handleFastForwardKey() {
    this.handleSeek();
    this.JuvoPlayer.Forward();
  }
  handleRewindKey() {
    this.handleSeek();
    this.JuvoPlayer.Rewind();
  }
  handleInfoHidden() {
    if(!this.showSettingsView) {
      this.requestInfoHide();
      this.redraw();
    }
  }
  handleSettingsViewDisappeared() {
    this.showSettingsView = false;
    this.redraw();
  }
  handleNotificationPopupDisappeared() {
    this.showNotificationPopup = false;
    this.redraw();
    this.toggleView();
  }

  async startPlayback()
  {
    this.JuvoPlayer.Log('startPlayback()');
    this.updatePlayerState('Starting');
    
    const video = ResourceLoader.clipsData[this.state.selectedIndex];
    let DRM = video.drmDatas ? JSON.stringify(video.drmDatas) : null;

    // Starting playback
    try
    {
      let playerState = await this.JuvoPlayer.StartPlayback(video.url, DRM, video.type);
      this.updatePlayerState(playerState);
    }
    catch(error)
    {
      console.error(error);
      this.updatePlayerState('None');
    }
  }

  updatePlayerState(newState)
  {
    this.playerState = newState.toString();

    this.JuvoPlayer.Log('updatePlayerState( '+this.playerState+' )');

    switch(this.playerState)
    {
      case 'Starting':
        // Playback start in progress.
        this.operationInProgress = true;
        this.resetPlaybackTime();
        this.requestInfoShow();
        break;

      case 'None':
      case 'Idle':
      case 'Ready':
        // Transition to playing state did not complete. 
        // Switch back to content selection if there's no error popup.
        if(this.showNotificationPopup == false)
          this.toggleView();
        break;

      case 'Playing':
        // Playback succeeded.
        this.operationInProgress = false;
        this.requestInfoShow();
        break;

      case 'Paused':
        this.requestInfoShow();
        break;
    }
  }

  onUpdateBufferingProgress(buffering) {
    if (buffering.Percent == 100) {
      this.inProgressDescription = 'Please wait...';
      this.operationInProgress = false;
    } else {
      this.inProgressDescription = 'Buffering...';
      this.operationInProgress = true;
    }
    this.redraw();
  }

  onUpdatePlayTime(playtime) {
    this.playbackTimeCurrent = parseInt(playtime.Current);
    this.playbackTimeTotal = parseInt(playtime.Total);
    this.currentSubtitleText = playtime.SubtiteText;
  }

  onSeekCompleted() {
    this.operationInProgress = false;
    this.inProgressDescription = 'Please wait...';
    this.requestInfoHide();
    this.redraw();
  }

  onPlaybackError(error) {
    this.popupMessage = error.Message;
    this.showNotificationPopup = true;
    this.operationInProgress = false;
    this.showSettingsView = false;
    this.requestInfoHide();
    this.redraw();
  }

  onEndOfStream(_) {
    this.JuvoPlayer.Log('onEndOfStream()');
    this.updatePlayerState('None');
  }

  async getStreamsDescription() {
    var getStreams = !this.infoHideWasRequested();
    this.JuvoPlayer.Log('getStreamsDescription(): '+getStreams);
    //Show the settings view only if the playback controls are visible on the screen
    if (getStreams) {
      //requesting the native module for details regarding the stream settings.
      //The response is handled inside the onGotStreamsDescription() function.
      try
      {
        let streams = await Promise.all([
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Audio),
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Video),
          this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Subtitle)]);

        this.JuvoPlayer.Log('getStreamsDescriptionAsync(): got all streams');
        streams.forEach(stream => this.onGotStreamsDescription(stream));
      }
      catch(error)
      {
        console.error(error);
        this.updatePlayerState('None');
      }
    }
  }

  async pauseResume() {
    this.JuvoPlayer.Log('pauseResume( '+this.playerState+' )');

    if (this.playerState === 'Paused' || this.playerState === 'Playing') {
      //pause - resume
      try
      {
        let playerState = await this.JuvoPlayer.PauseResumePlayback();
        this.updatePlayerState(playerState);
      }
      catch(error)
      {
        console.error(error);
        this.updatePlayerState('None');
      }
    }
  }

  onTVKeyDown(pressed) {
    //There are two parameters available:
    //params.KeyName
    //params.KeyCode
    DeviceEventEmitter.emit('PlaybackSettingsView/onTVKeyDown',pressed);
    this.JuvoPlayer.Log('onTVKeyDown( '+pressed.KeyName+' ) will process: '+!this.keysListenningOff);
    if (this.keysListenningOff) return;

    switch (pressed.KeyName) {
      case 'Right':
        this.handleFastForwardKey();
        break;

      case 'Left':
        this.handleRewindKey();
        break;

      case 'Return':
      case 'XF86AudioPlay':
      case 'XF86PlayBack':
        this.pauseResume();
        break;

      case 'XF86Back':
      case 'XF86AudioStop':
        if (this.infoHideWasRequested()) {
          this.JuvoPlayer.Log('onTVKeyDown: '+pressed.KeyName+'. toggleView()');
          this.toggleView();
        } else {
          this.JuvoPlayer.Log('onTVKeyDown: '+pressed.KeyName+'. requestInfoHide()->redraw()');
          this.requestInfoHide();
        }
        break;

      case 'Up':
        this.getStreamsDescription();

        // fall through intentional
      default:
        this.requestInfoShow();
    }
  }

  onGotStreamsDescription(streams) {
    if(streams == null)
    {
      this.JuvoPlayer.Log('onGotStreamsDescription( no stream! )');
      return;
    }

    this.JuvoPlayer.Log('onGotStreamsDescription( '+streams.StreamTypeIndex+' )');
    var StreamType = Native.JuvoPlayer.Common.StreamType;
    switch (streams.StreamTypeIndex) {
      case StreamType.Audio:
        this.streamsData.Audio = JSON.parse(streams.Description);
        break;
      case StreamType.Video:
        this.streamsData.Video = JSON.parse(streams.Description);
        break;
      case StreamType.Subtitle:
        this.streamsData.Subtitle = JSON.parse(streams.Description);
        break;
    }
    this.showSettingsView = this.streamsData.Audio !== null && this.streamsData.Video !== null && this.streamsData.Subtitle !== null;
    if (this.showSettingsView) {
      this.redraw();
    }
  }
  onSubtitleSelection(Selected) {
    this.clearSubtitleTextCallbackID();
    if (Selected != 'off') {
      this.subtitleTextRedrawCallbackID = this.setIntervalImmediately(this.redraw, 100);
    } else {
      this.currentSubtitleText = '';
      this.redraw();
    }
  }
  clearSubtitleTextCallbackID() {
    if (this.subtitleTextRedrawCallbackID >= 0) {
      clearInterval(this.subtitleTextRedrawCallbackID);
      this.subtitleTextRedrawCallbackID = -1;
    }
  }
  resetPlaybackTime() {
    this.playbackTimeCurrent = 0;
    this.playbackTimeTotal = 0;
  }

  requestInfoShow() {
    this.clearInfoRedrawCallabckID();
    this.clearInfoHideCallabckID();
    this.scheduleTheHideAndRedrawCallbacks();
  }

  requestInfoHide() {
    this.clearInfoRedrawCallabckID()
    this.clearInfoHideCallabckID();
    this.showSettingsView = false;
    this.operationInProgress = false;
  }
  clearInfoRedrawCallabckID() {
    if (this.infoRedrawCallbackID >= 0) {
      clearInterval(this.infoRedrawCallbackID);
      this.infoRedrawCallbackID = -1;
    }
  }
  clearInfoHideCallabckID() {
    if (this.infoHideCallbackID >= 0) {
      clearTimeout(this.infoHideCallbackID);
      this.infoHideCallbackID = -1;
    }
  }
  scheduleTheHideAndRedrawCallbacks() {
    this.infoHideCallbackID = setTimeout(this.handleInfoHidden, 10000);
    this.infoRedrawCallbackID = this.setIntervalImmediately(this.redraw, 500);
  }
  setIntervalImmediately(func, interval) {
    func();
    return setInterval(func, interval);
  }
  redraw() {
    //Calling the this.setState() here makes the component to launch it's render() method,
    //which results in redraw all its contents (including the embedded components recursively).
    this.forceUpdate();
  }
  render() {
    const index = this.state.selectedIndex;
    this.streamsData.selectedIndex = index;
    const title = ResourceLoader.clipsData[index].title;
    const fadeduration = 300;
    const revIconPath = ResourceLoader.playbackIcons.rew;
    const ffwIconPath = ResourceLoader.playbackIcons.ffw;
    const settingsIconPath = ResourceLoader.playbackIcons.set;
    const playIconPath = this.playerState !== 'Playing' ? ResourceLoader.playbackIcons.play : ResourceLoader.playbackIcons.pause;
    const visibility = this.props.visibility ? this.props.visibility : true;

    this.keysListenningOff = this.showSettingsView || this.showNotificationPopup;
    const total = this.playbackTimeTotal;
    const current = this.playbackTimeCurrent;
    const playbackTime = total > 0 ? current / total : 0;
    const progress = Math.round(playbackTime * 100) / 100;
    const subtitleText = this.currentSubtitleText;
    var subtitlesStyle = subtitleText == '' ? [styles.subtitles, { opacity: 0 }] : [styles.subtitles, { opacity: 0.8 }];

    return (
      <View style={{ position: 'absolute', width: width, height: height }}>
        <HideableView position={'absolute'} visible={visibility} duration={fadeduration} height={height} width={width}>
          <View style={[styles.page, { justifyContent: 'flex-end' }]}>
            <View style={[subtitlesStyle, { paddingBottom: height / 4.8 }]}>
              <Text style={styles.textSubtitles}>{subtitleText}</Text>
            </View>
          </View>
          <HideableView position={'relative'} visible={!this.infoHideWasRequested()} duration={fadeduration} height={height} width={width}>
            <View style={[styles.page, { position: 'relative' }]}>
              <View style={[styles.transparentPage, { flex: 2, flexDirection: 'row' }]}>
                <View style={[styles.element, { flex: 1 }]} />
                <View style={[styles.element, { flex: 8 }]}>
                  <ContentDescription viewStyle={styles.element} headerStyle={styles.textHeader} bodyStyle={styles.textBody} headerText={title} bodyText={''} />
                </View>
                <View style={[styles.element, { flex: 1 }]}>
                  <Image resizeMode='cover' style={styles.icon} source={settingsIconPath} />
                </View>
              </View>
              <View style={[styles.element, { flex: 8, justifyContent: 'flex-end' }]} />
              <View style={[styles.transparentPage, { flex: 2 }]}>
                <View style={[styles.element, { flex: 2, justifyContent: 'flex-start' }]}>
                  <PlaybackProgressBar value={progress} color='green' />
                  <Text style={[styles.time, { alignSelf: 'flex-start', marginLeft: 50 }]}>{this.getFormattedTime(this.playbackTimeCurrent)}</Text>
                  <Text style={[styles.time, { alignSelf: 'flex-end', marginLeft: 50 }]}>{this.getFormattedTime(this.playbackTimeTotal)}</Text>
                </View>
                <View style={{ flex: 5, backgroundColor: 'transparent', flexDirection: 'row' }}>
                  <View style={[styles.element, { flex: 1 }]}>
                    <Image resizeMode='cover' style={styles.icon} source={revIconPath} />
                  </View>
                  <View style={[styles.element, { flex: 10 }]}>
                    <Image resizeMode='cover' style={styles.icon} source={playIconPath} />
                  </View>
                  <View style={[styles.element, { flex: 1 }]}>
                    <Image resizeMode='cover' style={styles.icon} source={ffwIconPath} />
                  </View>
                </View>
              </View>
            </View>
          </HideableView>
          <View style={[styles.page, styles.element]}>
            <PlaybackSettingsView
              visible={this.showSettingsView}
              onCloseSettingsView={this.handleSettingsViewDisappeared}
              onSubtitleSelection={this.onSubtitleSelection}
              streamsData={this.streamsData}
            />
          </View>
          <View style={[styles.page, styles.element]}>
            <InProgressView visible={this.operationInProgress} message={this.inProgressDescription} />
          </View>
          <View style={[styles.page, styles.element]}>
            <NotificationPopup visible={this.showNotificationPopup} onNotificationPopupDisappeared={this.handleNotificationPopupDisappeared} messageText={this.popupMessage} />
          </View>
        </HideableView>
      </View>
    );
  }
}

const styles = StyleSheet.create({
  page: {
    position: 'absolute',
    backgroundColor: 'transparent',
    height: height,
    width: width
  },
  element: {
    backgroundColor: 'transparent',
    alignItems: 'center',
    justifyContent: 'center'
  },
  icon: {
    width: 70,
    height: 70
  },
  time: {
    marginTop: 25,
    position: 'absolute',
    width: 150,
    height: 30,
    fontSize: 30,
    color: 'white'
  },
  transparentPage: {
    backgroundColor: 'black',
    opacity: 0.9
  },
  textHeader: {
    fontSize: 60,
    color: 'white',
    alignSelf: 'center'
  },
  textBody: {
    fontSize: 30,
    color: 'white'
  },
  textSubtitles: {
    fontSize: 30,
    color: 'white',
    textAlign: 'center',
    backgroundColor: 'black'
  },
  subtitles: {
    width: width,
    height: 150
  }
});
