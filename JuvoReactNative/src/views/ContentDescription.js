'use strict';
import React, { Component } from 'react';
import { View, Text, StyleSheet } from 'react-native';

export default class ContentDescription extends Component 
{
  constructor(props) 
  {
    super(props);
  }

  shouldComponentUpdate(nextProps, nextState)
  {
    const updateRequired =  nextProps.headerText != this.props.headerText ||
                            nextProps.bodyText != this.props.bodyText;
    console.debug(`ContentDescription.shouldComponentUpdate(): ${updateRequired}`);
    return updateRequired;
  }

  render() {
    const header = this.props.headerText;
    const body = this.props.bodyText;

    console.log('ContentDescription.render(): done');
    return (
      <View style={styles.contentDescription} >
        <Text style={styles.contentHeader}>{header}</Text>
        <Text style={styles.contentBody}>{body}</Text>
      </View>
    );
  }
}

const styles = StyleSheet.create({
  contentDescription: {
    position: 'absolute',
    top: '10%',
    left: '5%',
    width: 900,
    height: 750,
    backgroundColor: 'transparent',
  },
  contentHeader: { 
    fontSize: 60, 
    color: '#ffffff', 
    
  },
  contentBody: {
    fontSize: 30,
    color: '#ffffff',
    top: 0,
  }
});