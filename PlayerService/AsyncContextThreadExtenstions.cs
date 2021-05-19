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
    public static class AsyncContextThreadExtenstions
    {
        // Wrappers allowing awaiting for execution completion of action/function payloads running context thread.
        // As such "minimal" context thread action will be of form Task<Task> / Task<Task<TResult>>.
        // First Task represents context thread launch. Inner Task/Task<TResult> represents context thread operaation
        // Async payloads will have inner task in form of Task<Task> / Task<Task<TResult>
        private static async Task RunAction(Action threadAction) => threadAction();
        private static async Task<TResult> RunFunction<TResult>(Func<TResult> threadFunction) => threadFunction();

        public static Task<Task<TResult>> ThreadJob<TResult>(this AsyncContextThread thread, Func<TResult> threadFunction) =>
            thread.Factory.StartNew(() => RunFunction(threadFunction));
        public static Task<Task> ThreadJob(this AsyncContextThread thread, Action threadAction) =>
            thread.Factory.StartNew(() => RunAction(threadAction));

        public static async Task ReportException(this Task<Task> threadJob, Subject<string> reportTo)
        {
            try
            {
                await await threadJob;
            }
            catch (Exception e)
            {
                Logger.Error($"{e.GetType()} {e.Message}");
                reportTo.OnNext(e.Message);
            }
        }

        public static async Task ReportException(this Task<Task<Task>> threadJob, Subject<string> reportTo)
        {
            try
            {
                await await await threadJob;
            }
            catch (Exception e)
            {
                Logger.Error($"{e.GetType()} {e.Message}");
                reportTo.OnNext(e.Message);
            }
        }

        public static async Task<TResult> ReportException<TResult>(this Task<Task<TResult>> threadJob, Subject<string> reportTo)
        {
            try
            {
                return await await threadJob;
            }
            catch (Exception e)
            {
                Logger.Error($"{e.GetType()} {e.Message}");
                reportTo.OnNext(e.Message);
            }

            return default;
        }

        public static async Task<TResult> ReportException<TResult>(this Task<Task<Task<TResult>>> threadJob, Subject<string> reportTo)
        {
            try
            {
                return await await await threadJob;
            }
            catch (Exception e)
            {
                Logger.Error($"{e.GetType()} {e.Message}");
                reportTo.OnNext(e.Message);
            }

            return default;
        }
    }
}