'use strict';
import React from 'react';
import { View, Image, ScrollView, NativeModules, Dimensions, DeviceEventEmitter } from 'react-native';

import ContentPicture from './ContentPicture';
import ContentDescription from './ContentDescription';
import ResourceLoader from '../ResourceLoader';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;
const itemWidth = 454;
const itemHeight = 260;

export default class ContentScroll extends React.Component {
  constructor(props) {
    super(props);
    this.renderedIndex = -1;
    this.selectedIndex = 0;
    this.deepLinkIndex = 0;
    this.numItems = this.props.contentURIs.length;
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.updateIndex = this.updateIndex.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
  }

  updateIndex(newIndex, animate = true)
  {
    if(newIndex < 0 || newIndex >= this.numItems || newIndex == this.selectedIndex)
      return;

    let scrolloffset = newIndex * itemWidth;
    this._scrollView.scrollTo({ x: scrolloffset, y: 0, animated: animate });
    this.selectedIndex = newIndex;
    this.props.onSelectedIndexChange(newIndex);  
  }

  shouldComponentUpdate(nextProps, nextState) {
    let updateRequired = (this.renderedIndex != this.selectedIndex);
    return updateRequired;
  }

  componentWillMount() {
    DeviceEventEmitter.addListener('ContentScroll/onTVKeyDown', this.onTVKeyDown);
  }
  
  componentWillUnmount() {
    DeviceEventEmitter.removeListener('ContentScroll/onTVKeyDown', this.onTVKeyDown);
  }

  componentWillReceiveProps(nextProps) {
    if(this.deepLinkIndex == nextProps.deepLinkIndex)
      return;

    this.deepLinkIndex = nextProps.deepLinkIndex;    
    this.updateIndex(nextProps.deepLinkIndex, false);
  }

  onTVKeyDown(pressed) {

    //There are two parameters available:
    //params.KeyName
    //params.KeyCode
    
    switch (pressed.KeyName) {
      case 'Right':
        this.updateIndex(this.selectedIndex + 1);
        break;

      case 'Left':
        this.updateIndex(this.selectedIndex - 1);
        break;
    }
  }

  render() {
    this.renderedIndex = this.selectedIndex;
    const index = this.renderedIndex;
    const title = ResourceLoader.clipsData[index].title;
    const description = ResourceLoader.clipsData[index].description;
    const overlayIcon = ResourceLoader.playbackIcons.play;
    
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
    return (
      <View style={{ height: height, width: width }}>
        <View
          style={{
            top: '10%',
            left: '5%',
            width: 900,
            height: 750
          }}>
          <ContentDescription
            viewStyle={{
              width: '100%',
              height: '100%'
            }}
            headerStyle={{ fontSize: 60, color: '#ffffff' }}
            bodyStyle={{ fontSize: 30, color: '#ffffff', top: 0 }}
            headerText={title}
            bodyText={description}
          />
        </View>
        <View>
          <ScrollView
            scrollEnabled={false}
            ref={scrollView => {
              this._scrollView = scrollView;
            }}
            automaticallyAdjustContentInsets={false}
            scrollEventThrottle={0}
            horizontal={true}
            showsHorizontalScrollIndicator={false}>
            {this.props.contentURIs.map(renderThumbs)}
          </ScrollView>
        </View>
      </View>
    );
  }
}
