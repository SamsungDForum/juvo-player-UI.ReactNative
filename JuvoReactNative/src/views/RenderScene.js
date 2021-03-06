'use strict';
import React, { Component } from 'react';

import { View, NativeModules, NativeEventEmitter, Dimensions, StyleSheet, DeviceEventEmitter, InteractionManager } from 'react-native';
import PropTypes from 'prop-types'

import {Debug} from '../ToolBox';
import RenderView from './RenderView';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

// instance accessor for class statics.
// Not expecting more then one instance of RenderScene.
const renderer =
{
  compose: null,
  currentScene: null,
  removeHiddenViews: null,
}

export default class RenderScene extends Component 
{
  static setScene(mainView = RenderView.viewNone, modalView = RenderView.viewNone)
  {
    renderer.compose(mainView,modalView);
  }

  static getScene()
  {
    return renderer.currentScene();
  }

  static removeHiddenViews()
  {
    renderer.removeHiddenViews();
  }

  constructor(props) 
  {
    console.debug('RenderScene.constructor():');

    super(props);

    this.mainView = RenderView.viewNone;
    this.modalView = RenderView.viewNone;
    let initialMain = null;
    let initialModal = null;

    if(props.initialScene)
    {
      if(props.initialScene.mainView)
      {
        this.mainView = props.initialScene.mainView;
        initialMain =  props.initialScene.mainView.getComponent(props.initialScene.mainView.args);
      }
      
      if(props.initialScene.modalView)
      {
        this.modalView = props.initialScene.modalView;
        initialModal = props.initialScene.modalView.getComponent(props.initialScene.modalView.args);
      }
    }

    this.state = 
    {
      main: initialMain,
      modal: initialModal,
    };

    this.JuvoPlayer = NativeModules.JuvoPlayer;
    this.JuvoEventEmitter = null;
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
    this.compose = this.compose.bind(this);
    this.composeMain = this.composeMain.bind(this);
    this.composeModal = this.composeModal.bind(this);
    this.currentScene = this.currentScene.bind(this);
    this.removeHiddenViews = this.removeHiddenViews.bind(this);

    this.displayScene = this.displayScene.bind(this);

    renderer.compose = this.compose;
    renderer.currentScene =  this.currentScene;
    renderer.removeHiddenViews = this.removeHiddenViews;

    console.debug('RenderScene.constructor(): done');
  }

  componentDidMount()
  {
    this.JuvoEventEmitter = new NativeEventEmitter(this.JuvoPlayer);
    this.JuvoEventEmitter.addListener("onTVKeyDown", this.onTVKeyDown);
    console.debug('RenderScene.componentDidMount(): done');
  }

  componentWillUnmount() 
  {
    console.debug('RenderScene.componentWillUnmount():');
    
    this.JuvoEventEmitter.removeAllListeners('onTVKeyDown');
    delete this.JuvoEventEmitter;

    console.debug('RenderScene.componentWillUnmount(): done');
  }
  
  onTVKeyDown(pressed) {
    const main = this.mainView;
    const modal = this.modalView;

    let targetView = null;

    if( modal.name != RenderView.viewNone.name && modal.usesKeys)
    {
      targetView = modal.name;
    }
    else if( main.name != RenderView.viewNone.name && main.usesKeys)
    {
      targetView = main.name;
    }
    else
    {
      console.debug(`RenderScene.onTVKeyDown(): key '${pressed.KeyName}' ignored. No views willing to consume keys.`);
      return;
    }

    const targetEvent = targetView + '/onTVKeyDown';
    console.debug(`RenderScene.onTVKeyDown(): Routing key '${pressed.KeyName}' to '${targetEvent}'`);
    DeviceEventEmitter.emit(targetEvent, pressed);
  }

  currentScene()
  {
    const currentlyShowing = Object.freeze({
      mainView: this.mainView,
      modalView: this.modalView,
    });

    return currentlyShowing;
  }
  
  displayScene(scene)
  {
    // to the contrary of "logic" described in ContentScroll.scrollToPosition()...
    // this.. works when operated opposite or so way.. voices in my head say..
    setImmediate( (s) =>
    {
      this.setState(s);
      console.debug('RenderScene.displayScene(): done');
    }, scene);
  }

