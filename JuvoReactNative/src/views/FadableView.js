'use strict';
import React, { Component } from 'react';
import { Animated } from 'react-native';
import PropTypes from 'prop-types'
import RenderScene, { RenderView } from './RenderScene';


export default class FadableView extends Component 
{
  static defaultProps =
  {
    // opacity: ['hidden', 'visible' ] range
    opacityRange: [0,1],
    duration: 500,
    
    onFadeIn: null,
    onFadeOut: null,
    removeOnFadeOut: true,
    fadeAway: false,

    nameTag: null,
  };
  
  constructor(props) 
  {
    super(props);
    this.state = { fadeAnim: props.duration > 0 ? new Animated.Value(props.opacityRange[0]) : 0, };

    this.runningAnimation = null;
    this.fadeIn = this.fadeIn.bind(this);
    this.fadeOut = this.fadeOut.bind(this);
    this.fade = this.fade.bind(this);
    this.onFadeComplete = this.onFadeComplete.bind(this);
  }

  componentDidMount()
  {
    if(this.props.nameTag)
      console.debug(`FadableView.componentDidMount(): ${this.props.nameTag}`);

    this.fadeIn();

    if(this.props.nameTag)
      console.debug(`FadableView.componentDidMount(): ${this.props.nameTag} done`);
  }

  componentWillUnmount()
  {
    if(this.props.nameTag)
      console.debug(`FadableView.componentWillUnmount(): ${this.props.nameTag}`);
    
    // terminating running animation will invoke provided handlers.
    if(this.runningAnimation)
      this.runningAnimation.stop();

    if(this.duration > 0)
      delete this.state.fadeAnim;
    
    if(this.props.nameTag)
      console.debug(`FadableView.componentWillUnmount(): ${this.props.nameTag} done`);
  }

  fadeIn() 
  {
    this.fade(this.props.opacityRange[1], this.props.duration, this.props.onFadeIn, false);
  }
  
  fadeOut() 
  {
    this.fade( this.props.opacityRange[0], this.props.duration, this.props.onFadeOut,this.props.removeOnFadeOut);
  }
  
  
  onFadeComplete(handler, removeWhenCompleted)
  {
    if(this.props.nameTag)
      console.debug(`FadableView.onFadeComplete(): ${this.props.nameTag}`);

    try
    {
      if(handler) 
      {
        if(this.props.nameTag)
          console.log(`FadableView.onFadeComplete(): ${this.props.nameTag} invoking provided handler`);
        
        handler();
      }
      else
      {
        if(this.props.nameTag)
          console.debug(`FadableView.onFadeComplete(): ${this.props.nameTag} handler not provided.`);
      }
    }
    catch(error)
    {
      console.warn(`FadableView.onFadeComplete(): '${this.props.nameTag&&this.props.nameTag}' handler invoke failed.`);
    }
  
    if(removeWhenCompleted)
      RenderScene.removeHiddenViews();

    if(this.props.nameTag)
      console.debug(`FadableView.onFadeComplete(): ${this.props.nameTag} done. Removed ${removeWhenCompleted}`);
  }

  fade(target, speed, onComplete, removeWhenCompleted)
  {
    if(this.runningAnimation)
    {
      this.runningAnimation.stop();
      this.runningAnimation = null;
      console.log(`FadableView.fade(): '${this.props.nameTag&&this.props.nameTag}' running animation stopped.`);
    }

    const logTag = this.props.nameTag != null 
      ? `FadableView.fade(): ${this.props.nameTag} -> opacity '${target}' @'${speed}' ms`
      : null;

    if(logTag)
      console.debug(logTag);

    if(speed > 0) 
    {
      this.runningAnimation = Animated.timing(this.state.fadeAnim, {toValue: target, duration: speed});
      this.runningAnimation.start(({finished}) =>
      {
        this.runningAnimation = null;
        if(logTag)
          console.debug(`${logTag} animation completed '${finished}'`);
        
        this.onFadeComplete(onComplete,removeWhenCompleted);
      });
    }
    else
    {
      this.setState({fadeAnim: target});

      if(logTag)
        console.debug(`${logTag} no animation`);
      
      this.onFadeComplete(onComplete,removeWhenCompleted);
    }

    if(logTag)
      console.debug(`${logTag} done`);
  }
  
  componentDidUpdate(prevProps, prevState) 
  {
    if(this.props.fadeAway != prevProps.fadeAway)
    {
      console.log(`FadableView.componentDidUpdate(): ${this.props.nameTag&&this.props.nameTag} fadeAway ${prevProps.fadeAway} -> ${this.props.fadeAway} remove ${this.props.removeOnFadeOut}`);

      if(this.props.fadeAway)
        this.fadeOut();
      else
        this.fadeIn();
    }
  }

  render() 
  {
    try
    {
      const useStyle=[ this.props.style, {opacity: this.state.fadeAnim }];

      if (this.props.nameTag)
        console.debug(`FadableView.render(): ${this.props.nameTag} done. fadeAway '${this.props.fadeAway}'`);

      return ( <Animated.View style={useStyle}>{this.props.children}</Animated.View> );
    }
    catch(error)
    {
      console.error(`FadableView.render(): ${this.props.nameTag && this.props.nameTag} ERROR '${error}'`);
    }
  }
}
  

FadableView.propTypes = {
  opacityRange: PropTypes.arrayOf(PropTypes.number),
  duration: PropTypes.number,

  onFadeIn: PropTypes.func,
  onFadeOut: PropTypes.func,
  removeOnFadeOut: PropTypes.bool,
  fadeAway: PropTypes.bool,

  nameTag: PropTypes.string,
};
