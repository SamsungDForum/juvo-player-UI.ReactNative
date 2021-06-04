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
        private IDisposable playerStateChangeSub;
        private IDisposable playbackErrorsSub;
        private IDisposable bufferingProgressSub;
        private IDisposable deepLinkSub;
        private IDisposable eosSub;
        private IDeepLinkSender deepLinkSender;
        private readonly SynchronizationContext mainSynchronizationContext;

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
        private void OnPlayerStateChanged(PlayerState state)
        {
            var stateStr = state.ToString();
            Logger.Info(stateStr);

            int interval = 100;
            switch (state)
            {
                case PlayerState.Ready:
                    playbackTimer.Change(0, interval); //resume progress info update
                    break;

                // "Stop" clears active Player preventing dispatch of extra stop calls.
                case PlayerState.Idle:
                    playbackTimer.Change(Timeout.Infinite, Timeout.Infinite); //suspend progress info update
                    break;
            }

            var param = new JObject();
            param.Add("State", stateStr);
            SendEvent("onPlayerStateChanged", param);

            Logger.LogExit(stateStr);
        }

        private void OnPlaybackCompleted()
        {
            Logger.Info("");
            var param = new JObject();
            param.Add("State", "Completed");
            SendEvent("onPlaybackCompleted", param);
        }

        private void DisposePlayerSubscribers()
        {
            Logger.LogEnter();

            playerStateChangeSub.Dispose();
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
                WaitHandle.WaitAll(new[] { ((IAsyncResult)Player.Resume()).AsyncWaitHandle });

            Logger.LogExit();
        }

        public void OnSuspend()
        {
            bool havePlayer = Player != null;
            Logger.LogEnter($"Have player: {havePlayer}");

            if (havePlayer)
                WaitHandle.WaitAll(new[] { ((IAsyncResult)Player.Suspend()).AsyncWaitHandle });

            Logger.LogExit();
        }

        private void UpdateBufferingProgress(int percent)
        {
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("Percent", (int)percent);
            SendEvent("onUpdateBufferingProgress", param);
        }
        private void UpdatePlayTime(object timerState)
        {
            string txt = "";
            if (Player?.CurrentCueText != null)
            {
                txt = Player?.CurrentCueText;
            }
            var param = new JObject();
            param.Add("Total", (int)seekLogic.Duration.TotalMilliseconds);
            param.Add("Current", (int)seekLogic.CurrentPositionUI.TotalMilliseconds);
            param.Add("SubtiteText", txt);
            SendEvent("onUpdatePlayTime", param);
        }

        //////////////////JS methods//////////////////
        [ReactMethod]
        public async void PlayerStart(IPromise promise)
        {
            Logger.LogEnter();

            await Player.Start().ConfigureAwait(false);
            promise.Resolve(default);

            Logger.LogExit();
        }

        [ReactMethod]
        public async void GetStreamsDescription(int StreamTypeIndex, IPromise promise)
        {
            JuvoPlayer.Common.StreamType streamType;

            Logger.LogEnter($"{streamType = (JuvoPlayer.Common.StreamType)StreamTypeIndex}");

            if (streamType == JuvoPlayer.Common.StreamType.Subtitle)
            {
                this.allStreamsDescriptions[StreamTypeIndex] = new List<StreamDescription>
                {
                    new StreamDescription
                    {
                        Default = true,
                        Description = "off",
                        Id = "0",
                        StreamType = streamType
                    }
                };

                this.allStreamsDescriptions[StreamTypeIndex].AddRange(
                    await Player.GetStreamsDescription(streamType).ConfigureAwait(false));
            }
            else
            {
                this.allStreamsDescriptions[StreamTypeIndex] =
                    await Player.GetStreamsDescription(streamType).ConfigureAwait(false);
            }

            var param = new JObject();
            param.Add("Description", Newtonsoft.Json.JsonConvert.SerializeObject(this.allStreamsDescriptions[StreamTypeIndex]));
            param.Add("StreamTypeIndex", StreamTypeIndex);
            promise.Resolve(param);

            Logger.LogExit($"{streamType}");
        }

        [ReactMethod]
        public async void SetStream(int SelectedIndex, int StreamTypeIndex, IPromise promise)
        {
            JuvoPlayer.Common.StreamType streamType;
            bool haveStreamData = this.allStreamsDescriptions[StreamTypeIndex] != null && SelectedIndex != -1;

            Logger.LogEnter($"{streamType = (JuvoPlayer.Common.StreamType)StreamTypeIndex} Have data: {haveStreamData}");

            if (haveStreamData)
            {
                if (streamType == JuvoPlayer.Common.StreamType.Subtitle && SelectedIndex == 0)
                {
                    Player.DeactivateStream(StreamType.Subtitle);
                }
                else
                {
                    await Player
                        .ChangeActiveStream(this.allStreamsDescriptions[StreamTypeIndex][SelectedIndex])
                        .ConfigureAwait(false);
                }
            }

            promise.Resolve(default);
            Logger.LogExit();
        }

        [ReactMethod]
        public void Log(string message)
        {
            Logger?.Info(message);
        }

        [ReactMethod]
        public void InitialisePlayback()
        {
            Logger.LogEnter();
            
            playbackTimer = new Timer(
                callback: new TimerCallback(UpdatePlayTime),
                state: seekLogic.CurrentPositionUI,
                Timeout.Infinite, Timeout.Infinite);

            seekCompletedSub = seekLogic.SeekCompleted().Subscribe(message =>
            {
                var param = new JObject();
                SendEvent("onSeekCompleted", param);
            });

            Player = new PlayerService.PlayerService();

            bufferingProgressSub = Player.BufferingProgress().Subscribe(UpdateBufferingProgress);
            eosSub = Player.EndOfStream().Subscribe(_ => SendEvent("onEndOfStream", new JObject()));
            playbackErrorsSub = Player.PlaybackError()
                .Subscribe(message =>
                {
                    var param = new JObject();
                    param.Add("Message", message);
                    SendEvent("onPlaybackError", param);
                });
            playerStateChangeSub = Player.StateChanged().Subscribe(OnPlayerStateChanged, OnPlaybackCompleted);

            Logger.LogExit();
        }

        [ReactMethod]
        public async void StartPlayback(string videoURI, string drmDatasJSON, string streamingProtocol, IPromise promise)
        {
            async Task StartPlaybackInternal(string uri, List<DrmDescription> drm, string protocol)
            {
                Logger.Info();

                Player.SetWindow(ReactProgram.RctWindow);

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
                }
            }

            Logger.LogEnter();

            if (videoURI != null)
            {
                await StartPlaybackInternal(
                        videoURI,
                        (drmDatasJSON != null)
                            ? JSONFileReader.DeserializeJsonText<List<DrmDescription>>(drmDatasJSON)
                            : new List<DrmDescription>(),
                        streamingProtocol)
                    .ConfigureAwait(false);
            }
            else
            {
                Logger.Error("No stream");
            }

            promise.Resolve(default);

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
            Logger.LogEnter();

            if (Player != null)
            {
                Logger.Info($"Player state: {Player.State}");

                Task pauseResumeTask;
                switch (Player.State)
                {
                    case JuvoPlayer.Common.PlayerState.Playing:
                        pauseResumeTask = Player.Pause();
                        break;

                    case JuvoPlayer.Common.PlayerState.Paused:
                        pauseResumeTask = Player.Start();
                        break;

                    default:
                        pauseResumeTask = Task.CompletedTask;
                        break;
                }

                await pauseResumeTask.ConfigureAwait(false);
            }

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