  composeMain(view)
  {
    if(view.name == RenderView.viewNone.name && this.mainView.name == RenderView.viewNone.name)
    {
      console.debug('RenderScene.composeMain(): no update');
      return null;
    }

    const mainComponent = view.getComponent(view.args);
    this.mainView = view;

    return mainComponent;
  }

  composeModal(view)
  {
    if(view.name == RenderView.viewNone.name && this.modalView.name == RenderView.viewNone.name)
    {
      console.debug('RenderScene.composeModal(): no update');
      return null;
    }

    let modalComponent = null;

    // Transition from view to viewNone (hide without displaying new view)
    if(view.name == RenderView.viewNone.name)
    {
      // Fade view into oblivion
      const fadingModal = this.modalView;
      fadingModal.args = {...fadingModal.args, fadeAway: true };
      modalComponent = fadingModal.getComponent(fadingModal.args);

      // Fading view will have:
      //  state.modal = fadingComponent
      //  this.modalView = viewNone
    }
    else
    {
      // Replacing modal with another modal. No need for "special" removals.
      // new modal will replace existing modal removing from DOM. 
      modalComponent = view.getComponent(view.args);
    }

    this.modalView = view;
    console.debug('RenderScene.composeModal(): updated');
    return modalComponent;
  }

  compose(nextMainView,nextModalView)
  {
    try
    {
      console.debug(`RenderScene.compose():\n\tmain ${this.mainView.name} -> ${nextMainView.name}\n\tmodal ${this.modalView.name} -> ${nextModalView.name}`);

      // This is a "wokraround" for content scroll's content picture overlay icon.
      // In scenarios, not yet identified, scrollTo() invoked and completed yields no visual result.
      // Don't bypass "no view change" scenario: "Once rendered, need do nothing more". Re-render if present.
      // Seems to help reflect "bakcground" on screen changes.
      const nextScene = {
        main: nextMainView.name != RenderView.viewCurrent.name
                ? this.composeMain(nextMainView)
                : this.state.main,
        modal: nextModalView.name != RenderView.viewCurrent.name
                ? this.composeModal(nextModalView)
                : this.state.modal
      };
      // fading components will be reported as "current" till cleared.

      this.displayScene(nextScene);
      console.debug(`RenderScene.compose(): done\nmain:\n${Debug.stringify(this.mainView)}\nmodal:\n${Debug.stringify(this.modalView)}`);
    }
    catch(error)
    {
      console.error(`RenderScene.compose(): ERROR '${error}'`);
    }
  }

  removeHiddenViews()
  {
    console.debug('RenderScene.removeHiddenViews():');
    const removeModal = this.state.modal != null && this.modalView.name == RenderView.viewNone.name;
    const removeMain = this.state.main != null && this.mainView.name == RenderView.viewNone.name;
    
    let nextScene = {};
    
    if(removeModal)
      nextScene = {modal: this.modalView.getComponent(this.modalView.args)};
    
    if(removeMain)
      nextScene = {...nextScene, main: this.mainView.getComponent(this.mainView.args)};
    
    this.displayScene(nextScene);

    console.log(`RenderScene.removeHiddenViews(): done. removed main '${removeMain}' modal '${removeModal}'`);
  }

  render()
  {
    try
    {
      console.debug('RenderScene.render():');

      const renderMain = this.state.main;
      const renderModal = this.state.modal;
      
      console.debug(`RenderScene.render(): done. main '${renderMain != null}' modal '${renderModal != null}'`);
      return (
        <View style={styles.page}>
          {renderMain && renderMain}
          {renderModal && renderModal}
        </View>
      );
    }
    catch(error)
    {
      console.error(`'RenderScene.render(): ERROR ${error}`);
    }
  }
}

RenderScene.propTypes = {
  initialScene: PropTypes.object,
};

const styles = StyleSheet.create({
  page: 
  {
    height: height,
    width: width,
    position: 'absolute',
    backgroundColor: 'transparent',
  },
  modalPage: 
  {
    height: height,
    width: width,
    position: 'absolute',
    backgroundColor: 'transparent',
  },
});