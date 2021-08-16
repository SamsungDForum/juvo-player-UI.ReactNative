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
using JuvoLogger;
using JuvoPlayer.Common;
using UI.Common;

namespace PlayerService
{
    public struct LogScope : IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");

        private string _file;
        private string _method;
        private int _line;

        public static LogScope Create(
            string msg = "",
            [CallerFilePath] string file = "",
            [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0)
        {
            Logger.Debug($"Enter() -> {msg}", file, method, line);

            return new LogScope
            {
                _file = file,
                _method = method,
                _line = line
            };
        }

        public void Dispose() => Logger.Debug("Exit() <- ", _file, _method, _line);
    }

    internal static class PlayerServiceToolBox
    {
        public static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");

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
            if (Logger.IsLevelEnabled(LogLevel.Debug))
            {
                foreach (var group in groups)
                {
                    Logger.Debug($"Group: {group.ContentType} Entries: {group.Streams.Count}");
                    group.Streams.DumpStreamInfo();
                }
            }

            return groups;
        }

        public static IEnumerable<StreamDescription> DumpStreamDescriptions(this IEnumerable<StreamDescription> descriptions)
        {
            if (Logger.IsLevelEnabled(LogLevel.Debug))
            {
                foreach (var description in descriptions)
                    Logger.Debug($"Stream: {description}");
            }

            return descriptions;
        }

        public static void DumpFormat(this Format format)
        {
            Logger.Debug($"Id: {format.Id}");
            Logger.Debug($"\tLabel: '{format.Label}'");
            Logger.Debug($"\tSelection Flags: '{format.SelectionFlags}'");
            Logger.Debug($"\tRole Flags: '{format.RoleFlags}'");
            Logger.Debug($"\tBitrate: '{format.Bitrate}'");
            Logger.Debug($"\tCodecs: '{format.Codecs}'");
            Logger.Debug($"\tContainer MimeType: '{format.ContainerMimeType}'");
            Logger.Debug($"\tSample MimeType: '{format.SampleMimeType}'");
            Logger.Debug($"\tWxH: '{format.Width}x{format.Height}'");
            Logger.Debug($"\tFrame Rate:'{format.FrameRate}'");
            Logger.Debug($"\tSample Rate: '{format.SampleRate}'");
            Logger.Debug($"\tChannel Count: '{format.ChannelCount}'");
            Logger.Debug($"\tSample Rate: '{format.SampleRate}'");
            Logger.Debug($"\tLanguage: '{format.Language}'");
            Logger.Debug($"\tAccessibility Channel: '{format.AccessibilityChannel}'");
        }

        public static IEnumerable<StreamInfo> DumpStreamInfo(this IEnumerable<StreamInfo> streamInfos)
        {
            if (Logger.IsLevelEnabled(LogLevel.Debug))
            {
                foreach (var info in streamInfos)
                    info.Format.DumpFormat();
            }

            return streamInfos;
        }
    }
}