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
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Nito.AsyncEx;
using static PlayerService.PlayerServiceToolBox;

namespace PlayerService
{
    public static class AsyncContextThreadExtensions
    {
        public static Task<TResult> ThreadJob<TResult>(this AsyncContextThread thread, Func<TResult> threadFunction) =>
            thread.Factory.StartNew(threadFunction, TaskCreationOptions.DenyChildAttach);

        public static Task ThreadJob(this AsyncContextThread thread, Action threadAction) =>
            thread.Factory.StartNew(threadAction, TaskCreationOptions.DenyChildAttach);

        public static async Task ReportException(this Task threadJob, Subject<string> reportTo, string messsage = default)
        {
            try
            {
                await threadJob;
            }
            catch (Exception e)
            {
                Logger.Error($"{e.GetType()} {e.Message}");
                reportTo?.OnNext(messsage ?? e.Message);
                throw;
            }
        }

        public static async Task<TResult> ReportException<TResult>(this Task<TResult> threadJob, Subject<string> reportTo, string messsage = default)
        {
            try
            {
                return await threadJob;
            }
            catch (Exception e)
            {
                Logger.Error($"{e.GetType()} {e.Message}");
                reportTo?.OnNext(messsage ?? e.Message);
                throw;
            }
        }
    }
}