'use strict';
import React, { Component } from 'react';
import { View, NativeModules, NativeEventEmitter, Dimensions, StyleSheet, DeviceEventEmitter } from 'react-native';

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
    this.bigPictureVisible = this.props.visibility;
    this.toggleVisibility = this.toggleVisibility.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.handleBigPicLoadStart = this.handleBigPicLoadStart.bind(this);
    this.handleBigPicLoadEnd = this.handleBigPicLoadEnd.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.pendingLoads = 0;
    this.onIndexChangeDebounceCompleted = this.onIndexChangeDebounceCompleted.bind(this);
    this.debounceIndexChange = debounceCompleted;
    this.candidateIndex = 0;
    this.selectedClipIndex = 0;
  }

  componentWillMount() {
    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
  }

  componentWillUnmount() {
    DeviceEventEmitter.removeListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
  }

  toggleVisibility() {
    this.props.switchView('PlaybackView');
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
          onIndexChangeDebounceCompleted(false);
        }

        this.toggleVisibility();
        break;

      case 'XF86Back':
        this.JuvoPlayer.ExitApp();
        break;
    }
  }
 
  onIndexChangeDebounceCompleted(redraw = true)
  {
    clearTimeout(this.debounceIndexChange);
    this.debounceIndexChange = debounceCompleted;
    this.bigPictureVisible = true;
    this.selectedClipIndex = this.candidateIndex;

    if(redraw)
    {
      this.forceUpdate();
    }
  }

  handleSelectedIndexChange(index) {

    if(this.debounceIndexChange != debounceCompleted)
    {
      clearTimeout(this.debounceIndexChange);
    }
    
    // Update index.tizen.js with new index
    this.props.onSelectedIndexChange(index);
    this.candidateIndex = index;
    this.debounceIndexChange = setTimeout(this.onIndexChangeDebounceCompleted,debounceTimeout);

    if(this.bigPictureVisible)
    {
      this.bigPictureVisible = false;
      this.forceUpdate();
    }
  }

  handleBigPicLoadStart() {
    ++this.pendingLoads;
    this.JuvoPlayer.Log('ContentCatalog.onLoadStart(): Pending loads: '+this.pendingLoads);
  }

  handleBigPicLoadEnd() {
    --this.pendingLoads;
    this.JuvoPlayer.Log('ContentCatalog.onLoadEnd(): Pending loads: '+this.pendingLoads);

    // Don't refresh on last load.
    if(this.pendingLoads > 0)
    {
      this.forceUpdate();
    }
  }

  render() {
    const index = this.selectedClipIndex;
    const path = ResourceLoader.tilePaths[index];
    const overlay = ResourceLoader.contentDescriptionBackground;
    const showBigPicture = this.bigPictureVisible;
    
    const onLoadStart = this.handleBigPicLoadStart;
    const onLoadEnd = this.handleBigPicLoadEnd;
    const indexChange = this.handleSelectedIndexChange;

    return (
      <HideableView visible={this.props.visibility} duration={300}>
        <View style={[styles.page, { alignItems: 'flex-end' }]}>
          <View style={[styles.cell, { height: '70%', width: '70%' }]}>
            <HideableView visible={showBigPicture} duration={100}>
              <ContentPicture selectedIndex={index} visible={showBigPicture} path={path} width={'100%'} height={'100%'} 
                onLoadStart={onLoadStart} 
                onLoadEnd={onLoadEnd}/>
            </HideableView>
            <ContentPicture position={'absolute'} source={overlay} selectedIndex={index} width={'100%'} height={'100%'} />
          </View>
        </View>
        <View style={[styles.page, { position: 'absolute' }]}>
          <ContentScroll
            onSelectedIndexChange={indexChange}
            contentURIs={ResourceLoader.tilePaths}
            deepLinkIndex={this.props.deepLinkIndex}
          />
        </View>
      </HideableView>
    );
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
