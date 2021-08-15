'use strict';
import React, {Component} from 'react';
import { Text, ActivityIndicator,StyleSheet, Dimensions} from 'react-native';
import PropTypes from 'prop-types'

import FadableView from './FadableView';

const width = Dimensions.get('window').width;
const height = Dimensions.get('window').height;

export default class InProgressView extends Component {

  static defaultProps =
    {
      messageText: 'Patience is a virtue, Boy!',
      fadeAway: false,
    };

  constructor(props) {
    super(props);
  }
  
  render() {
    try
    {
      console.log(`InProgressView.render(): done fadeAway '${this.props.fadeAway}' msg '${this.props.messageText}'`);

      return (
        <FadableView style={styles.notification} fadeAway={this.props.fadeAway} duration={300} nameTag='InProgress' >
          <ActivityIndicator style={{ left: 0, top: -10 }} size='large' color='#00ff00' />
          <Text style={{ left: 0, top: 10, color: '#00ff00', fontSize: 18, fontWeight: 'bold' }}>{this.props.messageText}</Text> 
        </FadableView>
      );
    }
    catch(error)
    {
      console.debug(`InProgressView.render(): ERROR ${error}`);
    }
  }
}

InProgressView.propTypes = {
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
  }
});