'use strict';
import React from 'react';
import { View, Text, ActivityIndicator,StyleSheet, } from 'react-native';

import HideableView from './HideableView';

const styles = StyleSheet.create({
  notification: {
    height: "100%", 
    justifyContent: "center", 
    alignItems: "center", 
    backgroundColor: "black"
  }});

export default class InProgressView extends React.Component {
  constructor(props) {
    super(props);
  }
  
  render() {
    const fadeduration = 300;
    const isVisible = this.props.visible;
    const message = this.props.messageText;

    console.debug(`InProgressView.render(): visible ${isVisible}`)
    return (
      <View style={[styles.notification]}>
        <View style={{ width: 200, height: 200 }}>
          <HideableView visible={isVisible} duration={fadeduration}>
            <View style={{ width: '100%', height: '100%', justifyContent: 'center', alignItems: 'center', backgroundColor: '#000000', opacity: 0.6 }}>
              <ActivityIndicator style={{ left: 0, top: -10 }} size='large' color='#00ff00' />
              <Text style={{ left: 0, top: 10, color: '#00ff00', fontSize: 18, fontWeight: 'bold' }}>{message}</Text>
            </View>
          </HideableView>
        </View>
      </View>      
    );
  }
}
