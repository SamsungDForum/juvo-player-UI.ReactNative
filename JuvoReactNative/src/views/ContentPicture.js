'use strict';
import React from 'react';
import { View, Image, NativeModules } from 'react-native';

import HideableView from './HideableView';
import ResourceLoader from '../ResourceLoader';

export default class ContentPicture extends React.Component {
  constructor(props) {
    super(props);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
  }

  render() {
    const index = typeof this.props.myIndex !== 'undefined' ? this.props.myIndex : -1;
    const selectedIndex = this.props.selectedIndex;
    const source = this.props.path ? {uri: this.props.path} : this.props.source ? this.props.source : ResourceLoader.defaultImage;

    const style = {
      width:  this.props.width ? this.props.width : 1920,
      height: this.props.height ? this.props.height : 1080,
      top: this.props.top ? this.props.top : 0,
      left:  this.props.left ? this.props.left : 0,
    };

    const pos = this.props.position;

    const fadeDuration = this.props.fadeDuration ? this.props.fadeDuration : 1;
    const visible = this.props.visible ? this.props.visible : true;

    const onLoadStart = this.props.onLoadStart;
    const onLoadEnd = this.props.onLoadEnd;
    const onError = err => console.error(err);

    if (selectedIndex == index) 
    {
      const stylesThumbSelected = this.props.stylesThumbSelected ? this.props.stylesThumbSelected : { width: style.width, height: style.height };

      return (
        <HideableView position={pos} visible={visible} duration={fadeDuration}>
          <View style={stylesThumbSelected}>
            <Image
              resizeMode='cover'
              style={style}
              source={source}
              onLoadStart={onLoadStart}
              onLoadEnd={onLoadEnd}
              onError={onError}
            />
          </View>
        </HideableView>
      );
    } 
    else 
    {
      const stylesThumb = this.props.stylesThumb ? this.props.stylesThumb : { width: style.width, height: style.height };

      return (
        <HideableView position={pos} visible={visible} duration={fadeDuration}>
          <View style={stylesThumb}>
            <Image
              resizeMode='cover'
              style={style}              
              source={source}
              onLoadStart={onLoadStart}
              onLoadEnd={onLoadEnd}
              onError={onError}
            />
          </View>
        </HideableView>
      );
    }
  }
}
