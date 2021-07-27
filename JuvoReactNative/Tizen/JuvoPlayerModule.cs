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
using ReactNative.JavaScriptCore;
using Tizen.Applications;
using UI.Common;

namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");

        private readonly SeekLogic _seekLogic = null; // needs to be initialized in the constructor!
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

        private void InitialisePlayback()
        {
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
        /// DO NOTE:
        /// All JS to Native calls are ASYNC!
        /// Use promise/event if sync execution is needed
        //////////////////////////////////////////////
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
                var streamType = (StreamType) streamTypeIndex;
                var streams = await GetStreamsDescriptionInternal(streamTypeIndex, streamType)
                    .ConfigureAwait(false);

                var streamLabel = streamType.ToString();
                var param = new JObject();
                param.Add(streamLabel, Newtonsoft.Json.JsonConvert.SerializeObject(streams));
                param.Add("streamTypeIndex", streamTypeIndex);
                param.Add("streamLabel", streamLabel);
                promise.Resolve(param);
            }
            catch (Exception e)
            {
                promise.Reject(e.GetType().ToString(),e.Message);
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
                promise.Reject(e.GetType().ToString(), e.Message);
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
            }

            if (string.IsNullOrWhiteSpace(videoURI))
            {
                promise.Reject("uri", "not specified");
            }
            else if (string.IsNullOrWhiteSpace(streamingProtocol))
            {
                promise.Reject("uri", "protocol not specified");
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
                    promise.Reject(e.GetType().ToString(), e.Message);
                }
            }
        }

        [ReactMethod]
        public void StopPlayback(IPromise promise)
        {
            using (LogScope.Create())
            {
                try
                {
                    TerminatePlayerService();
                }
                catch (Exception e)
                {
                    // Inform but don't fail. Decouples player state from UI.
                    Logger.Warn(e.Message);
                }

                // Don't pass "failed/sucess" to JS. Promise is to allow JS to wait for invokation completion.
                promise.Resolve(default);
            }
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
                        break;

                    case PlayerState.Paused:
                        pauseResumeTask = Player.Start();
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
                promise.Reject(e.GetType().ToString(), e.Message);
            }
        }

        [ReactMethod]
        public void Forward()
        {
            using (LogScope.Create()) 
                _seekLogic.SeekForward();
        }

        [ReactMethod]
        public void Rewind()
        {
            using (LogScope.Create())
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

        [ReactMethod]
        public void GetPlaybackInfo(IPromise promise)
        {
            try
            {
                var position = _seekLogic.CurrentPositionUI;
                var duration = _seekLogic.Duration;
                var progress = (int)((position / duration) * 100);
                var isPlaying = Player.State == PlayerState.Playing;

                promise.Resolve(new JObject
                {
                    {"position", position.ToString(@"hh\:mm\:ss")},
                    {"duration", duration.ToString(@"hh\:mm\:ss")},
                    {"progress", progress},
                    {"isPlaying", isPlaying}
                });
            }
            catch (Exception e)
            {
                // Will be raised if called prior to playback setup & start.
                // Don't penalise such use case, just inform. Decouples "current state" dependency from UI.
                Logger.Warn(e.Message);

                var zeroTimeIndex = TimeSpan.Zero.ToString(@"hh\:mm\:ss");

                promise.Resolve(new JObject
                {
                    {"position", zeroTimeIndex},
                    {"duration", zeroTimeIndex},
                    {"progress", 0},
                    {"isPlaying", false}
                });
            }
        }
    }

    internal static class ContextDump
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");

        private static void Dump(this ReactContext context)
        {
            string runningOn = string.Empty;

            if (context.IsOnDispatcherQueueThread())
                runningOn += "Dispatcher ";

            if (context.IsOnNativeModulesQueueThread())
                runningOn += "Native ";

            if (context.IsOnJavaScriptQueueThread())
                runningOn += "JavaScript ";

            if (string.IsNullOrEmpty(runningOn))
                runningOn = "Unknown";

            Logger.Debug($"Thread: {runningOn}");
        }
    }
}