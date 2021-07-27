'use strict';
import React, {Component} from 'react';
import { View, Image, ScrollView, NativeModules, DeviceEventEmitter } from 'react-native';

import ContentPicture from './ContentPicture';
import ResourceLoader from '../ResourceLoader';

const itemWidth = 454;
const itemHeight = 260;

const overlayIcon = ResourceLoader.playbackIcons.play;

export default class ContentScroll extends Component 
{
  constructor(props) 
  {
    super(props);
    this.state = {
      renderIndex: props.initialIndex,
    },

    this.selectedIndex = props.initialIndex;
    this.numItems = ResourceLoader.clipsData.length;
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.updateIndex = this.updateIndex.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
  }

  updateIndex(newIndex, animate = true)
  {
    if(newIndex < 0 || newIndex >= this.numItems)// || newIndex == this.selectedIndex)
      return;
  
    console.debug(`ContentScroll.updateIndex(): Index ${this.selectedIndex}->${newIndex} animate ${animate}`);
    this.selectedIndex = newIndex;
    const scrolloffset = newIndex * itemWidth;
    this._scrollView.scrollTo({ x: scrolloffset, y: 0, animated: animate });
    this.props.onSelectedIndexChange(newIndex);
    this.setState({renderIndex: newIndex});
  }

  componentWillMount() 
  {
    console.debug('ContentScroll.componentWillMount():');

    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);

    console.debug('ContentScroll.componentWillMount(): done');
  }
  
  componentWillUnmount() 
  {
    console.debug('ContentScroll.componentWillUnmount():');

    DeviceEventEmitter.removeListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);

    console.debug('ContentScroll.componentWillUnmount(): done');
  }

  shouldComponentUpdate(nextProps, nextState)
  {
    const updateRequired = this.state.renderIndex != nextState.renderIndex;
    console.debug(`ContentScroll.shouldComponentUpdate(): ${updateRequired}`);
    return updateRequired;
  }

  onTVKeyDown(pressed) {

    //There are two parameters available:
    //params.KeyName
    //params.KeyCode
    
    console.debug(`ContentScroll.onTVKeyDown(): ${pressed.KeyName}`);
    switch (pressed.KeyName) {
      case 'Right':
        this.updateIndex(this.selectedIndex + 1);
        break;

      case 'Left':
        this.updateIndex(this.selectedIndex - 1);
        break;

      default:
        console.debug(`ContentScroll.onTVKeyDown(): done. key '${pressed.KeyName}' ignored`);
        return;
    }

    console.debug(`ContentScroll.onTVKeyDown(): done. key '${pressed.KeyName}' processed`);
  }

  render() {

    try
    {
      console.debug(`ContentScroll.render():`);

      const index = this.state.renderIndex;
      const uris = ResourceLoader.tilePaths;
      const clipsData = ResourceLoader.clipsData;

      const renderThumbs = (uri, i) => (
        <View key={i}>
          <Image resizeMode='cover' style={{ top: itemHeight / 2 + 35, left: itemWidth / 2 - 25 }} source={overlayIcon} />
          <ContentPicture
            myIndex={i}
            selectedIndex={index}
            path={uri}
            width={itemWidth - 8}
            height={itemHeight - 8}
            top={4}
            left={4}
            fadeDuration={1}
            stylesThumbSelected={{
              width: itemWidth,
              height: itemHeight,
              backgroundColor: 'transparent',
              opacity: 0.3
            }}
            stylesThumb={{
              width: itemWidth,
              height: itemHeight,
              backgroundColor: 'transparent',
              opacity: 1
            }}
          />
        </View>
      );

      console.debug(`ContentScroll.render(): done. index '${index}' uri# '${uris.length} clips# '${clipsData.length}'`);

      return (        
        <ScrollView
          style={this.props.style}
          scrollEnabled={false}
          ref={scrollView => {
            this._scrollView = scrollView;
          }}
          automaticallyAdjustContentInsets={false}
          scrollEventThrottle={0}
          horizontal={true}
          showsHorizontalScrollIndicator={false}>
          {uris.map(renderThumbs)}
        </ScrollView>
      );
    }
    catch(error) {
      console.error(`ContentScroll.render(): ERROR ${error}`);
    }
  }
}
