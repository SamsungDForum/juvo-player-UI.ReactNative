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
using System.Linq;
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
        private Timer _playbackTimer;
        private readonly SeekLogic _seekLogic = null; // needs to be initialized in the constructor!

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");

        private EcoreEvent<EcoreKeyEventArgs> _keyDown;
        private EcoreEvent<EcoreKeyEventArgs> _keyUp;

        // assumes StreamType values are sequential [0..N]
        private readonly List<StreamDescription>[] _allStreamsDescriptions = new List<StreamDescription>[Enum.GetValues(typeof(StreamType)).Length];
        public IPlayerService Player { get; private set; }
        private IDisposable _seekCompletedSub;
        private IDisposable _playbackErrorsSub;
        private IDisposable _bufferingProgressSub;
        private IDisposable _deepLinkSub;
        private IDisposable _eosSub;
        private readonly IDeepLinkSender _deepLinkSender;
        private readonly SynchronizationContext _mainSynchronizationContext;
        private static readonly TimeSpan TimedDataUpdateInterval = TimeSpan.FromMilliseconds(100);

        public JuvoPlayerModule(ReactContext reactContext, IDeepLinkSender deepLinkSender,
            SynchronizationContext mainSynchronizationContext)
            : base(reactContext)
        {
            _seekLogic = new SeekLogic(this);
            _deepLinkSender = deepLinkSender;
            _mainSynchronizationContext = mainSynchronizationContext;
        }

        private void OnDeepLinkReceived(string url)
        {
            SendEvent("handleDeepLink", new JObject { { "url", url } });
        }

        public override string Name => "JuvoPlayer";

        private void SendEvent(string eventName, JObject parameters)
        {
            Context.GetJavaScriptModule<RCTDeviceEventEmitter>().emit(eventName, parameters);
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

            _playbackTimer.Change(TimeSpan.Zero, TimedDataUpdateInterval); //resume update

            Logger.LogExit();
        }

        private void SuspendTimedDataUpdate()
        {
            Logger.LogEnter();

            _playbackTimer.Change(Timeout.Infinite, Timeout.Infinite); //suspend update
            UpdateTimedData(); // Push out current (last known) timed data

            Logger.LogExit();
        }

        private void DisposePlayerSubscribers()
        {
            Logger.LogEnter();

            _playbackErrorsSub.Dispose();
            _bufferingProgressSub.Dispose();
            _eosSub.Dispose();

            Logger.LogExit();
        }

        public void OnDestroy()
        {
            Logger.Info("Destroying JuvoPlayerModule...");
            DisposePlayerSubscribers();
            _seekCompletedSub.Dispose();
            _deepLinkSub.Dispose();
            _playbackTimer.Dispose();
            _playbackTimer = null;
            Player.Dispose();
            Player = null;
        }

        public void OnResume()
        {
            bool havePlayer = Player != null;
            Logger.LogEnter($"Have player: {havePlayer}");

            if (havePlayer)
            {
                Context.RunOnNativeModulesQueueThread(async () =>
                {
                    try
                    {
                        await Player.Resume();
                        ResumeTimedDataUpdate();
                    }
                    catch
                    {
                        // Ignore. Errors are reported by Resume().
                    }
                });
            }

            Logger.LogExit();
        }

        public void OnSuspend()
        {
            bool havePlayer = Player != null;
            Logger.LogEnter($"Have player: {havePlayer}");

            if (havePlayer)
            {
                Context.RunOnNativeModulesQueueThread(async () =>
                {
                    _seekLogic.Reset();
                    SuspendTimedDataUpdate();
                    await Player.Suspend();
                });
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
            param.Add("Total", (int)_seekLogic.Duration.TotalMilliseconds);
            param.Add("Current", (int)_seekLogic.CurrentPositionUI.TotalMilliseconds);
            param.Add("SubtiteText", txt);
            SendEvent("onUpdatePlayTime", param);
        }

        private void InitialisePlayback()
        {
            Logger.LogEnter();

            _playbackTimer = new Timer(UpdateTimedData, default, Timeout.Infinite, Timeout.Infinite);
            _seekCompletedSub = _seekLogic.SeekCompleted().Subscribe(_ => SendEvent("onSeekCompleted", new JObject()));

            Player = new PlayerService.PlayerService();
            Player.SetWindow(ReactProgram.RctWindow);

            _bufferingProgressSub = Player.BufferingProgress().Subscribe(UpdateBufferingProgress);
            _eosSub = Player.EndOfStream().Subscribe(_ => SendEvent("onEndOfStream", new JObject()));
            _playbackErrorsSub = Player.PlaybackError()
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
        public async void GetStreamsDescription(int streamTypeIndex, IPromise promise)
        {
            async Task<List<StreamDescription>> GetStreamsDescriptionInternal(int streamIndex, StreamType streamType)
            {
                Logger.Info($"{streamType}");

                if (streamType == StreamType.Subtitle)
                {
                    _allStreamsDescriptions[streamIndex] = new List<StreamDescription>
                    {
                        new StreamDescription
                        {
                            Default = true,
                            Description = "off",
                            Id = "0",
                            StreamType = streamType
                        }
                    };

                    _allStreamsDescriptions[streamIndex].AddRange(await Player.GetStreamsDescription(streamType));
                }
                else
                {
                    _allStreamsDescriptions[streamIndex] = await Player.GetStreamsDescription(streamType);
                }

                return _allStreamsDescriptions[streamIndex];
            }


            Logger.LogEnter();

            try
            {
                var param = new JObject();
                param.Add("Description", Newtonsoft.Json.JsonConvert.SerializeObject(
                    await GetStreamsDescriptionInternal(streamTypeIndex, (StreamType)streamTypeIndex)
                        .ConfigureAwait(false)));
                param.Add("StreamTypeIndex", streamTypeIndex);
                promise.Resolve(param);
            }
            catch (Exception e)
            {
                promise.Reject(e.Message, e);
            }

            Logger.LogExit();
        }

        [ReactMethod]
        public async void SetStream(int selectionIndex, int streamTypeIndex, IPromise promise)
        {
            async Task<PlayerState> SetStreamInternal(int selectedIndex, int streamIndex)
            {
                bool haveStreamData = _allStreamsDescriptions[streamTypeIndex] != null && selectedIndex != -1;
                StreamType streamType = (StreamType)streamIndex;

                Logger.Info($"{streamType} Have data: {haveStreamData}");

                if (haveStreamData)
                {
                    if (streamType == StreamType.Subtitle && selectedIndex == 0)
                        Player.DeactivateStream(StreamType.Subtitle);
                    else
                        await Player.ChangeActiveStream(this._allStreamsDescriptions[streamIndex][selectedIndex]);
                }

                return Player.State;
            }

            Logger.LogEnter();

            try
            {
                var playerState = await SetStreamInternal(selectionIndex, streamTypeIndex).ConfigureAwait(false);
                promise.Resolve(playerState.ToString());
            }
            catch (Exception e)
            {
                promise.Reject(e.Message, e);
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
            async Task StartPlaybackInternal(string uri, List<DrmDescription> drm, string protocol)
            {
                InitialisePlayback();

                await Player.SetSource(new ClipDefinition
                {
                    Type = protocol,
                    Url = uri,
                    Subtitles = new List<SubtitleInfo>(),
                    DRMDatas = drm
                });

                await Player.Start();
                ResumeTimedDataUpdate();
            }

            Logger.LogEnter();

            if (string.IsNullOrWhiteSpace(videoURI))
            {
                promise.Reject("empty URI", new ArgumentException());
            }
            else if (string.IsNullOrWhiteSpace(streamingProtocol))
            {
                promise.Reject("empty protocol", new ArgumentException());
            }
            else
            {
                try
                {
                    var drmList = string.IsNullOrWhiteSpace(drmDatasJSON)
                        ? Enumerable.Empty<DrmDescription>().ToList()
                        : JSONFileReader.DeserializeJsonText<List<DrmDescription>>(drmDatasJSON);

                    await StartPlaybackInternal(videoURI, drmList, streamingProtocol).ConfigureAwait(false);
                    promise.Resolve(Player.State.ToString());
                }
                catch (Exception e)
                {
                    promise.Reject(e.Message, e);
                }
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
                Task pauseResumeTask;

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

            try
            {
                var playerState = await PauseResumePlaybackInternal().ConfigureAwait(false);
                promise.Resolve(playerState.ToString());
            }
            catch (Exception e)
            {
                promise.Reject(e.Message, e);
            }

            Logger.LogExit();
        }

        [ReactMethod]
        public void Forward()
        {
            _seekLogic.SeekForward();
        }

        [ReactMethod]
        public void Rewind()
        {
            _seekLogic.SeekBackward();
        }

        [ReactMethod]
        public void AttachDeepLinkListener()
        {
            if (_deepLinkSub == null)
                _deepLinkSub = _deepLinkSender.DeepLinkReceived().Subscribe(OnDeepLinkReceived);
        }

        [ReactMethod]
        public void ExitApp()
        {
            _mainSynchronizationContext.Post(_ =>
            {
                Application.Current.Exit();
            }, null);
        }
    }
}
