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
    this.state = {
      selectedClipIndex: 0
    };
    
    this.bigPictureVisible = this.props.visibility;
    this.keysListenningOff = false;
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
  }

  componentWillMount() {
    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
  }

  componentWillUnmount() {
    DeviceEventEmitter.removeListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
  }

  async componentDidUpdate(prevProps, prevState) {
    this.JuvoPlayer.Log('ContentCatalog.componentDidUpdate():');
    return true;
  }

  shouldComponentUpdate(nextProps, nextState) {
    this.JuvoPlayer.Log('ContentCatalog.shouldComponentUpdate():');
    return true;
  }

  toggleVisibility() {
    this.JuvoPlayer.Log('ContentCatalog.toggleVisibility():');
    this.props.switchView('PlaybackView');
  }
  
  onTVKeyDown(pressed) {
    //There are two parameters available:
    //pressed.KeyName
    //pressed.KeyCode
    DeviceEventEmitter.emit('ContentScroll/onTVKeyDown', pressed);
    this.JuvoPlayer.Log('ContentCatalog.onTVKeyDown(): '+pressed.KeyName+' ignore: '+this.keysListenningOff+' Visible: '+this.bigPictureVisible);

    if (this.keysListenningOff) return;

    switch (pressed.KeyName) {
      case 'XF86AudioStop':
      case 'Return':
      case 'XF86AudioPlay':
      case 'XF86PlayBack':
        if(this.debounceIndexChange != debounceCompleted)
        {
          // Clear pending updates. Gets updated during playback start thus make index up to date.
          clearTimeout(this.debounceIndexChange);
          this.bigPictureVisible = true;
          this.state.selectedClipIndex = this.candidateIndex;
        }

        this.toggleVisibility();
        break;

      case 'XF86Back':
        // Exiting, don't bother updating this.debounceIndexChange.
        if(this.debounceIndexChange != debounceCompleted)
          clearTimeout(this.debounceIndexChange);
        
        this.JuvoPlayer.ExitApp();
        break;
    }
  }
 
  onIndexChangeDebounceCompleted()
  {
    clearTimeout(this.debounceIndexChange);
    this.debounceIndexChange = debounceCompleted;

    this.bigPictureVisible = true;

    this.JuvoPlayer.Log('ContentCatalog.onIndexChangeDebounceCompleted(): Index Set: '+this.candidateIndex +' Pending loads: '+this.pendingLoads);
    
    this.setState({
      selectedClipIndex: this.candidateIndex
    });
     
  }

  handleSelectedIndexChange(index) {
    
    this.JuvoPlayer.Log('ContentCatalog.handleSelectedIndexChange(): Candidate index: '+ index );
    this.props.onSelectedIndexChange(index);

    if(this.bigPictureVisible)
    {
      this.bigPictureVisible = false;
      this.JuvoPlayer.Log('ContentCatalog.handleSelectedIndexChange(): Hiding Big pic.');
      this.forceUpdate(this.render);
      this.JuvoPlayer.Log('ContentCatalog.handleSelectedIndexChange(): Hiding Big pic. DONE');
    }

    if(this.debounceIndexChange == debounceCompleted)
    {
      this.JuvoPlayer.Log('ContentCatalog.handleSelectedIndexChange(): debouncing index change');
    }

    clearTimeout(this.debounceIndexChange);
    this.candidateIndex = index;
    this.debounceIndexChange = setTimeout(this.onIndexChangeDebounceCompleted,debounceTimeout);
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
      this.forceUpdate(this.render);
  }

  render() {
    this.JuvoPlayer.Log('ContentCatalog.render( '+this.state.selectedClipIndex+' ) Big visible: '+this.bigPictureVisible + ' Pending loads: '+this.pendingLoads);

    const index = this.state.selectedClipIndex;
    const path = ResourceLoader.tilePaths[index];
    const overlay = ResourceLoader.contentDescriptionBackground;
    this.keysListenningOff = !this.props.visibility;
    const showBigPicture = this.bigPictureVisible;
    
    const onLoadStart = this.handleBigPicLoadStart;
    const onLoadEnd = this.handleBigPicLoadEnd;
    const indexChange = this.handleSelectedIndexChange;

    return (
      <HideableView visible={this.props.visibility} duration={300}>
        <View style={[styles.page, { alignItems: 'flex-end' }]}>
          <View style={[styles.cell, { height: '70%', width: '70%' }]}>
            <HideableView visible={showBigPicture} duration={100}>
              <ContentPicture selectedIndex={index} path={path} width={'100%'} height={'100%'} 
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
            keysListenningOff={this.keysListenningOff}
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
