'use strict';
import React, { Component } from 'react';

import HideableView from './HideableView';
import InProgressView from './InProgressView';
import ContentCatalog from './ContentCatalog';
import PlaybackView from './PlaybackView';
import NotificationPopup from './NotificationPopup';
import StreamSelectionView from './StreamSelectionView';

const RenderView = {
    viewInProgress: 
    {
      name: 'InProgressView',
      args: null,
      getComponent: (props) => { return ( <InProgressView {...props} /> );},
      usesKeys: false,
    },
    viewContentCatalog:
    {
      name: 'ContentCatalog',
      args: null,
      getComponent: (props) => { return ( <ContentCatalog {...props} /> );},
      usesKeys: true,
    },
    viewPlayback: 
    {
      name: 'PlaybackView',
      args: null,
      getComponent: (props) => { return ( <PlaybackView {...props} /> );},
      usesKeys: true,
    },
    viewPopup: 
    {
      name: 'NotificationPopup',
      args: null,
      getComponent: (props) => { return ( <NotificationPopup {...props} /> );},
      usesKeys: true,
    },
    viewStreamSelection: 
    {
      name:'StreamSelectionView',
      args: null,
      getComponent: (props) => { return (<StreamSelectionView {...props} />);},
      usesKeys: true,
    },
    viewNone: {
      name: 'no view',
      args: null,
      getComponent: (_) => { return null;},
      usesKeys: false,
    },
    viewCurrent: {
      name: 'use curent',
      args: null,
      getComponent: null,
      usesKeys: false,
    },
  };

export default RenderView;