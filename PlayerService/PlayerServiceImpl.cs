/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Reactive.Linq;
using System.Threading.Tasks;
using Nito.AsyncEx;
using UI.Common;
using System.Reactive.Subjects;
using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using static PlayerService.PlayerServiceToolBox;
using Window = ElmSharp.Window;

namespace PlayerService
{
    public class PlayerServiceImpl : IPlayerService
    {
        private readonly AsyncContextThread _playerThread = new AsyncContextThread();
        private Window _window;
        private IPlayer _player;
        private readonly BehaviorSubject<PlayerState> _playerStateSubject = new BehaviorSubject<PlayerState>(PlayerState.None);
        private readonly Subject<string> _errorSubject = new Subject<string>();
        private readonly Subject<int> _bufferingSubject = new Subject<int>();
        private IDisposable _playerEventSubscription;

        private ClipDefinition _currentClip;
        private TimeSpan _suspendTimeIndex;

        private IPlayer BuildDashPlayer(ClipDefinition clip, Configuration configuration = default)
        {
            string SchemeToKeySystem(in string scheme)
            {
                switch (scheme)
                {
                    case "playready":
                        return "com.microsoft.playready";
                    case "widevine":
                        return "com.widevine.alpha";
                    default:
                        return scheme;
                }
            }

            DashPlayerBuilder builder = new DashPlayerBuilder()
                .SetWindow(new JuvoPlayer.Platforms.Tizen.ElmSharpWindow(_window))
                .SetMpdUri(clip.Url)
                .SetConfiguration(configuration);

            DrmDescription drmInfo = clip.DRMDatas?.FirstOrDefault();
            if (drmInfo != null)
            {
                builder = builder
                    .SetKeySystem(SchemeToKeySystem(drmInfo.Scheme))
                    .SetDrmSessionHandler(new YoutubeDrmSessionHandler(
                        drmInfo.LicenceUrl,
                        drmInfo.KeyRequestProperties));
            }

            return builder.Build();
        }

        private async Task TerminatePlayer()
        {
            Logger.LogEnter();

            _playerEventSubscription?.Dispose();

            if (_player != null)
            {
                Logger.Info("Disposing player");
                var current = _player;
                _player = null;
                _window = null;

                try
                {
                    await current.DisposeAsync();
                }
                catch (Exception e)
                {
                    Logger.Warn($"Ignoring exception: {e}");
                }
            }

            Logger.LogExit();
        }

        private async Task OnEvent(IEvent ev)
        {
            Logger.Info(ev.ToString());

            switch (ev)
            {
                case EosEvent _:
                    await ThreadJob(() => _playerStateSubject.OnCompleted());
                    break;

                case BufferingEvent buf:
                    bool buffering = buf.IsBuffering;
                    _playerStateSubject.OnNext(buffering ? PlayerState.Paused : PlayerState.Playing);
                    _bufferingSubject.OnNext(buffering ? 0 : 100);
                    break;
            }
        }

        private Task<TResult> ThreadJob<TResult>(Func<TResult> threadFunc) =>
            _playerThread.Factory.StartNew(() => InvokeFunction(threadFunc));

        private Task ThreadJob(Action threadAction) =>
            _playerThread.Factory.StartNew(() => InvokeAction(threadAction));

        private TResult InvokeFunction<TResult>(Func<TResult> threadFunction)
        {
            try
            {
                return threadFunction();
            }
            catch (Exception e)
            {
                _errorSubject.OnNext($"{e.GetType()} {e.Message}");
            }

            return default;
        }

        private void InvokeAction(Action threadAction)
        {
            try
            {
                threadAction();
            }
            catch (Exception e)
            {
                _errorSubject.OnNext($"{e.GetType()} {e.Message}");
            }
        }

        public void Dispose()
        {
            Logger.LogEnter();

            ThreadJob(async () => await TerminatePlayer());
            _playerThread.Join();

            _errorSubject.OnCompleted();
            _errorSubject.Dispose();
            _bufferingSubject.OnCompleted();
            _bufferingSubject.Dispose();
            _playerStateSubject.Dispose();

            Logger.LogExit();
        }

        public TimeSpan Duration => _player?.Duration ?? TimeSpan.Zero;
        public TimeSpan CurrentPosition => _player?.Position ?? TimeSpan.Zero;
        public bool IsSeekingSupported => true;
        public PlayerState State => _playerStateSubject.Value;
        public string CurrentCueText { get; }

