const Native = {
  JuvoPlayer: {
    Common: {
      StreamType: {
        Unknown: 0,
        Audio: 1,
        Video: 2,
        Subtitle: 3,
        Count: 4,
      }      
    },
    PlaybackState: {
      None: 'None',
      Idle: 'Idle',
      Ready: 'Ready',
      Paused: 'Paused',
      Playing: 'Playing',
    }
  }
};
export default Native;
