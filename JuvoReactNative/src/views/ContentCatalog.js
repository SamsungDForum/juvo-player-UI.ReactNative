'use strict';
import React, { Component } from 'react';
import { Image, NativeModules, Dimensions, StyleSheet, DeviceEventEmitter, } from 'react-native';
import PropTypes from 'prop-types'

import FadableView from './FadableView';
import ContentScroll from './ContentScroll';
import ResourceLoader from '../ResourceLoader';
import ContentDescription from './ContentDescription';

import RenderScene from './RenderScene'; 
import RenderView from './RenderView';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;
const invalidTimeoutId = -1;
const debounceTimeout = 200; // 200ms idle timeout
const overlayUri = ResourceLoader.contentDescriptionBackground;

export default class ContentCatalog extends Component 
{
  constructor(props) 
  {
    super(props);
    this.state = 
    {
      selectedClipIndex: props.selectedIndex,
      renderBackground: true,
    };

    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.onIndexChangeDebounceCompleted = this.onIndexChangeDebounceCompleted.bind(this);
    
    this.debounceIndexChangeTimeoutId = invalidTimeoutId;
    this.candidateIndex = props.selectedIndex;
  }
  
  componentDidMount() 
  {
    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);
    console.debug('ContentCatalog.componentDidMount(): done');
  }

  componentWillUnmount() 
  {
    DeviceEventEmitter.removeAllListeners('ContentCatalog/onTVKeyDown');
    console.debug('ContentCatalog.componentWillUnmount(): done');
  }

  shouldComponentUpdate(nextProps, nextState)
  {
    const updateRequired =  nextState.selectedClipIndex != this.state.selectedClipIndex ||
                            nextState.renderBackground != this.state.renderBackground;
    console.debug(`ContentCatalog.shouldComponentUpdate(): ${updateRequired}`);
    return updateRequired;
  }

  onTVKeyDown(pressed) 
  {
    console.debug(`ContentCatalog.onTVKeyDown(): key ${pressed.KeyName}`);

    switch (pressed.KeyName) 
    {
      case 'Return':
        if(this.debounceIndexChangeTimeoutId != invalidTimeoutId) 
        {
          clearTimeout(this.debounceIndexChangeTimeoutId);
          this.debounceIndexChangeTimeoutId = invalidTimeoutId;
        }
        
        // Use candidate rather then selected index.
        // On debounce complete, candidateIndex = selectedIndex
        // During debounce, candidate index is one to be played.
        const playIndex = this.candidateIndex;
        const playbackView = RenderView.viewPlayback;
        playbackView.args = { selectedIndex: playIndex };
        
        const inProgressView = RenderView.viewInProgress;
        inProgressView.args = { messageText: 'Almost there...'};

        RenderScene.setScene(playbackView,inProgressView);    
        break;

      case 'XF86Back':
        this.JuvoPlayer.ExitApp();
        break;

      default:
        console.debug(`ContentCatalog.onTVKeyDown(): done. ${pressed.KeyName} ignored`);
        return;
    }

    console.log(`ContentCatalog.onTVKeyDown(): done. ${pressed.KeyName} processed`);
  }
 
  onIndexChangeDebounceCompleted()
  {
    console.debug(`ContentCatalog.onIndexChangeDebounceCompleted():`);

    this.debounceIndexChangeTimeoutId = invalidTimeoutId;
    const newIndex = this.candidateIndex;
 
    this.setState({
      selectedClipIndex: newIndex,
      renderBackground: true
    });
    console.debug(`ContentCatalog.onIndexChangeDebounceCompleted(): done. Index ${newIndex}`);
  }

  handleSelectedIndexChange(index)
  {
    console.debug('ContentCatalog.handleSelectedIndexChange():');
    
    if(this.debounceIndexChangeTimeoutId != invalidTimeoutId)
    {
      clearTimeout(this.debounceIndexChangeTimeoutId);
      this.debounceIndexChangeTimeoutId = invalidTimeoutId;
    }
    
    this.candidateIndex = index;
   
    if(this.state.renderBackground)
    {
      console.debug('ContentCatalog.handleSelectedIndexChange(): Hiding big pic');
      this.setState({renderBackground: false});
    }

    this.debounceIndexChangeTimeoutId = setTimeout(this.onIndexChangeDebounceCompleted, debounceTimeout);
  }

  render() {
    try
    {
      console.debug(`ContentCatalog.render():`);

      const isBigPicVisible = this.state.renderBackground;
      const index = this.state.selectedClipIndex;

      const remoteUri = { uri: ResourceLoader.tilePaths[index]};
      
      const title = ResourceLoader.clipsData[index].title;
      const description = ResourceLoader.clipsData[index].description;

      console.debug(`ContentCatalog.render(): done. index: ${index} bigPicVisible ${isBigPicVisible}`);

      return (
        <FadableView style={styles.contentCatalog} duration={300} >
          <Image source={remoteUri} style={styles.backgroundPicture} resizeMode='cover' opacity={isBigPicVisible?1:0} fadeDuration={100}/>
          <Image source={overlayUri} style={styles.foregroundPicture} resizeMode='cover' />
          <ContentDescription style={styles.contentDescription} headerText={title} bodyText={description} />
          <ContentScroll style={styles.contentScroll} onSelectedIndexChange={this.handleSelectedIndexChange} initialIndex={this.props.selectedIndex} />
        </FadableView>
      );
    }
    catch(error)
    {
        console.error(`ContentCatalog.render(): ERROR ${error.toString()}`);
    }
  }
}

ContentCatalog.propTypes = {
  selectedIndex: PropTypes.number.isRequired,
};

const styles = StyleSheet.create(
{
  contentCatalog:
  {
    width: width,
    height: height,
    position: 'absolute',
  },

  contentDescription: 
  {
    left: '5%',
    width: '45%',
    top: '10%',
    height: '60%',
    position: 'absolute',
  },

  backgroundPicture:
  {
    left: '30%',
    width: '70%',
    height: '70%',
    position: 'absolute',
  },
  foregroundPicture:
  {
    left: '30%',
    width: '70%',
    height: '70%',
    position: 'absolute',
  },
  contentScroll:
  {
    left: '3%',
    width: '97%',
    top: '70%',
    height: '30%',
    position: 'absolute',
  },
});
