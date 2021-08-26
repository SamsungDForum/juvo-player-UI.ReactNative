'use strict';
import React, { Component, PropTypes } from 'react';
import { Animated } from 'react-native';


export default class HideableView extends Component {

  static defaultProps =
  {
    duration: 500,
    removeWhenHidden: false,
    noAnimation: false,
  };

  constructor(props) {
    super(props);
    this.state = {
      opacity: new Animated.Value(this.props.visible ? 1 : 0)
    };
  }

  animate(show) {
    const duration = this.props.duration ? parseInt(this.props.duration) : 500;
    Animated.timing(this.state.opacity, {
      toValue: show ? 1 : 0,
      duration: !this.props.noAnimation ? duration : 0
    })
    .start(({ finished }) => {
      console.debug(`HideableView.animate(): animation completed: ${finished}`);
     });
  }

  componentDidMount()
  {
    if(this.props.nameTag)
      console.debug(`HideableView.componentDidMount(): done ${this.props.nameTag}`);
  } 

  componentWillUnmount()
  {
    delete this.state.opacity;
    if(this.props.nameTag)
      console.debug(`HideableView.componentWillUnmount(): done ${this.props.nameTag}`);
  }

  render() 
  {
    try
    {

      if (this.props.removeWhenHidden && !this.visible) 
      {
        if(this.props.nameTag)
          console.debug(`HideableView.render(): removing ${this.props.nameTag}`);

        return null;
      }

      const useStyle=[ this.props.style, {opacity: this.state.opacity }];

      if(this.props.nameTag)
        console.debug(`HideableView.render(): done ${this.props.nameTag}`);
       
      return ( <Animated.View style={useStyle}>{this.props.children}</Animated.View> );
    }
    catch(error)
    {
      console.error(`HidableView.render(): '${this.props.nameTag}' ${error}`);
    }
  }
}

HideableView.propTypes = {
  visible: PropTypes.bool,
  duration: PropTypes.number,
  removeWhenHidden: PropTypes.bool,
  noAnimation: PropTypes.bool
};