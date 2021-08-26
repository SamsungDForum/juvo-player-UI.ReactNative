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
const invalidIndex = -1;

function getClipIndexFromDeepLink(deepLink) 
{
  console.log(`getClipIndexFromDeepLink(): Processing deep link url ${deepLink.url}`);

  const index = ResourceLoader.clipsData.findIndex(e => e.url === deepLink.url);
  if (index == invalidIndex) 
    console.log(`getClipIndexFromDeepLink(): Url ${deepLink.url} index not found`);

  return index;
}

export default class JuvoReactNative extends Component 
{
  constructor(props) 
  {
    console.debug('JuvoReactNative.constructor():');

    super(props);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
        
    this.handleDeepLink = this.handleDeepLink.bind(this);
    this.playClipIndex = this.playClipIndex.bind(this);
    this.completeInitalization = this.completeInitalization.bind(this);

    this.loadingPromise = ResourceLoader.load();
    this.initializationPromise = null;
    this.JuvoEventEmitter = null;
    
    console.debug('JuvoReactNative.constructor(): done');
  };

  componentDidMount()
  {
    console.debug('JuvoReactNative.componentDidMount():');
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.initializationPromise = this.completeInitalization();
    setImmediate(async ()=> 
    {
      try
      {
        await this.initializationPromise;
      }
      catch(error)
      {
        console.error(`JuvoReactNative.componentDidMount(): ERROR ${error}`);
        const errorPopup = RenderView.viewPopup;
        errorPopup.args = {messageText: 'Failed to initialize'};
        RenderScene.setScene(RenderView.viewCurrent, errorPopup);
      }
      this.initializationPromise = null;
    });

    console.debug('JuvoReactNative.componentDidMount(): done');
  }

  async componentWillUnmount()
  {
    console.debug('JuvoReactNative.componentWillUnmount():');

    // await mount completion if running to cleanup event subscriptions.
    const initInProgress = this.initializationPromise;
    if(initInProgress)
      await initInProgress;

    this.JuvoEventEmitter.removeAllListeners('handleDeepLink');
    delete this.JuvoEventEmitter;

    console.debug('JuvoReactNative.componentWillUnmount(): done');
  }

  async completeInitalization()
  {
    console.debug('JuvoReactNative.completeInitalization():');

    if(this.loadingPromise)
    {
      await this.loadingPromise;
      this.loadingPromise = null;
    }

    this.JuvoEventEmitter.addListener('handleDeepLink', deepLink => 
    {
      setImmediate(this.handleDeepLink,deepLink);
    });

    this.JuvoPlayer.AttachDeepLinkListener();

    console.debug('JuvoReactNative.completeInitalization(): done');
  }
 
  playClipIndex(index)
  {
    const playbackView = RenderView.viewPlayback;
    playbackView.args = {selectedIndex:index};

    const inProgressView = RenderView.viewInProgress;
    inProgressView.args = {messageText: `Starting '${ResourceLoader.clipsData[index].title}'`};

    RenderScene.setScene(playbackView, inProgressView);
  
    console.log('JuvoReactNative.playClipIndex(): done.');
  }
  
  handleDeepLink(deepLink)
  {
    console.debug('JuvoReactNative.handleDeepLink():');

    if(deepLink.url == '')
    {
      const currentScene = RenderScene.getScene();
      
      if(currentScene.mainView.name == RenderView.viewNone.name)
      {
        // Have nothing, show default content catalog.
        console.log('JuvoReactNative.handleDeepLink(): empty url. Showing default content catalog.');
        const contentSelection = RenderView.viewContentCatalog;
        contentSelection.args = {selectedIndex: 0};
        RenderScene.setScene(contentSelection,RenderView.viewCurrent);
      }
      else
      {
        // Have something, don't change it.
        console.log(`JuvoReactNative.handleDeepLink(): empty url. Showing main '${currentScene.mainView.name}' modal '${currentScene.modalView.name}'`);
        RenderScene.setScene(RenderView.viewCurrent,RenderView.viewCurrent);
      }
    }
    else
    {
      const index = getClipIndexFromDeepLink(deepLink);
      if(index == invalidIndex)
        console.warn(`JuvoReactNative.handleDeepLink(): Url '${deepLink.url}' not found.`);
      else
        this.playClipIndex(index);
    }

    console.log('JuvoReactNative.handleDeepLink(): done');
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