﻿/*!
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
using System.Runtime.CompilerServices;
using System.Text;
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

        public static StreamDescription ToStreamDescription(this Format format, StreamType stream)
        {
            string description = string.Empty;

            if (!string.IsNullOrEmpty(format.Language))
                description += format.Language;

            if (format.Width.HasValue && format.Height.HasValue)
                description += " " + format.Width + "x" + format.Height;
            else if (format.ChannelCount.HasValue)
                description += " " + format.ChannelCount +" Ch.";

            if (format.Bitrate.HasValue)
                description += " " + (int)(format.Bitrate / 1000) + " kbps";

            return new StreamDescription
            {
                Default = format.RoleFlags.HasFlag(RoleFlags.Main),
                Description = description,
                Id = format.Id,
                StreamType = stream
            };
        }

        public static IEnumerable<StreamDescription> GetStreamDescriptionsFromStreamType(this StreamGroup[] groups, StreamType type)
        {
            ContentType content = type.ToContentType();
            return groups
                .Where(group => group.ContentType == content)
                .SelectMany(group => group.Streams)
                .Select(format => format.Format.ToStreamDescription(type));
        }

        public static (StreamGroup group, IStreamSelector selector) SelectStream(this StreamGroup[] groups, StreamDescription targetStream)
        {
            ContentType type = targetStream.StreamType.ToContentType();

            var prospectGroups = groups
                .Where(group => group.ContentType == type)
                .Select(group => group);

            foreach (var prospect in prospectGroups)
            {
                var prospectStreams = prospect.Streams.Count;
                for (var streamIndex = 0; streamIndex < prospectStreams; streamIndex++)
                {
                    if (prospect.Streams[streamIndex].Format.Id.Equals(targetStream.Id))
                        return (prospect, new FixedStreamSelector(streamIndex));
                }
            }

            return default;
        }

        public static (StreamGroup[], IStreamSelector[]) UpdateSelection(
            this (StreamGroup[] groups, IStreamSelector[] selectors) currentSelection,
            (StreamGroup group, IStreamSelector selector) newSelection)
        {
            for (int i = 0; i < currentSelection.groups.Length; i++)
            {
                if (currentSelection.groups[i].ContentType == newSelection.group.ContentType)
                {
                    currentSelection.groups[i] = newSelection.group;
                    currentSelection.selectors[i] = newSelection.selector;
                }
            }

            return currentSelection;
        }

        public static StreamGroup[] DumpStreamGroups(this StreamGroup[] groups)
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
            Logger.Debug($"\tBitrate: '{format.Bitrate}'");
            Logger.Debug($"\tChannels: '{format.ChannelCount}'");
            Logger.Debug($"\tCodecs: '{format.Codecs}'");
            Logger.Debug($"\tContainer: '{format.ContainerMimeType}'");
            Logger.Debug($"\tFrameRate:'{format.FrameRate}'");
            Logger.Debug($"\tLanguage: '{format.Language}'");
            Logger.Debug($"\tSample: '{format.SampleMimeType}'");
            Logger.Debug($"\tWxH: '{format.Width}x{format.Height}'");
            Logger.Debug($"\tFramerate: '{format.FrameRate}'");
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
