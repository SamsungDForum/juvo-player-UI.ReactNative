'use strict';
import React, {Component} from 'react';
import { View, Image, ScrollView, NativeModules, DeviceEventEmitter, InteractionManager } from 'react-native';
import PropTypes from 'prop-types';

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
      renderIndex: props.selectedIndex,
    },

    this.numItems = ResourceLoader.clipsData.length;
    this.JuvoPlayer = NativeModules.JuvoPlayer;

    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.updateIndex = this.updateIndex.bind(this);
    this.selectIndex = this.selectIndex.bind(this);
    
    if(props.selectedIndex < 0 || props.selectedIndex >= this.numItems )
    {
      const errMsg = `ContentScroll.constructor(): Selected index '${props.selectedIndex}' out of range '0-${this.numItems}'`;
      console.error(errMsg);
      throw new Error(errMsg);
    }
  }

  componentWillMount() 
  {
    console.debug('ContentScroll.componentWillMount():');

    DeviceEventEmitter.addListener('ContentCatalog/onTVKeyDown', this.onTVKeyDown);

    // Assure this._scrollView ref is available prior to selecting target index. Ready to use after first render().
    InteractionManager.runAfterInteractions( () =>
    {
      this.selectIndex(this.props.selectedIndex, false);
      console.log('ContentScroll.scrollToInitial(): done');
    });

    console.debug(`ContentScroll.componentWillMount(): done. to Index '${this.props.selectedIndex}'`);
  }
  
  componentWillUnmount() 
  {
    console.debug('ContentScroll.componentWillUnmount():');

    DeviceEventEmitter.removeAllListeners('ContentCatalog/onTVKeyDown');

    console.debug('ContentScroll.componentWillUnmount(): done');
  }

  shouldComponentUpdate(nextProps, nextState)
  {
    // Scrolling operation does not require explicit render.
    const updateRequired = this.state.renderIndex != nextState.renderIndex;
    console.debug(`ContentScroll.shouldComponentUpdate(): ${updateRequired}`);
    return updateRequired;
  }

  selectIndex(validIndex, animate)
  {
    console.debug('ContentScroll.selectIndex():');

    this.setState({renderIndex: validIndex});
    this._scrollView.scrollTo({ x: validIndex * itemWidth, y: 0, animated: animate });
    
    console.log(`ContentScroll.selectIndex(): done. Index '${validIndex}' animated '${animate}'`);
  }

  updateIndex(newIndex, animate = true)
  {
    if(newIndex < 0 || newIndex >= this.numItems)
      return;
  
    this.selectIndex(newIndex, animate);
    
    if(this.props.onSelectedIndexChange)
      this.props.onSelectedIndexChange(newIndex);
  }

  onTVKeyDown(pressed) {

    //There are two parameters available:
    //params.KeyName
    //params.KeyCode
    
    console.debug(`ContentScroll.onTVKeyDown(): ${pressed.KeyName}`);
    switch (pressed.KeyName) {
      case 'Right':
        this.updateIndex(this.state.renderIndex + 1);
        break;

      case 'Left':
        this.updateIndex(this.state.renderIndex - 1);
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
      console.debug('ContentScroll.render():');

      const uris = ResourceLoader.tilePaths;
      const clipsData = ResourceLoader.clipsData;
      const index = this.state.renderIndex;     

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

ContentScroll.propTypes = {
  selectedIndex: PropTypes.number.isRequired,
};