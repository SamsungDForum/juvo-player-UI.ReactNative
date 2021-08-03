/**
 * Juvo React Native App
 * https://github.com/facebook/react-native
 * @flow
 */

'use strict'
import React, {Component} from 'react';
import {
  AppRegistry,
  NativeModules,
  NativeEventEmitter,
} from 'react-native';


import ResourceLoader from './src/ResourceLoader';
import RenderScene from './src/views/RenderScene'; 
import RenderView from './src/views/RenderView';

const initialModal = RenderView.viewInProgress;
initialModal.args = {messageText: 'Getting... ready'};
const initialScene = { modalView: initialModal };

export default class JuvoReactNative extends Component 
{
  constructor(props) 
  {
    console.debug('JuvoReactNative.constructor():');

    super(props);
    this.selectedClipIndex = 0;
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = null;
    this.handleDeepLink = this.handleDeepLink.bind(this);
    this.finishLoading = this.finishLoading.bind(this);
    this.loadingPromise = ResourceLoader.load();

    console.debug('JuvoReactNative.constructor(): done');
  };

  componentDidMount() {
    console.debug('JuvoReactNative.componentDidMount():');
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.JuvoEventEmitter.addListener('handleDeepLink', async (deepLink) => await this.handleDeepLink(deepLink));
    this.JuvoPlayer.AttachDeepLinkListener();
    
    setImmediate(async ()=> 
    {
      console.debug('JuvoReactNative.loadResources():');

      await this.finishLoading();

      const index = this.selectedClipIndex;
      const catalogView = RenderView.viewContentCatalog;
      catalogView.args = { selectedIndex: index };
      RenderScene.setScene(catalogView, RenderView.viewNone);

      console.debug('JuvoReactNative.loadResources(): done');
    });
    console.debug('JuvoReactNative.componentDidMount(): done');
  }

  componentWillUnmount()
  {
    console.debug('JuvoReactNative.componentWillUnmount():');

    this.JuvoEventEmitter.removeAllListeners('handleDeepLink');
    delete this.JuvoEventEmitter;

    console.debug('JuvoReactNative.componentWillUnmount(): done');
  }

  async finishLoading() 
  {
    console.debug('JuvoReactNative.finishLoading():');
    if(this.loadingPromise)
    {
      await this.loadingPromise;
      this.loadingPromise = null;
    }
    console.debug('JuvoReactNative.finishLoading(): done.');
  }

  async handleDeepLink(deepLink) 
  {
    console.log(`JuvoReactNative.handleDeepLink(): Processing deep link url ${deepLink.url}`);

    let index = ResourceLoader.clipsData.findIndex(e => e.url === deepLink.url);
    if (index == -1) 
    {
      console.log(`JuvoReactNative.handleDeepLink(): Url ${deepLink.url} index not found`);
      return;
    }

    await finishLoading();

    this.selectedClipIndex = index;
    const playbackView = RenderView.viewPlayback;
    playbackView.args = {selectedIndex:index};

    const inProgressView = RenderView.viewInProgress;
    inProgressView.args = {messageText: `Starting '${ResourceLoader.clipsData[index].title}'`};

    RenderScene.setScene(playbackView, inProgressView);
  
    console.log('JuvoReactNative.handleDeepLink(): done.');
  }
  
  render()
  {
    try
    {
      console.debug('JuvoReactNative.render(): done');
      return ( <RenderScene initialScene={initialScene}/> );
    }
    catch(error)
    {
      console.error(`JuvoReactNative.render(): ${error}`);
    }
  }
}

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);