        public async Task Pause()
        {
            Logger.LogEnter();

            await await ThreadJob(async () => await _player.Pause());

            Logger.LogExit();
        }

        public async Task SeekTo(TimeSpan to)
        {
            Logger.LogEnter();

            await await ThreadJob(async () => await _player.Seek(to));

            Logger.LogExit();
        }

        public async Task ChangeActiveStream(StreamDescription streamDescription)
        {
            Logger.LogEnter($"Selecting {streamDescription.StreamType} {streamDescription.Description}");

            await await ThreadJob(async () =>
            {
                var selected = _player.GetStreamGroups().SelectStream(
                    streamDescription.StreamType.ToContentType(),
                    streamDescription.Id.ToString());

                if (selected.selector == null)
                {
                    Logger.Warn($"Stream index not found {streamDescription.StreamType} {streamDescription.Description}");
                    return;
                }

                var (newGroups, newSelectors) = _player.GetSelectedStreamGroups().UpdateSelection(selected);

                Logger.Info($"Using {selected.selector.GetType()} for {streamDescription.StreamType} {streamDescription.Description}");

                await _player.SetStreamGroups(newGroups, newSelectors);
            });

            Logger.LogExit();
        }

        public void DeactivateStream(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public async Task<List<StreamDescription>> GetStreamsDescription(StreamType streamType)
        {
            Logger.LogEnter(streamType.ToString());

            var result = await ThreadJob(() =>
                _player.GetStreamGroups().GetStreamDescriptionsFromStreamType(streamType));

            Logger.LogExit();

            return result.ToList();
        }

        public async Task SetSource(ClipDefinition clip)
        {
            Logger.LogEnter(clip.Url);

            await await ThreadJob(async () =>
            {
                IPlayer player = BuildDashPlayer(clip);
                _playerEventSubscription = player.OnEvent().Subscribe(async e => await OnEvent(e));

                await player.Prepare();
                _player = player;
                _currentClip = clip;

                _playerStateSubject.OnNext(PlayerState.Ready);
            });

            Logger.LogExit();
        }

        public async Task Start()
        {
            Logger.LogEnter();

            await ThreadJob(() =>
            {
                PlayerState current = _player.State;
                switch (current)
                {
                    case PlayerState.Playing:
                        _player.Pause();
                        _playerStateSubject.OnNext(PlayerState.Paused);
                        break;

                    case PlayerState.Ready:
                    case PlayerState.Paused:
                        _player.Play();
                        _playerStateSubject.OnNext(PlayerState.Playing);
                        break;

                    default:
                        Logger.Warn($"Cannot play/pause in state: {current}");
                        break;
                }
            });

            Logger.LogExit();
        }

        public async Task Stop()
        {
            Logger.LogEnter();

            // Terminate by closing state subject. Single player termination path via Dispose. Suspend excluded.
            await ThreadJob(() => _playerStateSubject.OnCompleted());

            Logger.LogExit();
        }

        public async Task Suspend()
        {
            Logger.LogEnter();

            await await ThreadJob(async () =>
            {
                _suspendTimeIndex = _player.Position ?? TimeSpan.Zero;
                await TerminatePlayer();

                Logger.Info($"Suspended {_suspendTimeIndex}@{_currentClip.Url}");
            });

            Logger.LogExit();
        }

        public async Task Resume()
        {
            Logger.LogEnter();

            await await ThreadJob(async () =>
            {
                IPlayer player = BuildDashPlayer(_currentClip, new Configuration { StartTime = _suspendTimeIndex });
                _playerEventSubscription = player.OnEvent().Subscribe(async e => await OnEvent(e));

                await player.Prepare();
                player.Play();
                _player = player;
                // Can be expanded to restore track selection / playback state (Paused/Playing)

                Logger.Info($"Resumed {_suspendTimeIndex}@{_currentClip.Url}");
            });

            Logger.LogExit();
        }

        public IObservable<PlayerState> StateChanged() => _playerStateSubject.Publish().RefCount();
        
        public IObservable<string> PlaybackError() => _errorSubject.Publish().RefCount();

        public IObservable<int> BufferingProgress() => _bufferingSubject.Publish().RefCount();
        
        public void SetWindow(Window window) => _window = window;
    }
}