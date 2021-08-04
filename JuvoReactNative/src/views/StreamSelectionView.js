'use strict';
import React from 'react';
import { View, Text, Picker, NativeModules, StyleSheet, DeviceEventEmitter,Dimensions } from 'react-native';
import PropTypes from 'prop-types'

import Native from '../Native';
import {Debug} from '../ToolBox';
import RenderView from './RenderView';
import FadableView from './FadableView';
import RenderScene from './RenderScene';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

function getDefaultStreamDescription(streams) {
  const defaultStream = getDefaultStream(streams);
  if (defaultStream !== undefined) return defaultStream.Description;
  return '';
}

function getDefaultStream(streams) {
  for (const stream of streams) {
    if (stream.Default === true) {
      return stream;
    }
  }
}

function StreamPicker(props)
{
  const pickerProps =
  {
    ...props,
    title: getDefaultStreamDescription(props.streams),
  };

  return (
    <Picker {...pickerProps}>
      {props.streams.map( (item) => <Picker.Item label={item.Description} value={item} key={`${item.StreamType}.${item.Id}`} /> )}
    </Picker>
  );
}

export default class StreamSelectionView extends React.Component {
  static defaultProps =
  {
    fadeAway: false,
    onFadeOut: null,
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
    this.readStreamData = this.readStreamData.bind(this);
    
    this.streamsPromise = (()=> 
    {
      console.debug('StreamSelectionView.constructor(): quering available streams');

      return Promise.all([
        this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Audio),
        this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Video),
        this.JuvoPlayer.GetStreamsDescription(Native.JuvoPlayer.Common.StreamType.Subtitle),
      ]);      
    })();
  }

  componentDidMount() 
  {
    console.debug('StreamSelectionView.componentDidMount():');

    DeviceEventEmitter.addListener('StreamSelectionView/onTVKeyDown', this.onTVKeyDown);
    this.readStreamData();

    console.debug('StreamSelectionView.componentDidMount(): done');
  }

  componentWillUnmount()
  {
    console.debug('StreamSelectionView.componentWillUnmount():');
    
    DeviceEventEmitter.removeAllListeners('StreamSelectionView/onTVKeyDown');
    
    console.debug('StreamSelectionView.componentWillUnmount(): done');
  }

  async readStreamData()
  {
    try
    {
      console.debug('StreamSelectionView.readStreamData():');
      const dataSource = await this.streamsPromise;
      const streams = {
        Audio: [],
        Video: [],
        Subtitle: [],
      };

      dataSource.forEach( el =>
      {
        if(el != null)
        {
          const streamLabel = el.streamLabel;
          streams[streamLabel] = JSON.parse(el[streamLabel]);
        }
       });
      
      this.setState(streams);
      console.debug('StreamSelectionView.readStreamData(): done');
    }
    catch(error)
    {
      console.error(`StreamSelectionView.readStreamData(): ERROR ${error}`);
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

    console.debug(`StreamSelectionView.onTVKeyDown(): ${pressed.KeyName} processed. XF86Back count ${this.XF86BackPressCount}`);
  }

  async pickerChange(itemValue, itemPosition) 
  {
    //Apply the playback setttings to the playback
    try
    {
      console.log(`StreamSelectionView.pickerChange(): selecting stream ${itemValue.Id} ${itemValue.Description}`);
      await this.JuvoPlayer.SetStream(itemValue.Id, itemValue.StreamType);
      console.log('StreamSelectionView.pickerChange(): done');
    }
    catch(error)
    {
      console.error('StreamSelectionView.pickerChange(): failed to select strean');
    }
  }

  render() {
    try
    {
      console.debug('StreamSelectionView.render():');

      const audioStreams = this.state.Audio;
      const videoStreams = this.state.Video;
      const subtitleStreams = this.state.Subtitle;
      
      const hideView = this.props.fadeAway;
      console.debug(`StreamSelectionView.render(): done. fadeAway ${hideView}`);

      return (
        <FadableView style={styles.streamSelection} duration={300} fadeAway={hideView} onFadeOut={this.props.onFadeOut} removeOnFadeAway={true} nameTag='StreamSelection'>
          <View style={styles.selectionBox}>
            <View style={[styles.textView, { flex: 1.5 }]}>
              <Text style={styles.textHeader}> Use arrow keys to navigate. Press enter key to select a setting. </Text>
            </View>
            <View style={{ flex: 2, alignItems: 'flex-start', flexDirection: 'row', backgroundColor: 'transparent' }}>
              <View style={{ flex: 1, alignItems: 'center' }}>                
                  <Text style={styles.textBody}>Audio track</Text>
                  <StreamPicker streams={audioStreams} style={styles.picker} onValueChange={this.pickerChange} enabled={true} />
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                  <Text style={styles.textBody}>Video quality</Text>
                  <StreamPicker streams={videoStreams} style={styles.picker} onValueChange={this.pickerChange} enabled={true} />
              </View>
              <View style={{ flex: 1, alignItems: 'center' }}>
                  <Text style={styles.textBody}>Subtitles</Text>
                  <StreamPicker streams={subtitleStreams} style={styles.picker} onValueChange={this.pickerChange} enabled={true} />
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