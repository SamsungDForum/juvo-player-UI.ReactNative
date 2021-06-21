/**
 * Juvo React Native App
 * https://github.com/facebook/react-native
 * @flow
 */

'use strict'
import React, {Component} from 'react';
import {
  StyleSheet,
  View,
  AppRegistry,
  NativeModules,
  NativeEventEmitter,
  Dimensions,
  NavigationExperimental,
  DeviceEventEmitter
} from 'react-native';

import ContentCatalog from './src/views/ContentCatalog';
import PlaybackView from './src/views/PlaybackView';
import ResourceLoader from "./src/ResourceLoader";
import InProgressView from "./src/views/InProgressView";
import NotificationPopup from "./src/views/NotificationPopup";

const {
  CardStack: NavigationCardStack,
  StateUtils: NavigationStateUtils
} = NavigationExperimental;

function createReducer(initialState) {
  return (currentState = initialState, action) => {
    switch (action.type) {
      case 'forward':
        return NavigationStateUtils.forward(currentState);
      case 'back':
        return NavigationStateUtils.back(currentState);
      case 'push':
        return NavigationStateUtils.push(currentState, {key: action.key});
      case 'pop':
        return currentState.index > 0 ?
          NavigationStateUtils.pop(currentState) :
          currentState;
      default:
        return currentState;
    }
  }
}

const NavReducer = createReducer({
  index: 0,
  key: 'App',
  routes: [{key: 'ContentCatalog'}]
});

export default class JuvoReactNative extends Component {
  constructor(props) {
    super(props);
    this.state = {
      loading: true,
      navState: NavReducer(undefined, {}),
    };
    this.selectedClipIndex = 0;
    this.deepLinkIndex = 0;

    this.switchComponentsView = this.switchComponentsView.bind(this);
    this.handleSelectedIndexChange = this.handleSelectedIndexChange.bind(this);
    this.handleDeepLink = this.handleDeepLink.bind(this);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.currentView = this.currentView.bind(this);
    this.finishLoading = this.finishLoading.bind(this);
    this.renderProgress = this.renderProgress.bind(this);
    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.LoadingPromise = ResourceLoader.load();
  }

  onTVKeyDown(pressed) {
    DeviceEventEmitter.emit(`${this.currentView()}/onTVKeyDown`, pressed);
  }

  async componentDidMount() {
    this.JuvoEventEmitter.addListener("onTVKeyDown", this.onTVKeyDown);
    this.JuvoEventEmitter.addListener("handleDeepLink", async (deepLink)=> 
    {
      await this.handleDeepLink(deepLink);
    });

    this.JuvoPlayer.AttachDeepLinkListener();
    await this.finishLoading();
  }

  async finishLoading() {
    await this.LoadingPromise;
    this.setState({loading: false});
  }

  currentView()
  {
    return this.state.navState.routes[this.state.navState.index].key;
  }

//It is assumed that at the only one component can be visible on the screen
  switchComponentsView(componentName) {
    switch (componentName) {
      case 'Previous':
        this.handleAction({type: 'pop'});
        break;
      case 'PlaybackView':
        this.handleAction({type: 'push', key: 'PlaybackView'});
        break;
      default:
        alert('Undefined view');
    }
    this.setState({});
  }

  handleSelectedIndexChange(index) {
    this.selectedClipIndex = index;
  }

  async handleDeepLink(deepLink) {
    if (deepLink.url !== null) {
      if (this.state.loading) {
        console.log(`JuvoReactNative.handleDeepLink(): Url ${deepLink.url} waiting for load completion`);
        await this.LoadingPromise;
      }

      let index = ResourceLoader.clipsData.findIndex(e => e.url === deepLink.url);
      if (index !== -1) {
        console.log(`JuvoReactNative.handleDeepLink(): Processing deep link url ${deepLink.url}`);
        this.deepLinkIndex = index;
        this.handleSelectedIndexChange(index);
        this.switchComponentsView('PlaybackView');
      }
      else
      {
        console.log(`JuvoReactNative.handleDeepLink(): Url ${deepLink.url} index not found`);
      }
    }
  }

  handleAction(action) {
    const newState = NavReducer(this.state.navState, action);
    if (newState === this.state.navState) {
      return false;
    }
    this.setState({
      navState: newState
    });
    return true;
  }

  renderProgress() {
    if (ResourceLoader.errorMessage)
      return (
          <View style={[styles.notification]}>
            <NotificationPopup visible={true} onNotificationPopupDisappeared={this.JuvoPlayer.ExitApp} messageText={ResourceLoader.errorMessage} />
          </View>
      );
    if (this.state.loading)
      return (
          <View style={[styles.notification]}>
            <InProgressView visible={true} message="Please wait..."/>
          </View>
      );
    return null;
  }

  renderRoute(key) {
    if (key === 'ContentCatalog') {
      return <ContentCatalog
        visibility={true}
        switchView={this.switchComponentsView}
        onSelectedIndexChange={this.handleSelectedIndexChange}
        deepLinkIndex={this.deepLinkIndex}/>
    }
    if (key === 'PlaybackView') {
      return <PlaybackView
        visibility={true}
        switchView={this.switchComponentsView}
        selectedIndex={this.selectedClipIndex}/>
    }
  }

  renderScene(props) {
    var progress = this.renderProgress();
    if (progress)
      return progress;
    const ComponentToRender = this.renderRoute(props.scene.route.key);
    return (
      ComponentToRender
    );
  }

  render() {
      return (
        <NavigationCardStack
          cardStyle={{backgroundColor: 'transparent'}}
          navigationState={this.state.navState}
          onNavigate={this.handleAction.bind(this)}
          renderScene={this.renderScene.bind(this)}/>
      );
  }
}

const styles = StyleSheet.create({
  notification: {
    height: "100%", 
    justifyContent: "center", 
    alignItems: "center", 
    backgroundColor: "black"
  }
});

AppRegistry.registerComponent('JuvoReactNative', () => JuvoReactNative);
