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
using System.Diagnostics;
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
using PlayerService;
using Tizen.Applications;
using UI.Common;

namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private Timer _playbackTimer;
        private readonly SeekLogic _seekLogic = null; // needs to be initialized in the constructor!

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");

        private EcoreEvent<EcoreKeyEventArgs> _keyDown;

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

        private void DumpContext()
        {
            Logger.Debug($"Dispatcher: {Context.IsOnDispatcherQueueThread()}");
            Logger.Debug($"Native: {Context.IsOnNativeModulesQueueThread()}");
            Logger.Debug($"JavaScript: {Context.IsOnJavaScriptQueueThread()}");
        }

        private void OnDeepLinkReceived(string url)
        {
            using (LogScope.Create(url))
                Context.RunOnNativeModulesQueueThread(() => SendEvent("handleDeepLink", new JObject { { "url", url } }));

        }

        private void OnDeepLinkClosed()
        {
            using (LogScope.Create())
            {
                Context.RunOnNativeModulesQueueThread(() =>
                {
                    _deepLinkSub?.Dispose();
                    _deepLinkSub = null;
                });
            }
        }

        public override string Name => "JuvoPlayer";

        private void SendEvent(string eventName, JObject parameters)
        {
            Debug.Assert(Context.IsOnNativeModulesQueueThread(), $"{eventName} not on native module thread.");
            Context.GetJavaScriptModule<RCTDeviceEventEmitter>().emit(eventName, parameters);
        }


        public override void Initialize()
        {
            using (LogScope.Create())
            {
                // Lifecycle events will be on dispatcher thread.
                Context.AddLifecycleEventListener(this);
                _keyDown = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyDown, EcoreKeyEventArgs.Create);
                _keyDown.On += (s, e) =>
                {
                    //Propagate the key press event to JavaScript module
                    var param = new JObject();
                    param.Add("KeyName", e.KeyName);
                    param.Add("KeyCode", e.KeyCode);
                    Context.RunOnNativeModulesQueueThread(() => SendEvent("onTVKeyDown", param));
                };
            }
        }

        private void ResumeTimedDataUpdate()
        {
            using (LogScope.Create())
                _playbackTimer.Change(TimeSpan.Zero, TimedDataUpdateInterval); //resume update
        }

        private void SuspendTimedDataUpdate()
        {
            using (LogScope.Create())
            {
                _playbackTimer.Change(Timeout.Infinite, Timeout.Infinite); //suspend update
                UpdateTimedData(); // Push out current (last known) timed data
            }
        }

        private void DisposePlayerSubscribers()
        {
            _playbackErrorsSub.Dispose();
            _bufferingProgressSub.Dispose();
            _eosSub.Dispose();
        }

        private void TerminatePlayerService()
        {
            bool havePlayer = Player != null;
            Logger.Info($"Have player: {havePlayer}");

            if (havePlayer)
            {
                DisposePlayerSubscribers();
                _seekCompletedSub.Dispose();
                _playbackTimer.Dispose();
                _playbackTimer = null;
                Player.Dispose();
                Player = null;
                Logger.Info("PlayerService kicked the bucket");
            }
        }

        void ILifecycleEventListener.OnDestroy()
        {
            Logger.Info("Unicorn event!");
        }

        void ILifecycleEventListener.OnResume()
        {
            using (LogScope.Create())
            {
                Context.RunOnNativeModulesQueueThread(async () =>
                {
                    bool havePlayer = Player != null;

                    Logger.Info($"Have player: {havePlayer}");
                    if (!havePlayer)
                        return;

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
        }

        void ILifecycleEventListener.OnSuspend()
        {
            using (LogScope.Create())
            {
                Context.RunOnNativeModulesQueueThread(async () =>
                {
                    bool havePlayer = Player != null;

                    Logger.Info($"Have player: {havePlayer}");
                    if (!havePlayer)
                        return;

                    _seekLogic.Reset();
                    SuspendTimedDataUpdate();
                    await Player.Suspend();
                });
            }
        }

        private void UpdateBufferingProgress(int percent)
        {
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("Percent", percent);
            Context.RunOnNativeModulesQueueThread(() => SendEvent("onUpdateBufferingProgress", param));
        }

        private void UpdateTimedData(object _ = default)
        {
            Context.RunOnNativeModulesQueueThread(() =>
            {
                var txt = Player?.CurrentCueText ?? string.Empty;
                var param = new JObject();
                param.Add("Total", (int)_seekLogic.Duration.TotalMilliseconds);
                param.Add("Current", (int)_seekLogic.CurrentPositionUI.TotalMilliseconds);
                param.Add("SubtiteText", txt);
                SendEvent("onUpdatePlayTime", param);
            });
        }

        private void InitialisePlayback()
        {
            _playbackTimer = new Timer(UpdateTimedData, default, Timeout.Infinite, Timeout.Infinite);
            _seekCompletedSub = _seekLogic.SeekCompleted().Subscribe(_ =>
                Context.RunOnNativeModulesQueueThread(() => SendEvent("onSeekCompleted", new JObject())));

            Player = new PlayerService.PlayerService();
            Player.SetWindow(ReactProgram.RctWindow);

            _bufferingProgressSub = Player.BufferingProgress().Subscribe(UpdateBufferingProgress);
            _eosSub = Player.EndOfStream().Subscribe(_ =>
                Context.RunOnNativeModulesQueueThread(() => SendEvent("onEndOfStream", new JObject())));

            _playbackErrorsSub = Player.PlaybackError().Subscribe(message =>
                {
                    var param = new JObject();
                    param.Add("Message", message);
                    Context.RunOnNativeModulesQueueThread(() => SendEvent("onPlaybackError", param));
                });
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
                            Id = "off",
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

            try
            {
                var streams = await GetStreamsDescriptionInternal(streamTypeIndex, (StreamType)streamTypeIndex)
                    .ConfigureAwait(false);
                var param = new JObject();
                param.Add("Description", Newtonsoft.Json.JsonConvert.SerializeObject(streams));
                param.Add("StreamTypeIndex", streamTypeIndex);
                promise.Resolve(param);
            }
            catch (Exception e)
            {
                promise.Reject(e.Message, e);
            }
        }

        [ReactMethod]
        public async void SetStream(int selectionIndex, int streamTypeIndex, IPromise promise)
        {
            async Task<PlayerState> SetStreamInternal(StreamDescription targetStream)
            {
                if (targetStream.StreamType == StreamType.Subtitle && targetStream.Id == "off")
                    Player.DeactivateStream(StreamType.Subtitle);
                else
                    await Player.ChangeActiveStream(targetStream);

                return Player.State;
            }

            try
            {
                var playerState = _allStreamsDescriptions[streamTypeIndex] != null && selectionIndex != -1
                    ? await SetStreamInternal(_allStreamsDescriptions[streamTypeIndex][selectionIndex]).ConfigureAwait(false)
                    : Player.State;

                promise.Resolve(playerState.ToString());
            }
            catch (Exception e)
            {
                promise.Reject(e.Message, e);
            }
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
        }

        [ReactMethod]
        public void StopPlayback()
        {
            Logger.Info();
            TerminatePlayerService();
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

            try
            {
                var playerState = await PauseResumePlaybackInternal().ConfigureAwait(false);
                promise.Resolve(playerState.ToString());
            }
            catch (Exception e)
            {
                promise.Reject(e.Message, e);
            }
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
            using (LogScope.Create())
            {
                _deepLinkSub = _deepLinkSender
                    .DeepLinkReceived()
                    .Subscribe(OnDeepLinkReceived, OnDeepLinkClosed);
            }
        }

        [ReactMethod]
        public void ExitApp()
        {
            using (LogScope.Create())
            {
                //Context.RemoveLifecycleEventListener(this);
                _mainSynchronizationContext.Post(_ => Application.Current.Exit(), null);
            }
        }
    }
}