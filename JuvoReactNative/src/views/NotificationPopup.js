'use strict';
import React, { Component } from 'react';
import { View, Text, Dimensions,StyleSheet,DeviceEventEmitter } from 'react-native';
import PropTypes from 'prop-types'

import RenderScene from './RenderScene';
import RenderView from './RenderView';
import FadableView from './FadableView';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

export default class NotificationPopup extends Component 
{
  static defaultProps =
  {
    messageText: `Ain't saying a word without my lawey!`,
    fadeAway: false,
  };

  constructor(props) 
  {
    super(props);
    this.onTVKeyDown = this.onTVKeyDown.bind(this);
  }
  
  componentDidMount() 
  {
    DeviceEventEmitter.addListener('NotificationPopup/onTVKeyDown', this.onTVKeyDown);
    console.debug(`NotificationPopup.componentDidMount(): done`);
  }

  componentWillUnmount() 
  { 
    DeviceEventEmitter.removeAllListeners('NotificationPopup/onTVKeyDown'); 
    console.debug(`NotificationPopup.componentWillUnmount(): done`);
  }
  
  onTVKeyDown(pressed) {
    console.debug(`NotificationPopup.onTVKeyDown(): '${pressed.KeyName}'`);
    switch (pressed.KeyName) {
      case 'Return':
      case 'XF86AudioPlay':
      case 'XF86PlayBack':
      case 'XF86Back':
      case 'XF86AudioStop':        
        RenderScene.setScene(RenderView.viewCurrent, RenderView.viewNone);
        break;

      default:
        console.debug(`NotificationPopup.onTVKeyDown(): key '${pressed.KeyName}' ignored`);
        return;
    }

    console.log(`NotificationPopup.onTVKeyDown(): key '${pressed.KeyName}' processed`);
  }

  render() 
  {
    return (
      <FadableView style={styles.notification} fadeAway={this.props.fadeAway} duration={300} nameTag='NotificationPopup' >
        <View style={styles.messageBox}>
          <Text style={styles.messageText}>{this.props.messageText}</Text>
          <Text style={styles.instructionText}>Press enter or return key to close</Text>
        </View>
      </FadableView>
    );
  }
}

NotificationPopup.propTypes = {
  messageText: PropTypes.string,
  fadeAway: PropTypes.bool,
};

const styles = StyleSheet.create({
  notification: {
    width: width,
    height: height,
    justifyContent: 'center', 
    alignItems: 'center', 
    backgroundColor: 'rgba(35, 35, 35, 0.5)',
  },
  messageBox: {
    width: 850,
    height: 430,
    justifyContent: 'space-around',
    alignItems: 'center',
    padding: 5,
    backgroundColor: 'rgba(255,255,255,0.8)',
  },
  messageText: {
    fontSize: 40,
    color: '#000000', 
    textAlign: 'center',
  },
  instructionText: {
    fontSize: 20,
    color: '#000000',
    textAlign: 'center',
  }
});