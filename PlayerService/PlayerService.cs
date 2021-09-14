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
using System.Reactive.Threading.Tasks;
using System.Threading;
using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

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

        public TimeSpan Duration => _player?.Duration ?? TimeSpan.Zero;
        public TimeSpan CurrentPosition => _player?.Position ?? TimeSpan.Zero;
        public bool IsSeekingSupported => true;
        public PlayerState State => _player?.State ?? PlayerState.None;
        public string CurrentCueText { get; }

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
                LogRn.Info("Disposing event subscription");
                _playerEventSubscription?.Dispose();
                _playerEventSubscription = default;

                if (_player != default)
                {
                    try
                    {
                        LogRn.Info("Disposing player");
                        await _player.DisposeAsync();
                        _player = default;
                    }
                    catch (Exception e)
                    {
                        LogRn.Warn($"Ignoring exception: {e}");
                    }
                }

                LogRn.Info("Completing event subjects");
                _errorSubject.OnCompleted();
                _bufferingSubject.OnCompleted();
                _endOfStream.OnCompleted();

                LogRn.Info("Disposing event subjects");
                _errorSubject.Dispose();
                _bufferingSubject.Dispose();
                _endOfStream.Dispose();

                _errorSubject = null;
                _bufferingSubject = null;
                _endOfStream = null;
            }

            using (LogScope.Create())
            {
                var job = await _playerThread.ThreadJob(TerminateJob).ConfigureAwait(false);
                await job.ConfigureAwait(false);
            }
        }


        private void OnEvent(IEvent ev)
        {
            using (LogScope.Create())
            {
                LogRn.Info(ev.ToString());

                switch (ev)
                {
                    case EosEvent _:
                        _endOfStream?.OnNext(Unit.Default);
                        break;

                    case BufferingEvent buf:
                        _bufferingSubject?.OnNext(buf.IsBuffering ? 0 : 100);
                        break;

                    case ExceptionEvent ex:
                        var errMsg = $"{ex.Exception.Message} {ex.Exception.InnerException?.Message}";
                        LogRn.Error(errMsg);
                        break;
                }
            }
        }

        private static IDisposable SubscribePlayerEvents(IPlayer player, Action<IEvent> handler) =>
            player.OnEvent().Subscribe(handler);

        private static async Task PrepareWithCancellation(IPlayer player)
        {
            using (LogScope.Create())
            {
                using (var cts = new CancellationTokenSource())
                {
                    var errorTask = player
                        .OnEvent()
                        .FirstAsync(ev => ev is ExceptionEvent)
                        .Select(ev => (ev as ExceptionEvent).Exception)
                        .ToTask(cts.Token);
                    var prepareTask = player.Prepare();
                    var firstCompleted = await Task.WhenAny(errorTask, prepareTask);
                    if (firstCompleted == errorTask)
                    {
                        LogRn.Error("Prepare completed with error: " + errorTask.Result);
                        cts.Cancel();
                        throw errorTask.Result;
                    }

                    LogRn.Info("Prepare completed without errors");
                }
            }
        }

        public void Dispose()
        {
            using (LogScope.Create())
            {
                new Task(async () => await TerminatePlayer().ConfigureAwait(false), CancellationToken.None, TaskCreationOptions.DenyChildAttach).RunSynchronously();

                LogRn.Info("Waiting player thread join");
                _playerThread.Join();
            }
        }

        public async Task Pause()
        {
            async Task PauseJob()
            {
                await _player.Pause();
                LogRn.Info(_player.State.ToString());
            }

            using (LogScope.Create())
            {
                var job = await _playerThread
                    .ThreadJob(() => PauseJob().ReportException(_errorSubject, _pauseFailMessage))
                    .ConfigureAwait(false);

                await job.ConfigureAwait(false);
            }
        }

        public async Task SeekTo(TimeSpan to)
        {
            async Task SeekJob(TimeSpan seekTo)
            {
                await _player.Seek(seekTo);
                LogRn.Info($"Seeked to: {seekTo}");
            }

            using (LogScope.Create())
            {
                var job = await _playerThread
                    .ThreadJob(() => SeekJob(to).ReportException(_errorSubject, _seekFailMessage))
                    .ConfigureAwait(false);

                await job.ConfigureAwait(false);
            }
        }

        public async Task ChangeActiveStream(int groupIdx, int formatIdx)
        {
            async Task ChangeStreamJob(int groupId, int formatId)
            {
                var (groups, selectors) = _player.GetSelectedStreamGroups();

                if (formatId == PlayerServiceToolBox.ThroughputSelection)
                {
                    selectors[groupId] = new ThroughputHistoryStreamSelector(new ThroughputHistory());
                    LogRn.Info($"Selecting {groups[groupId].ContentType} 'ThroughputHistoryStreamSelector'");
                }
                else
                {
                    selectors[groupId] = new FixedStreamSelector(formatId);
                    LogRn.Info($"Selecting {groups[groupId].ContentType} FormatId '{formatId}' {groups[groupId].Streams[formatId].Format.FormatDescription()}");
                }

                await _player.SetStreamGroups(groups, selectors);
            }

            using (LogScope.Create())
            {
                var job = await _playerThread
                    .ThreadJob(() =>
                        ChangeStreamJob(groupIdx, formatIdx).ReportException(_errorSubject, _changeStreamFailMessage))
                    .ConfigureAwait(false);

                await job.ConfigureAwait(false);
            }
        }

        public void DeactivateStream(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public async Task<List<StreamDescription>> GetStreamsDescription(StreamType streamType)
        {
            Task<List<StreamDescription>> GetStreamsJob(ContentType contentType)
            {
                LogRn.Info(contentType.ToString());

                // Do Note. Odering of streams in StreamGroups differs between GetStreamGroups() and GetSelectedStresmsGroups().
                return Task.FromResult(_player
                    .GetSelectedStreamGroups()
                    .ToStreamDescription(contentType));
            }

            using (LogScope.Create())
            {
                var job = await _playerThread
                    .ThreadJob(() => GetStreamsJob(streamType.AsContentType()).ReportException(_errorSubject))
                    .ConfigureAwait(false);

                return await job.ConfigureAwait(false);
            }
        }

        public async Task SetSource(ClipDefinition clip)
        {
            async Task SetSourceJob(ClipDefinition source)
            {
                LogRn.Info(source.Url);
                _currentClip = source;

                try
                {
                    _player = BuildDashPlayer(source);
                    _playerEventSubscription = SubscribePlayerEvents(_player, OnEvent);
                    await PrepareWithCancellation(_player);
                    LogRn.Info(_player.State.ToString());
                }
                catch (Exception e)
                {
                    LogRn.Error($"Prepare failed: {e}");
                    throw new Exception(_setSourceFailMessage, e);
                }
            }

            using (LogScope.Create())
            {
                var job = await _playerThread
                    .ThreadJob(() => SetSourceJob(clip))
                    .ConfigureAwait(false);

                await job.ConfigureAwait(false);
            }
        }

        public async Task Start()
        {
            Task StartJob()
            {
                _player.Play();
                LogRn.Info(_player.State.ToString());
                return Task.CompletedTask;
            }

            using (LogScope.Create())
            {
                var job = await _playerThread
                    .ThreadJob(() => StartJob().ReportException(_errorSubject, _startFailMessage))
                    .ConfigureAwait(false);

                await job.ConfigureAwait(false);
            }
        }

        public async Task Suspend()
        {
            async Task SuspendJob()
            {
                try
                {
                    _suspendTimeIndex = _player.Position ?? TimeSpan.Zero;
                    _playerEventSubscription.Dispose();
                    _playerEventSubscription = null;
                    await _player.DisposeAsync();
                }
                catch (Exception ex)
                {
                    LogRn.Warn($"Ignoring exception: {ex.Message}");
                }

                _player = null;
                LogRn.Info($"Suspend position: {_suspendTimeIndex}");
            }

            using (LogScope.Create())
            {
                var job = await _playerThread.ThreadJob(SuspendJob).ConfigureAwait(false);
                await job.ConfigureAwait(false);
            }
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
                    LogRn.Error($"Prepare failed {e.Message}");

                    if (_player != default)
                    {
                        await _player.DisposeAsync();
                    }

                    throw;
                }

                LogRn.Info($"Resumed position/state: {_suspendTimeIndex.Value}/{_player.State}");
                return State;
            }

            using (LogScope.Create())
            {
                if (!_suspendTimeIndex.HasValue)
                    return;

                var job = await _playerThread
                    .ThreadJob(() => ResumeJob().ReportException(_errorSubject, _resumeFailMessage))
                    .ConfigureAwait(false);
                var resumeState = await job.ConfigureAwait(false);

                // Suspend/Resume calls are not routed through JS. If playing state is not reached, report an error.
                if (resumeState != PlayerState.Playing)
                    _errorSubject.OnNext($"Resume failed. State {resumeState}");
            }
        }

        public IObservable<string> PlaybackError() => _errorSubject.Publish().RefCount();
        public IObservable<int> BufferingProgress() => _bufferingSubject.Publish().RefCount();
        public IObservable<Unit> EndOfStream() => _endOfStream.Publish().RefCount();
        public void SetWindow(Window window) => _window = window;
    }
}
