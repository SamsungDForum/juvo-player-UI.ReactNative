/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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

namespace UI.Common.Defaults
{
    public static class SeekLogic
    {
        public static TimeSpan DefaultSeekInterval { get; set; } = TimeSpan.FromSeconds(5);
        public static TimeSpan DefaultSeekAccumulateInterval { get; set; } = TimeSpan.FromSeconds(2);
        public static double DefaultMaximumSeekIntervalPercentOfContentTotalTime { get; set; } = 1.0;
        public static TimeSpan DefaultSeekIntervalValueThreshold { get; set; } =
                TimeSpan.FromMilliseconds(200); // time between key events when key is being hold is ~100ms   
    }

    public static class SupportedClipTypes
    {
        public static string[] SupportedTypes { get; set; } = { "dash" };
    }
}
