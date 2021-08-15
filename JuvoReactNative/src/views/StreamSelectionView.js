'use strict';
import React from 'react';
import { View, Text, Picker, NativeModules, StyleSheet, DeviceEventEmitter,Dimensions, InteractionManager } from 'react-native';
import PropTypes from 'prop-types'

import Native from '../Native';
import {Debug} from '../ToolBox';
import RenderView from './RenderView';
import FadableView from './FadableView';
import RenderScene from './RenderScene';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

function StreamPicker(props)
{
  // enable picker if streams exist.
  const enabled = props.streams.length > 0;
  let selectedDescription = null;
  
  console.debug(JSON.stringify(props.selected));

  if(props.selected)
  {
    selectedDescription = props.selected.Description;
  }
  else
  {
    // selection not specified. Look for default selection set in native code.
    for(const stream of props.streams)
    {
      if(stream.Default)
      {
        selectedDescription = stream.Description;
        break;
      }
    }
  }
    
  const pickerProps =
  {
    ...props,
    enabled: enabled,
    title: selectedDescription,
  };

  return (
    <Picker {...pickerProps}>
      {props.streams.map( (item) => <Picker.Item label={item.Description} value={item} key={`${item.StreamType}.${item.Id}`} /> )}
    </Picker>
  );
}

function getStreams(player)
{
  console.debug('getStreams(): quering available streams');

  return Promise.all([
    player.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Audio),
    player.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Video),
    player.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Subtitle),
  ]);      
}

export default class StreamSelectionView extends React.Component
{
  static defaultProps =
  {
    fadeAway: false,
    onFadeOut: null,
    currentAudio: null,
    currentVideo: null,
    currentSubtitles: null,
  };

  constructor(props) {
    super(props);
    this.state = {
      Audio: [],
      Video: [],
      Subtitle: [],
    };

    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.pickerChange = this.pickerChange.bind(this);
    this.initializeStreamSelection = this.initializeStreamSelection.bind(this);
    
    // start getting streams
    this.streamsPromise = getStreams(this.JuvoPlayer);
  }

  componentDidMount() 
  {
    console.debug('StreamSelectionView.componentDidMount():');

    console.log(`  Current audio: '${JSON.stringify(this.props.currentAudio)}'`);
    console.log(`  Current video: '${JSON.stringify(this.props.currentVideo)}'`);
    console.log(`  Current subtitles: '${JSON.stringify(this.props.currentSubtitles)}'`);
    this.initializeStreamSelection();
    
    console.debug('StreamSelectionView.componentDidMount(): done');
  }

  componentWillUnmount()
  {
    console.debug('StreamSelectionView.componentWillUnmount():');
    
    DeviceEventEmitter.removeAllListeners('StreamSelectionView/onTVKeyDown');
    
    console.debug('StreamSelectionView.componentWillUnmount(): done');
  }

  async initializeStreamSelection()
  {
    try
    {
      console.debug('StreamSelectionView.initializeStreamSelection():');

      const streamsSource = await this.streamsPromise;
      const streams = {
        Audio: [],
        Video: [],
        Subtitle: [],
      };

      for(const stream of streamsSource)
      {
        if(!stream) continue;
        
        const streamLabel = stream.streamLabel;
        streams[streamLabel] = JSON.parse(stream[streamLabel]);  
      }
      
      this.setState(streams);

      // add key listener afer init. Prevents init of 'unmounted' object.
      DeviceEventEmitter.addListener('StreamSelectionView/onTVKeyDown', this.onTVKeyDown);

      console.debug('StreamSelectionView.initializeStreamSelection(): done');
    }
    catch(error)
    {
      console.error(`StreamSelectionView.initializeStreamSelection(): ERROR ${error}`);
      const errorView = RenderView.viewPopup;
      errorView.args = {messageText: 'Failed to obtain stream selection.'};
      RenderScene.setScene(RenderView.viewCurrent,errorView);
    }
  }

