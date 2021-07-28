'use strict';
import  React, { Component } from 'react';
import { View, Image, NativeModules, Text, Dimensions, StyleSheet, ProgressBarAndroid } from 'react-native';
import PropTypes from 'prop-types';
import ResourceLoader from '../ResourceLoader';
import FadableView from './FadableView';
import { Debug } from '../ToolBox';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;
const updateTimeout = 500; // 500ms updare frequency.
const autoHideTimeout = 7000; // 7s auto hide timeout.
const invalidTimeoutId = -1;

const settingsIconPath = ResourceLoader.playbackIcons.set;
const revIconPath = ResourceLoader.playbackIcons.rew;
const ffwIconPath = ResourceLoader.playbackIcons.ffw;
const playIconPath = ResourceLoader.playbackIcons.play;
const pauseIconPath = ResourceLoader.playbackIcons.pause;

export default class PlaybackInfo extends Component 
{
  static defaultProps =
  {
    autoHide: true,
    onHide: null,
    selectedIndex: 0,
  };

  constructor(props) 
  {
    super(props);
    this.state = {
      position: '00:00:00',
      duration: '00:00:00',
      progress: 0,
      isPlaying: false,
      hide: false,
    };

    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.hideTimeoutId = invalidTimeoutId;
    this.updateTimeoutId = invalidTimeoutId;

    this.onUpdatePlaybackInfo = this.onUpdatePlaybackInfo.bind(this);
    this.onAutoHide = this.onAutoHide.bind(this);
    this.autoHideStartStop = this.autoHideStartStop.bind(this);    
  }

  componentDidMount()
  {
    console.debug('PlaybackInfo.componentDidMount():');
  
    setImmediate(()=>
    {
      this.onUpdatePlaybackInfo();
      this.autoHideStartStop();
    });

    console.debug('PlaybackInfo.componentDidMount(): done');
  }

  componentWillUnmount() 
  {
    console.debug('PlaybackInfo.componentWillUnmount():');
    
    if(this.updateTimeoutId != invalidTimeoutId)
    {
      clearTimeout(this.updateTimeoutId);
      this.updateTimeoutId = invalidTimeoutId;
    }

    if( this.hideTimeoutId != invalidTimeoutId)
    {
      clearTimeout(this.hideTimeoutId);
      this.hideTimeoutId = invalidTimeoutId;
    }
    console.debug('PlaybackInfo.componentWillUnmount(): done');
  }

  shouldComponentUpdate(nextProps, nextState)
  {
    const updateRequired = (  nextState.position != this.state.position || 
              nextState.duration != this.state.duration ||
              nextState.progress != this.state.progress || 
              nextState.isPlaying != this.state.isPlaying ||
              nextState.hide != this.state.hide ||
              nextProps.autoHide != this.props.autoHide );

    return updateRequired;
  }

  componentDidUpdate(prevProps, prevState) 
  {
    if(prevProps.autoHide != this.props.autoHide)
      this.autoHideStartStop();
  }
  
  autoHideStartStop()
  {
    console.debug(`PlaybackInfo.autoHideStartStop():`);

    if(this.hideTimeoutId != invalidTimeoutId)
    {
      clearTimeout(this.hideTimeoutId);
      this.hideTimeoutId = invalidTimeoutId;
    }

    if(this.props.autoHide)
    {
      this.hideTimeoutId = setTimeout(this.onAutoHide,autoHideTimeout);
      console.log(`PlaybackInfo.autoHideStartStop(): done. hide timeout ${autoHideTimeout/1000}s`);
    }
    else
    {
      console.log(`PlaybackInfo.autoHideStartStop(): done. hide timeout cleared.`);
    } 
  }

  async onUpdatePlaybackInfo()
  {
    try
    {
      this.updateTimeoutId = invalidTimeoutId;
      const infoBundle = await this.JuvoPlayer.GetPlaybackInfo();
      
      this.setState(
        {
          position: infoBundle.position, 
          duration: infoBundle.duration,
          progress: infoBundle.progress,
          isPlaying: infoBundle.isPlaying,
        });
      
      this.updateTimeoutId = setTimeout(this.onUpdatePlaybackInfo,updateTimeout);
    }
    catch(error)
    {
      console.warn(`PlaybackInfo.onUpdatePlaybackInfo(): ${error}`);
    }
  }

