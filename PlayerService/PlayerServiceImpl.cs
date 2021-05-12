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
using System.Threading.Tasks;
using UI.Common;
using JuvoPlayer.Common;
using Window = ElmSharp.Window;

namespace PlayerService
{
    public class PlayerServiceImpl : IPlayerService
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public TimeSpan Duration { get; }
        public TimeSpan CurrentPosition { get; }
        public bool IsSeekingSupported { get; }
        public PlayerState State { get; }
        public string CurrentCueText { get; }
        public Task Pause()
        {
            throw new NotImplementedException();
        }

        public Task SeekTo(TimeSpan to)
        {
            throw new NotImplementedException();
        }

        public Task ChangeActiveStream(StreamDescription streamDescription)
        {
            throw new NotImplementedException();
        }

        public void DeactivateStream(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public Task<List<StreamDescription>> GetStreamsDescription(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public Task SetSource(ClipDefinition clip)
        {
            throw new NotImplementedException();
        }

        public Task Start()
        {
            throw new NotImplementedException();
        }

        public Task Stop()
        {
            throw new NotImplementedException();
        }

        public Task Suspend()
        {
            throw new NotImplementedException();
        }

        public Task Resume()
        {
            throw new NotImplementedException();
        }

        public IObservable<PlayerState> StateChanged()
        {
            throw new NotImplementedException();
        }

        public IObservable<string> PlaybackError()
        {
            throw new NotImplementedException();
        }

        public IObservable<int> BufferingProgress()
        {
            throw new NotImplementedException();
        }

        public void SetWindow(Window window)
        {
            throw new NotImplementedException();
        }
    }
}