  onTVKeyDown(pressed)
  {
    console.debug(`StreamSelectionView.onTVKeyDown(): key ${pressed.KeyName}`);
    
    switch (pressed.KeyName)
    {
      case 'XF86Back':
        RenderScene.setScene(RenderView.viewCurrent,RenderView.viewNone);
        break;

      default:
        console.debug(`StreamSelectionView.onTVKeyDown(): key ${pressed.KeyName} ignored`);
        return;
    }

    console.debug(`StreamSelectionView.onTVKeyDown(): ${pressed.KeyName} processed.`);
  }

  async pickerChange(itemValue, itemPosition) 
  {
    try
    {
      console.log(`StreamSelectionView.pickerChange(): selecting stream ${itemValue.StreamType} [${itemValue.GroupIndex} ${itemValue.FormatIndex} ] ${itemValue.Description}`);
      
      await this.JuvoPlayer.SetStream(itemValue.GroupIndex, itemValue.FormatIndex);
      this.props.onStreamChanged(itemValue);

      console.log('StreamSelectionView.pickerChange(): done');
    }
    catch(error)
    {
      console.error(`StreamSelectionView.pickerChange(): ERROR ${error}`);
    }
  }

  render()
  {
    try
    {
      console.debug('StreamSelectionView.render():');

      const audioStreams = this.state.Audio;
      const videoStreams = this.state.Video;
      const subtitleStreams = this.state.Subtitle;
      
      console.log(`StreamSelectionView.render(): done. fadeAway ${this.props.fadeAway}`);

      return (
        <FadableView style={styles.streamSelection} duration={300} fadeAway={this.props.fadeAway} onFadeOut={this.props.onFadeOut} removeOnFadeAway={true} nameTag='StreamSelection'>
          <View style={styles.selectionBox}>
            <View style={[styles.textView, { flex: 1.5 }]}>
              <Text style={styles.textHeader}> Use arrow keys to navigate. Press enter key to select a setting. </Text>
            </View>
            <View style={{ flex: 2, alignItems: 'flex-start', flexDirection: 'row', backgroundColor: 'transparent' }}>
              <View style={{ flex: 1, alignItems: 'center' }}>                
                  <Text style={styles.textBody}>Audio track</Text>
                  <StreamPicker streams={audioStreams} selected={this.props.currentAudio} style={styles.picker} onValueChange={this.pickerChange} />
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                  <Text style={styles.textBody}>Video quality</Text>
                  <StreamPicker streams={videoStreams} selected={this.props.currentVideo} style={styles.picker} onValueChange={this.pickerChange} />
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                  <Text style={styles.textBody}>Subtitles</Text>
                  <StreamPicker streams={subtitleStreams} selected={this.props.currentSubtitles} style={styles.picker} onValueChange={this.pickerChange} />
              </View>
            </View>
            <View style={[styles.textView, { flex: 1 }]}>
              <Text style={styles.textFooter}> Press return key to close </Text>
            </View>
          </View>
        </FadableView>
      );
    }
    catch(error)
    {
      console.error(`StreamSelectionView.render(): ERROR ${error}`);
    }
  }
}

StreamSelectionView.propTypes = {
  fadeAway: PropTypes.bool,
  onFadeOut: PropTypes.func,
  onStreamChanged: PropTypes.func.isRequired,
  currentAudio: PropTypes.object,
  currentVideo: PropTypes.object,
  currentSubtitles: PropTypes.object,
};

const styles = StyleSheet.create({
  streamSelection: {
    width: width,
    height: height,
    justifyContent: 'center', 
    alignItems: 'center', 
    backgroundColor: 'rgba(35, 35, 35, 0.5)',
  },
  selectionBox:
  {
    width: 1600, 
    height: 350,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
  },
  picker: {
    height: 30,
    width: 450,
    color: '#ffffff'
  },
  textView: {
    justifyContent: 'center',
    backgroundColor: 'transparent',
    opacity: 1
  },
  textHeader: {
    fontSize: 30,
    color: 'white',
    alignSelf: 'center'
  },
  textFooter: {
    fontSize: 20,
    color: 'white',
    textAlign: 'center'
  },
  textBody: {
    fontSize: 28,
    color: 'white',
    fontWeight: 'bold'
  }
});