  onAutoHide()
  {
    console.debug(`PlaybackInfo.onAutoHide():`);
    this.hideTimeoutId = invalidTimeoutId;

    if(this.updateTimeoutId != invalidTimeoutId)
    {
      clearTimeout(this.updateTimeoutId);
      this.updateTimeoutId = invalidTimeoutId;
    }

    if( !this.props.onFadeOut )
      console.warn(`PlaybackInfo.onAutoHide(): onFadeOut not provided. Component will just 'hide'. not a happy scenario...`);
    
    this.setState({hide: true});
    console.debug(`PlaybackInfo.onAutoHide(): done`);
  }

  render()
  {
    try
    {
      console.debug('PlaybackInfo.render():');
    
      const isPlaying = this.state.isPlaying;
      
      const index = this.props.selectedIndex;
      const title = ResourceLoader.clipsData[index].title;

      const playbackPos = this.state.position;
      const playbackDur = this.state.duration;
      const progress = this.state.progress / 100;

      const hide = this.state.hide;

      console.debug(`PlaybackInfo.render(): done. autoHide '${this.props.autoHide}' hide '${hide}' playing '${this.state.isPlaying}' progress '${progress}' Position '${playbackPos}'->'${playbackDur}'`);
      return (
        <FadableView style={styles.playbackInfo} duration={300} fadeAway={hide} onFadeOut={this.props.onFadeOut} removeOnHide={false} > 
       
          <View style={styles.contentDescriptionBox} >
            <Image resizeMode='contain' style={styles.settingsIcon} source={settingsIconPath} />
            <Text style={styles.contentTitle} >{title}</Text>
          </View>
            
          <View style={styles.playbackProgressBox} >
            <ProgressBarAndroid style={styles.progressBar} value={progress} horizontal={true} color='green' />
            
            <Text style={styles.playbackPosition}>{playbackPos}</Text>
            <Text style={styles.playbackDuration}>{playbackDur}</Text>
       
            <Image resizeMode='contain' style={styles.rewindControl} source={revIconPath} />
            <Image resizeMode='contain' style={styles.playControl} source={isPlaying ? playIconPath : pauseIconPath} />
            <Image resizeMode='contain' style={styles.forwardControl} source={ffwIconPath} />
          </View>
        </FadableView>
      );
    }
    catch(error)
    {
      console.error(`PlaybackInfo.render(): ERROR '${error.toString()}'`);
    }
  }
}
const styles = StyleSheet.create({
  playbackInfo: {
    position: 'absolute',
    backgroundColor: 'transparent',
    height: height,
    width: width,
  },
  contentDescriptionBox: {
    width: width,
    height: '18%',
    position: 'absolute',
    backgroundColor: 'rgba(0,0,0,0.8)',
  },
  playbackProgressBox: {
    top: '82%',
    width: width,
    height: '18%',
    position: 'absolute',
    backgroundColor: 'rgba(0,0,0,0.8)',
  },
  settingsIcon: {
    left: '90%',
    top: '30%',
    width: '10%',
    height: '40%',
    position: 'absolute',
  },
  contentTitle: {
    width: '100%',
    height: '100%',
    fontSize: 60,
    color: 'white',
    textAlign: 'center',
    textAlignVertical: 'center',
  },
  progressBar: {
    left: '5%',
    width: '90%',
    height: '5%',
  },
  playbackPosition: {
    top: '12%',
    left: '2.5%',
    position: 'absolute',
    fontSize: 30,
    color: 'white',
    textAlign: 'center',
    textAlignVertical: 'center',
  },
  playbackDuration: {
    top: '12%',
    right: '2.5%',
    position: 'absolute',
    fontSize: 30,
    color: 'white',
    textAlign: 'center',
    textAlignVertical: 'center',
  },
  rewindControl: {
    top: '35%',
    left: '2.5%',
    position: 'absolute',
    width: 100,
    height: 100,
  },
  playControl: {
    top: '35%',
    left: '47.5%',
    position: 'absolute',
    width: 100,
    height: 100,
  },
  forwardControl: {
    top: '35%',
    right: '2.5%',
    position: 'absolute',
    width: 100,
    height: 100,
  }
});