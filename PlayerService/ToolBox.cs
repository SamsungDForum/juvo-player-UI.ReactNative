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
using System.Runtime.CompilerServices;
using JuvoPlayer.Common;
using UI.Common;

namespace PlayerService
{
    public static class LogRn
    {
        private const string Tag = "JuvoRN";

        public static void Verbose(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Verbose(Tag, message, file, func, line);

        public static void Debug(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Debug(Tag, message, file, func, line);

        public static void Info(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Info(Tag, message, file, func, line);

        public static void Warn(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Warn(Tag, message, file, func, line);

        public static void Error(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Error(Tag, message, file, func, line);

        public static void Fatal(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Error(Tag, message, file, func, line);
    }

    public struct LogScope : IDisposable
    {
        private string _file;
        private string _method;
        private int _line;

        public static LogScope Create(
            string msg = "",
            [CallerFilePath] string file = "",
            [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0)
        {
            LogRn.Debug($"Enter() -> {msg}", file, method, line);

            return new LogScope
            {
                _file = file,
                _method = method,
                _line = line
            };
        }

        public void Dispose() => LogRn.Debug("Exit() <- ", _file, _method, _line);
    }

    internal static class PlayerServiceToolBox
    {
        public const int ThroughputSelection = -1;
        public const string ThroughputDescription = "Auto";
        public const string ThroughputId = @"\ō͡≡o˞̶";

        // Note:
        // Player's StreamType to Content type uses platform's StreamType. Use own conversions.
        public static StreamType AsStreamType(this JuvoPlayer.Common.ContentType contentType)
        {
            switch (contentType)
            {
                case ContentType.Audio:
                    return StreamType.Audio;
                case ContentType.Video:
                    return StreamType.Video;
                case ContentType.Text:
                    return StreamType.Subtitle;

                case ContentType.Application:
                case ContentType.Unknown:
                    return StreamType.Unknown;
                default:
                    return StreamType.Unknown;
            }
        }

        public static ContentType AsContentType(this JuvoPlayer.Common.StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Audio:
                    return ContentType.Audio;
                case StreamType.Video:
                    return ContentType.Video;
                case StreamType.Subtitle:
                    return ContentType.Text;

                case StreamType.Unknown:
                    return ContentType.Unknown;
                default:
                    return ContentType.Unknown;
            }
        }

        public static string FormatDescription(this Format format)
        {
            string description = string.Empty;

            if (!string.IsNullOrEmpty(format.Language))
                description += format.Language;

            if (format.Width.HasValue && format.Height.HasValue)
                description += " " + format.Width + "x" + format.Height;
            else if (format.ChannelCount.HasValue)
                description += " " + format.ChannelCount + " Ch.";

            if (format.Bitrate.HasValue)
                description += " " + (int)(format.Bitrate / 1000) + " kbps";

            return description;
        }

        public static List<StreamDescription> ToStreamDescription(this (StreamGroup[] groups, IStreamSelector[] selectors) grouping, ContentType targeType)
        {
            var descriptions = new List<StreamDescription>();
            var (groups, selectors) = grouping;

            var groupCount = groups.Length;
            for (var g = 0; g < groupCount; g++)
            {
                if (groups[g].ContentType != targeType)
                    continue;

                var streams = groups[g].Streams;
                var streamCount = streams.Count;

                // if there are no streams in group of interest.. nothing more to do.
                // Not expecting multiple group entries for same StreamType
                if (streamCount == 0)
                    break;

                var streamType = targeType.AsStreamType();

                for (var f = 0; f < streamCount; f++)
                {
                    descriptions.Add(new StreamDescription
                    {
                        Default = false,
                        FormatIndex = f,
                        GroupIndex = g,
                        Description = streams[f].Format.FormatDescription(),
                        Id = streams[f].Format.Id,
                        StreamType = streamType
                    });
                }

                switch (streamType)
                {
                    case StreamType.Video when streamCount > 1:
                        // Add 'Auto' option if multiple video streams exist.
                        descriptions.Add(new StreamDescription
                        {
                            // Mark as default if selector is throughput.
                            // Manual stream selections is tracked.. manually (done in UI).
                            Default = selectors[g] is ThroughputHistoryStreamSelector,
                            Description = ThroughputDescription,
                            FormatIndex = ThroughputSelection,
                            GroupIndex = g,
                            Id = ThroughputId,
                            StreamType = StreamType.Video
                        });
                        break;

                    case StreamType.Video:
                        // One video stream.
                        descriptions[0].Default = true;
                        break;

                    case StreamType.Audio:
                        // Default audio = audio.streamcount - 1
                        descriptions[streamCount - 1].Default = true;
                        break;
                }

                // No need to scan further. Not expecting multiple group entries for same StreamType
                break;
            }

            return descriptions;
        }

        public static IEnumerable<StreamGroup> DumpStreamGroups(this IEnumerable<StreamGroup> groups)
        {

            foreach (var group in groups)
            {
                LogRn.Debug($"Group: {group.ContentType} Entries: {group.Streams.Count}");
                group.Streams.DumpStreamInfo();
            }


            return groups;
        }

        public static IEnumerable<StreamDescription> DumpStreamDescriptions(this IEnumerable<StreamDescription> descriptions)
        {

            foreach (var description in descriptions)
                LogRn.Debug($"Stream: {description}");


            return descriptions;
        }

        public static void DumpFormat(this Format format)
        {
            LogRn.Debug($"Id: {format.Id}");
            LogRn.Debug($"\tLabel: '{format.Label}'");
            LogRn.Debug($"\tSelection Flags: '{format.SelectionFlags}'");
            LogRn.Debug($"\tRole Flags: '{format.RoleFlags}'");
            LogRn.Debug($"\tBitrate: '{format.Bitrate}'");
            LogRn.Debug($"\tCodecs: '{format.Codecs}'");
            LogRn.Debug($"\tContainer MimeType: '{format.ContainerMimeType}'");
            LogRn.Debug($"\tSample MimeType: '{format.SampleMimeType}'");
            LogRn.Debug($"\tWxH: '{format.Width}x{format.Height}'");
            LogRn.Debug($"\tFrame Rate:'{format.FrameRate}'");
            LogRn.Debug($"\tSample Rate: '{format.SampleRate}'");
            LogRn.Debug($"\tChannel Count: '{format.ChannelCount}'");
            LogRn.Debug($"\tSample Rate: '{format.SampleRate}'");
            LogRn.Debug($"\tLanguage: '{format.Language}'");
            LogRn.Debug($"\tAccessibility Channel: '{format.AccessibilityChannel}'");
        }

        public static IEnumerable<StreamInfo> DumpStreamInfo(this IEnumerable<StreamInfo> streamInfos)
        {

            foreach (var info in streamInfos)
                info.Format.DumpFormat();


            return streamInfos;
        }
    }
}