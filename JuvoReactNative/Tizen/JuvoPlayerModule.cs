/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2021, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReactNative;
using ReactNative.Bridge;
using JuvoPlayer.Common;
using JuvoLogger;
using ILogger = JuvoLogger.ILogger;
using ElmSharp;
using ReactNative.Modules.Core;
using Newtonsoft.Json.Linq;
using Tizen.Applications;
using UI.Common;
using static PlayerService.LogToolBox;

namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private Timer playbackTimer;
        private readonly SeekLogic seekLogic = null; // needs to be initialized in the constructor!

        private const string Tag = "JuvoRN";
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        EcoreEvent<EcoreKeyEventArgs> _keyDown;
        EcoreEvent<EcoreKeyEventArgs> _keyUp;

        // assumes StreamType values are sequential [0..N]
        List<StreamDescription>[] allStreamsDescriptions = new List<StreamDescription>[Enum.GetValues(typeof(StreamType)).Length];
        public IPlayerService Player { get; private set; }
        private IDisposable seekCompletedSub;
        private IDisposable playbackErrorsSub;
        private IDisposable bufferingProgressSub;
        private IDisposable deepLinkSub;
        private IDisposable eosSub;
        private IDeepLinkSender deepLinkSender;
        private readonly SynchronizationContext mainSynchronizationContext;
        private const int timedDataUpdateInterval = 100; //in ms

        public JuvoPlayerModule(ReactContext reactContext, IDeepLinkSender deepLinkSender,
            SynchronizationContext mainSynchronizationContext)
            : base(reactContext)
        {
            seekLogic = new SeekLogic(this);
            this.deepLinkSender = deepLinkSender;
            this.mainSynchronizationContext = mainSynchronizationContext;
        }

        private void OnDeepLinkReceived(string url)
        {
            SendEvent("handleDeepLink", new JObject { { "url", url } });
        }

        public override string Name
        {
            get
            {
                return "JuvoPlayer";
            }
        }

        private void SendEvent(string eventName, JObject parameters)
        {
            Context.GetJavaScriptModule<RCTDeviceEventEmitter>()
                .emit(eventName, parameters);
        }

        public override void Initialize()
        {
            Logger.LogEnter();

            Context.AddLifecycleEventListener(this);
            _keyDown = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyDown, EcoreKeyEventArgs.Create);
            _keyDown.On += (s, e) =>
            {
                //Propagate the key press event to JavaScript module
                var param = new JObject();
                param.Add("KeyName", e.KeyName);
                param.Add("KeyCode", e.KeyCode);
                SendEvent("onTVKeyDown", param);
            };
            _keyUp = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyUp, EcoreKeyEventArgs.Create);
            _keyUp.On += (s, e) =>
            {
                //Propagate the key press event to JavaScript module
                var param = new JObject();
                param.Add("KeyName", e.KeyName);
                param.Add("KeyCode", e.KeyCode);
                SendEvent("onTVKeyUp", param);
            };

            Logger.LogExit();
        }
        
        private void ResumeTimedDataUpdate()
        {
            Logger.LogEnter();

            playbackTimer.Change(0, timedDataUpdateInterval); //resume progress info update
            
            Logger.LogExit();
        }

        private void SuspendTimedDataUpdate()
        {
            Logger.LogEnter();

            playbackTimer.Change(Timeout.Infinite, Timeout.Infinite); //suspend progress info update
            UpdateTimedData(); // Push out current (last known) timed data

            Logger.LogExit();
        }
        
        private void DisposePlayerSubscribers()
        {
            Logger.LogEnter();

            playbackErrorsSub.Dispose();
            bufferingProgressSub.Dispose();
            eosSub.Dispose();

            Logger.LogExit();
        }

        public void OnDestroy()
        {
            Logger.Info("Destroying JuvoPlayerModule...");
            DisposePlayerSubscribers();
            seekCompletedSub.Dispose();
            deepLinkSub.Dispose();
            playbackTimer.Dispose();
            playbackTimer = null;
            Player.Dispose();
            Player = null;
        }

        public void OnResume()
        {
            bool havePlayer = Player != null;
            Logger.LogEnter($"Have player: {havePlayer}");

            if (havePlayer)
            {
                WaitHandle.WaitAll(new[] {((IAsyncResult) Player.Resume()).AsyncWaitHandle});
                ResumeTimedDataUpdate();
            }

            Logger.LogExit();
        }

        public void OnSuspend()
        {
            bool havePlayer = Player != null;
            Logger.LogEnter($"Have player: {havePlayer}");

            if (havePlayer)
            {
                SuspendTimedDataUpdate();
                WaitHandle.WaitAll(new[] {((IAsyncResult) Player.Suspend()).AsyncWaitHandle});
            }

            Logger.LogExit();
        }

        private void UpdateBufferingProgress(int percent)
        {
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("Percent", (int)percent);
            SendEvent("onUpdateBufferingProgress", param);
        }

        private void UpdateTimedData(object _ = default)
        {
            var txt = Player?.CurrentCueText ?? string.Empty;
            var param = new JObject();
            param.Add("Total", (int)seekLogic.Duration.TotalMilliseconds);
            param.Add("Current", (int)seekLogic.CurrentPositionUI.TotalMilliseconds);
            param.Add("SubtiteText", txt);
            SendEvent("onUpdatePlayTime", param);
        }

        private void InitialisePlayback()
        {
            Logger.LogEnter();

            playbackTimer = new Timer(
                callback: new TimerCallback(UpdateTimedData),
                default,
                Timeout.Infinite, Timeout.Infinite);

            seekCompletedSub = seekLogic.SeekCompleted().Subscribe(message =>
            {
                var param = new JObject();
                SendEvent("onSeekCompleted", param);
            });

            Player = new PlayerService.PlayerService();
            Player.SetWindow(ReactProgram.RctWindow);

            bufferingProgressSub = Player.BufferingProgress().Subscribe(UpdateBufferingProgress);
            eosSub = Player.EndOfStream().Subscribe(_ => SendEvent("onEndOfStream", new JObject()));
            playbackErrorsSub = Player.PlaybackError()
                .Subscribe(message =>
                {
                    var param = new JObject();
                    param.Add("Message", message);
                    SendEvent("onPlaybackError", param);
                });

            Logger.LogExit();
        }

        //////////////////JS methods//////////////////
        [ReactMethod]
        public async void GetStreamsDescription(int StreamTypeIndex, IPromise promise)
        {
            async Task<List<StreamDescription>> GetStreamsDescriptionInternal(int streamIndex, StreamType streamType)
            {
                Logger.Info($"{streamType}");

                if (streamType == JuvoPlayer.Common.StreamType.Subtitle)
                {
                    allStreamsDescriptions[streamIndex] = new List<StreamDescription>
                    {
                        new StreamDescription
                        {
                            Default = true,
                            Description = "off",
                            Id = "0",
                            StreamType = streamType
                        }
                    };

                    allStreamsDescriptions[streamIndex].AddRange(await Player.GetStreamsDescription(streamType));
                }
                else
                {
                    allStreamsDescriptions[streamIndex] = await Player.GetStreamsDescription(streamType);
                }

                return allStreamsDescriptions[streamIndex];
            }

            
            Logger.LogEnter();

            try
            {
                var param = new JObject();
                param.Add("Description", Newtonsoft.Json.JsonConvert.SerializeObject(
                    await GetStreamsDescriptionInternal(StreamTypeIndex, (StreamType) StreamTypeIndex)
                        .ConfigureAwait(false)));
                param.Add("StreamTypeIndex", StreamTypeIndex);
                promise.Resolve(param);
            }
            catch (Exception ex)
            {
                promise.Reject(string.Empty,ex);
            }

            Logger.LogExit();
        }

        [ReactMethod]
        public async void SetStream(int SelectedIndex, int StreamTypeIndex, IPromise promise)
        {
            async Task<PlayerState> SetStreamInternal(int selecteIndex, int streamIndex)
            {
                bool haveStreamData = allStreamsDescriptions[StreamTypeIndex] != null && selecteIndex != -1;
                StreamType streamType = (StreamType)streamIndex;

                Logger.Info($"{streamType} Have data: {haveStreamData}");

                if (haveStreamData)
                {
                    if (streamType == StreamType.Subtitle && selecteIndex == 0)
                        Player.DeactivateStream(StreamType.Subtitle);
                    else
                        await Player.ChangeActiveStream(this.allStreamsDescriptions[streamIndex][selecteIndex]);
                }

                return Player.State;
            }

            Logger.LogEnter();

            try
            {
                var playerState = await SetStreamInternal(SelectedIndex, StreamTypeIndex).ConfigureAwait(false);
                promise.Resolve(playerState.ToString());
            }
            catch (Exception ex)
            {
                promise.Reject(string.Empty,ex);
            }

            Logger.LogExit();
        }

        [ReactMethod]
        public void Log(string message)
        {
            Logger?.Info(message);
        }

        [ReactMethod]
        public async void StartPlayback(string videoURI, string drmDatasJSON, string streamingProtocol, IPromise promise)
        {
            async Task<PlayerState> StartPlaybackInternal(string uri, List<DrmDescription> drm, string protocol)
            {
                Logger.Info();
                
                InitialisePlayback();

                await Player.SetSource(new ClipDefinition
                {
                    Type = protocol,
                    Url = uri,
                    Subtitles = new List<SubtitleInfo>(),
                    DRMDatas = drm
                });

                Logger.Info($"Source set. Player state: {Player.State}");

                if (Player.State == PlayerState.Ready)
                {
                    seekLogic.Reset();
                    await Player.Start();

                    if (Player.State == PlayerState.Playing)
                        ResumeTimedDataUpdate();
                }

                return Player.State;
            }

            Logger.LogEnter();

            if (videoURI != null)
            {
                var playerState = await StartPlaybackInternal(
                        videoURI,
                        (drmDatasJSON != null)
                            ? JSONFileReader.DeserializeJsonText<List<DrmDescription>>(drmDatasJSON)
                            : new List<DrmDescription>(),
                        streamingProtocol)
                    .ConfigureAwait(false);

                promise.Resolve(playerState.ToString());
            }
            else
            {
                promise.Reject(string.Empty,new ArgumentNullException());
            }
            
            Logger.LogExit();
        }
        
        [ReactMethod]
        public void StopPlayback()
        {
            if (Player == null)
                return;

            Logger.Info();

            OnDestroy();
        }

        [ReactMethod]
        public async void PauseResumePlayback(IPromise promise)
        {
            async Task<PlayerState> PauseResumePlaybackInternal()
            {
                Logger.Info();

                Task pauseResumeTask;
                
                Logger.Info($"Player state: {Player.State}");

                switch (Player.State)
                {
                    case PlayerState.Playing:
                        pauseResumeTask = Player.Pause();
                        SuspendTimedDataUpdate();
                        break;

                    case PlayerState.Paused:
                        pauseResumeTask = Player.Start();
                        ResumeTimedDataUpdate();
                        break;

                    default:
                        pauseResumeTask = Task.CompletedTask;
                        break;
                }
                
                await pauseResumeTask;
                return Player.State;
            }

            Logger.LogEnter();

            var playerState = await PauseResumePlaybackInternal().ConfigureAwait(false);
            promise.Resolve(playerState.ToString());
            
            Logger.LogExit();
        }

        [ReactMethod]
        public void Forward()
        {
            seekLogic.SeekForward();
        }

        [ReactMethod]
        public void Rewind()
        {
            seekLogic.SeekBackward();
        }

        [ReactMethod]
        public void AttachDeepLinkListener()
        {
            if (deepLinkSub == null)
                deepLinkSub = deepLinkSender.DeepLinkReceived().Subscribe(OnDeepLinkReceived);
        }

        [ReactMethod]
        public void ExitApp()
        {
            mainSynchronizationContext.Post(_ =>
            {
                Application.Current.Exit();
            }, null);
        }
    }
}
