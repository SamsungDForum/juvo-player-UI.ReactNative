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
using System.Threading;
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
        private Subject<string> _errorSubject = new Subject<string>();
        private Subject<int> _bufferingSubject = new Subject<int>();
        private Subject<Unit> _endOfStream = new Subject<Unit>();
        private IDisposable _playerEventSubscription;

        private ClipDefinition _currentClip;
        private TimeSpan? _suspendTimeIndex;

        private const string _pauseFailMessage = "Something went wrong when pausing.";
        private const string _resumeFailMessage = "Resume did not succeed.";
        private const string _changeStreamFailMessage = "Stream change failure caused by ***.";
        private const string _setSourceFailMessage = "Tried hard.. Failed harder to prepare playback.";
        private const string _startFailMessage = "Starting playback did not start nor said why.";
        private const string _seekFailMessage = "Was seeking but got lost along the way.";

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
                // Terminate subjects on player thread. Any pending thread jobs will handle terminated subjects,
                // _playerStateSubject in particular, gracefully.
                _errorSubject.OnCompleted();
                _bufferingSubject.OnCompleted();
                _endOfStream.OnCompleted();

                _errorSubject.Dispose();
                _bufferingSubject.Dispose();
                _endOfStream.Dispose();

                _errorSubject = null;
                _bufferingSubject = null;
                _endOfStream = null;

                Logger.Info("Disposed event subjects");
                if (_player != null)
                {
                    try
                    {
                        _playerEventSubscription.Dispose();
                        _playerEventSubscription = default;

                        await _player.DisposeAsync();
                        Logger.Info("Disposed player");

                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Ignoring exception: {e}");
                    }
                }
            }

            Logger.LogEnter();

            var job = await _playerThread.ThreadJob(TerminateJob).ConfigureAwait(false);
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

            WaitHandle.WaitAll(new[] { ((IAsyncResult)TerminatePlayer()).AsyncWaitHandle });
            _playerThread.Join();

            Logger.LogExit();
        }

        public TimeSpan Duration => _player?.Duration ?? TimeSpan.Zero;
        public TimeSpan CurrentPosition => _player?.Position ?? TimeSpan.Zero;
        public bool IsSeekingSupported => true;
        public PlayerState State => _player?.State ?? PlayerState.None;
        public string CurrentCueText { get; }

        public async Task Pause()
        {
            async Task PauseJob()
            {
                await _player.Pause();
                Logger.Info(_player.State.ToString());
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => PauseJob().ReportException(_errorSubject, _pauseFailMessage))
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
                .ThreadJob(() => SeekJob(to).ReportException(_errorSubject, _seekFailMessage))
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
                .ThreadJob(() => ChangeStreamJob(streamDescription).ReportException(_errorSubject, _changeStreamFailMessage))
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
                    _playerEventSubscription = SubscribePlayerEvents(_player, OnEvent);
                    await _player.Prepare();
                    Logger.Info(_player.State.ToString());
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

            if (!clip.Type.Equals("dash", StringComparison.InvariantCultureIgnoreCase))
            {
                _errorSubject.OnNext($"Unsupported protocol: {clip.Type}");
            }
            else
            {
                var job = await _playerThread
                    .ThreadJob(() => SetSourceJob(clip).ReportException(_errorSubject, _setSourceFailMessage))
                    .ConfigureAwait(false);

                await job.ConfigureAwait(false);
            }

            Logger.LogExit();
        }

        public async Task Start()
        {
            async Task StartJob()
            {
                _player.Play();
                Logger.Info(_player.State.ToString());
            }

            Logger.LogEnter();

            var job = await _playerThread
                .ThreadJob(() => StartJob().ReportException(_errorSubject, _startFailMessage))
                .ConfigureAwait(false);

            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        public async Task Suspend()
        {
            async Task SuspendJob()
            {
                _suspendTimeIndex = _player.Position ?? TimeSpan.Zero;
                _playerEventSubscription.Dispose();
                _playerEventSubscription = null;

                try
                {
                    await _player.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Ignoring exception: {ex.Message}");
                }

                _player = null;

                Logger.Info($"Suspend position: {_suspendTimeIndex}");
            }

            Logger.LogEnter();

            var job = await _playerThread.ThreadJob(() => SuspendJob()).ConfigureAwait(false);
            await job.ConfigureAwait(false);

            Logger.LogExit();
        }

        public async Task Resume()
        {
            async Task<PlayerState> ResumeJob()
            {
                try
                {
                    _player = BuildDashPlayer(_currentClip);
                    _playerEventSubscription = SubscribePlayerEvents(_player, e => OnEvent(e));
                    await _player.Prepare();
                    await _player.Seek(_suspendTimeIndex.Value);
                    _player.Play();
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

                Logger.Info($"Resumed position/state: {_suspendTimeIndex.Value}/{_player.State}");

                return State;
            }

            Logger.LogEnter();

            if (!_suspendTimeIndex.HasValue)
                return;

            var job = await _playerThread
                .ThreadJob(() => ResumeJob().ReportException(_errorSubject, _resumeFailMessage))
                .ConfigureAwait(false);
            var resumeState = await job.ConfigureAwait(false);

            // Suspend/Resume calls are not routed through JS. If playing state is not reached, report an error.
            if (resumeState != PlayerState.Playing)
                _errorSubject.OnNext($"Resume failed. State {resumeState}");

            Logger.LogExit();
        }

        public IObservable<string> PlaybackError() => _errorSubject.Publish().RefCount();
        public IObservable<int> BufferingProgress() => _bufferingSubject.Publish().RefCount();
        public IObservable<Unit> EndOfStream() => _endOfStream.Publish().RefCount();
        public void SetWindow(Window window) => _window = window;
    }
}
