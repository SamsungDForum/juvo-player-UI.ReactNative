'use strict';
import React, { Component } from 'react';
import { View, NativeModules, Dimensions, StyleSheet, DeviceEventEmitter } from 'react-native';

import HideableView from './HideableView';
import ContentPicture from './ContentPicture';
import ContentScroll from './ContentScroll';
import ResourceLoader from '../ResourceLoader';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;
const debounceCompleted = -1;
const debounceTimeout = 200; // 200ms idle timeout

export default class ContentCatalog extends Component {
  constructor(props) {
    super(props);
    this.state = {
      selectedClipIndex: 0,
      bigPicVisible: true
    };

    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.onIndexChangeDebounceCompleted = this.onIndexChangeDebounceCompleted.bind(this);
    this.debounceIndexChange = debounceCompleted;
    this.candidateIndex = 0;
  }
  
  componentDidMount() {
    console.debug('ContentCatalog.componentDidMount()');
    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
  }

  componentWillUnmount() {
    console.debug('ContentCatalog.componentWillUnmount()');
    DeviceEventEmitter.removeListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
  }

  onTVKeyDown(pressed) {
    //There are two parameters available:
    //pressed.KeyName
    //pressed.KeyCode
    
    switch (pressed.KeyName) {
      case 'XF86AudioStop':
      case 'Return':
      case 'XF86AudioPlay':
      case 'XF86PlayBack':
        if(this.debounceIndexChange != debounceCompleted) 
        {
          clearTimeout(this.debounceIndexChange);
          onIndexChangeDebounceCompleted();
        }
        
        this.props.switchView('PlaybackView');
        break;

      case 'XF86Back':
        this.JuvoPlayer.ExitApp();
        break;
    }
  }
 
  onIndexChangeDebounceCompleted()
  {
    this.debounceIndexChange = debounceCompleted;
 
    this.setState(
      {
        selectedClipIndex: this.candidateIndex,
        bigPicVisible: true
    });

    console.debug('ContentCatalog.onIndexChangeDebounceCompleted(): done');
  }

  handleSelectedIndexChange(index) {

    console.debug('ContentCatalog.handleSelectedIndexChange():');
    
    if(this.debounceIndexChange != debounceCompleted)
    {
      clearTimeout(this.debounceIndexChange);
      this.debounceIndexChange = debounceCompleted;
    }
    
    // Update index.tizen.js with new index
    this.props.onSelectedIndexChange(index);

    this.candidateIndex = index;
   
    if(this.state.bigPicVisible)
    {
      console.debug('ContentCatalog.handleSelectedIndexChange(): Hiding big pic');
      this.setState({bigPicVisible: false});
    }

    this.debounceIndexChange = setTimeout(this.onIndexChangeDebounceCompleted,debounceTimeout);
  }

  render() {
    try
    {
      const isVisible = this.props.visible;
      const isBigPicVisible = (this.state.bigPicVisible && isVisible);
      
      const index = this.state.selectedClipIndex;
      const remoteUri = Object.freeze({ uri: ResourceLoader.tilePaths[index]});
      
      const overlay = ResourceLoader.contentDescriptionBackground;
      const indexChange = this.handleSelectedIndexChange;

      const deepLink = this.props.deepLinkIndex;

      console.debug(`ContentCatalog.render(): Visible: ${isVisible} BigPicVisible ${isBigPicVisible}`);
      return (
        <HideableView visible={isVisible} duration={300}>
          <View style={[styles.page, { alignItems: 'flex-end' }]}>
            <View style={[styles.cell, { height: '70%', width: '70%' }]}>
              <ContentPicture fadeDuration={100} visible={isBigPicVisible} source={remoteUri} width={'100%'} height={'100%'} />
              <ContentPicture visible={isVisible} position={'absolute'} source={overlay} selectedIndex={index} width={'100%'} height={'100%'} />
            </View>
          </View>
          <View style={[styles.page, { position: 'absolute' }]}>
            <ContentScroll
              onSelectedIndexChange={indexChange}
              contentURIs={ResourceLoader.tilePaths}
              deepLinkIndex={deepLink}
            />
          </View>
        </HideableView>
      );
    }
    catch(error)
    {
        console.error(error);
    }
  }
}

const styles = StyleSheet.create({
  page: {
    width: width,
    height: height
  },
  cell: {
    backgroundColor: 'black'
  }
});
