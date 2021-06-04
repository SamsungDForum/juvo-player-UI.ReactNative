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
using System.Reactive;
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
        private BehaviorSubject<PlayerState> _playerStateSubject = new BehaviorSubject<PlayerState>(PlayerState.None);
        private Subject<string> _errorSubject = new Subject<string>();
        private Subject<int> _bufferingSubject = new Subject<int>();
        private Subject<Unit> _endOfStream = new Subject<Unit>();
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
            async Task TerminateJob()
            {
                Logger.Info("Terminating subjects");

                // Terminate subjects on player thread. Any pending thread jobs will handle terminated subjects,
                // _playerStateSubject in particular, gracefully.
                _errorSubject.OnCompleted();
                _bufferingSubject.OnCompleted();
                _playerStateSubject.OnCompleted();
                _endOfStream.OnCompleted();

                _errorSubject.Dispose();
                _bufferingSubject.Dispose();
                _playerStateSubject.Dispose();
                _endOfStream.Dispose();

                _errorSubject = null;
                _bufferingSubject = null;
                _playerStateSubject = null;
                _endOfStream = null;

                if (_player != null)
                {
                    Logger.Info("Disposing player");

                    try
                    {
                        await _player.DisposeAsync();
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Ignoring exception: {e}");
                    }
                    finally
                    {
                        _playerEventSubscription.Dispose();
                    }
                }
            }

            Logger.LogEnter();

            // wait for job start and do not report termination errors (if any)
            var job = _playerThread.ThreadJob(async () => await TerminateJob()).Result;
            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        private void OnEvent(IEvent ev)
        {
            Logger.Info(ev.ToString());

            switch (ev)
            {
                case EosEvent _:
                    _endOfStream?.OnNext(Unit.Default);
                    break;

                case BufferingEvent buf:
                    _bufferingSubject?.OnNext(buf.IsBuffering ? 0 : 100);
                    break;
            }
        }

        private static IDisposable SubscribePlayerEvents(IPlayer player, Action<IEvent> handler) =>
            player.OnEvent().Subscribe(handler);

        public void Dispose()
        {
            Logger.LogEnter();

            _ = TerminatePlayer();
            _playerThread.Join();

            Logger.LogExit();
        }

        public TimeSpan Duration => _player?.Duration ?? TimeSpan.Zero;
        public TimeSpan CurrentPosition => _player?.Position ?? TimeSpan.Zero;
        public bool IsSeekingSupported => true;
        public PlayerState State => _playerStateSubject.Value;
        public string CurrentCueText { get; }

        public async Task Pause()
        {
            async Task PauseJob()
            {
                Logger.Info();

                await _player.Pause();
                _playerStateSubject?.OnNext(_player.State);
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => PauseJob().ReportException(_errorSubject))
                .ConfigureAwait(false);

            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        public async Task SeekTo(TimeSpan to)
        {
            async Task SeekJob(TimeSpan seekTo)
            {
                await _player.Seek(seekTo);
                Logger.Info($"Seeked to: {seekTo}");
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => SeekJob(to).ReportException(_errorSubject))
                .ConfigureAwait(false);

            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        public async Task ChangeActiveStream(StreamDescription streamDescription)
        {
            async Task ChangeStreamJob(StreamDescription targetStream)
            {
                Logger.Info($"Selecting {targetStream.StreamType} {targetStream.Description}");

                var selected = _player.GetStreamGroups()
                    .SelectStream(
                        targetStream.StreamType.ToContentType(),
                        targetStream.Id);

                if (selected.selector == null)
                {
                    Logger.Warn($"Stream index not found {targetStream.StreamType} {targetStream.Description}");
                    return;
                }

                var (newGroups, newSelectors) = _player
                    .GetSelectedStreamGroups()
                    .UpdateSelection(selected);

                Logger.Info($"Using {selected.selector.GetType()} for {targetStream.StreamType} {targetStream.Description}");

                await _player.SetStreamGroups(newGroups, newSelectors);
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => ChangeStreamJob(streamDescription).ReportException(_errorSubject))
                .ConfigureAwait(false);

            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        public void DeactivateStream(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public async Task<List<StreamDescription>> GetStreamsDescription(StreamType streamType)
        {
            async Task<List<StreamDescription>> GetStreamsJob(StreamType stream)
            {
                Logger.Info($"{stream}");

                var description = _player
                    .GetStreamGroups()
                    .GetStreamDescriptionsFromStreamType(stream)
                    .ToList();

                Logger.LogExit($"{stream} {description.Count} entries");

                return description;
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => GetStreamsJob(streamType).ReportException(_errorSubject))
                .ConfigureAwait(false);

            var streamList = await job.ConfigureAwait(false);
            if (streamList == default)
                streamList = new List<StreamDescription>();

            Logger.LogExit();
            return streamList;
        }

        public async Task SetSource(ClipDefinition clip)
        {
            async Task SetSourceJob(ClipDefinition source)
            {
                Logger.Info(source.Url);
                _currentClip = source;

                try
                {
                    _player = BuildDashPlayer(source);
                    _playerEventSubscription = SubscribePlayerEvents(_player, e => OnEvent(e));
                    await _player.Prepare();
                    _playerStateSubject?.OnNext(_player.State);
                }
                catch (Exception e)
                {
                    Logger.Error($"Prepare failed {e.Message}");

                    if (_player != default)
                    {
                        await _player.DisposeAsync();
                    }

                    throw;
                }
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => SetSourceJob(clip).ReportException(_errorSubject))
                .ConfigureAwait(false);

            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        public async Task Start()
        {
            async Task StartJob()
            {
                Logger.Info();

                _player.Play();
                _playerStateSubject?.OnNext(_player.State);
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => StartJob().ReportException(_errorSubject))
                .ConfigureAwait(false);

            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        public async Task Suspend()
        {
            Logger.LogEnter();

            await Pause().ConfigureAwait(false);
            _suspendTimeIndex = _player.Position ?? TimeSpan.Zero;
            await TerminatePlayer().ConfigureAwait(false);

            Logger.LogExit($"Suspended {_suspendTimeIndex}");
        }

        public async Task Resume()
        {
            Logger.LogEnter($"Resuming {_suspendTimeIndex}");

            await SetSource(_currentClip).ConfigureAwait(false);
            await SeekTo(_suspendTimeIndex).ConfigureAwait(false);
            await Start().ConfigureAwait(false);

            Logger.LogExit();
        }

        public IObservable<PlayerState> StateChanged() => _playerStateSubject.DistinctUntilChanged().Publish().RefCount();
        public IObservable<string> PlaybackError() => _errorSubject.Publish().RefCount();
        public IObservable<int> BufferingProgress() => _bufferingSubject.Publish().RefCount();
        public IObservable<Unit> EndOfStream() => _endOfStream.Publish().RefCount();
        public void SetWindow(Window window) => _window = window;
    }
}