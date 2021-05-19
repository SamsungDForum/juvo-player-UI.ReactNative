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
    public class PlayerService : IPlayerService
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

        private Task OnEvent(IEvent ev)
        {
            Logger.Info(ev.ToString());

            switch (ev)
            {
                case EosEvent _:
                    Logger.Info("EOS observed");
                    break;

                case BufferingEvent buf:
                    _bufferingSubject.OnNext(buf.IsBuffering ? 0 : 100);
                    break;
            }

            return Task.CompletedTask;
        }

        private IDisposable SubscribePlayerEvents(IPlayer player) =>
            player.OnEvent().Subscribe(async (e) => await OnEvent(e));

        public void Dispose()
        {
            Logger.LogEnter();

            _playerThread.ThreadJob(async () => await TerminatePlayer());

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

            await _playerThread
                .ThreadJob(async () => await _player.Pause())
                .ReportException(_errorSubject);
            
            Logger.LogExit();
        }

        public async Task SeekTo(TimeSpan to)
        {
            Logger.LogEnter();

            await _playerThread
                .ThreadJob(async () => await _player.Seek(to))
                .ReportException(_errorSubject);

            Logger.LogExit();
        }

        public async Task ChangeActiveStream(StreamDescription streamDescription)
        {
            Logger.LogEnter($"Selecting {streamDescription.StreamType} {streamDescription.Description}");

            await _playerThread.ThreadJob(async () =>
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
            }).ReportException(_errorSubject);

            Logger.LogExit();
        }

        public void DeactivateStream(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public async Task<List<StreamDescription>> GetStreamsDescription(StreamType streamType)
        {
            Logger.LogEnter($"{streamType}");

            var result = await _playerThread
                .ThreadJob(async () => _player.GetStreamGroups().GetStreamDescriptionsFromStreamType(streamType))
                .ReportException(_errorSubject);

            Logger.LogExit();

            return result.ToList();
        }

        public async Task SetSource(ClipDefinition clip)
        {
            Logger.LogEnter(clip.Url);

            await _playerThread.ThreadJob(async () =>
            {
                IPlayer player = BuildDashPlayer(clip);
                _playerEventSubscription = SubscribePlayerEvents(player);
                try
                {
                    await player.Prepare();
                }
                catch (Exception)
                {
                    await player.DisposeAsync();
                    throw;
                }

                _player = player;
                _currentClip = clip;
                _playerStateSubject.OnNext(PlayerState.Ready);
            }).ReportException(_errorSubject);

            Logger.LogExit();
        }

        public async Task Start()
        {
            Logger.LogEnter();

            await _playerThread.ThreadJob(() =>
            {
                _player.Play();
                _playerStateSubject.OnNext(PlayerState.Playing);
            }).ReportException(_errorSubject);

            Logger.LogExit();
        }

        public async Task Stop()
        {
            Logger.LogEnter();

            await _playerThread
                .ThreadJob(_playerStateSubject.OnCompleted)
                .ReportException(_errorSubject);
            
            Logger.LogExit();
        }

        public async Task Suspend()
        {
            Logger.LogEnter();

            await _playerThread.ThreadJob(async () =>
            {
                _suspendTimeIndex = _player.Position ?? TimeSpan.Zero;
                await TerminatePlayer();

                Logger.Info($"Suspended {_suspendTimeIndex}@{_currentClip.Url}");
            }).ReportException(_errorSubject);

            Logger.LogExit();
        }

        public async Task Resume()
        {
            Logger.LogEnter();

            await _playerThread.ThreadJob(async () =>
            {
                IPlayer player = BuildDashPlayer(_currentClip, new Configuration {StartTime = _suspendTimeIndex});
                _playerEventSubscription = SubscribePlayerEvents(player);

                await player.Prepare();
                player.Play();
                _player = player;
                // Can be expanded to restore track selection / playback state (Paused/Playing)

                Logger.Info($"Resumed {_suspendTimeIndex}@{_currentClip.Url}");
            }).ReportException(_errorSubject);

            
            Logger.LogExit();
        }

        public IObservable<PlayerState> StateChanged() => _playerStateSubject.Publish().RefCount();

        public IObservable<string> PlaybackError() => _errorSubject.Publish().RefCount();

        public IObservable<int> BufferingProgress() => _bufferingSubject.Publish().RefCount();

        public void SetWindow(Window window) => _window = window;
    }
}