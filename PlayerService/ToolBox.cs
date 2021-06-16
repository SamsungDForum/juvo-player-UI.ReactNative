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

        public static StreamDescription ToStreamDescription(this Format format, StreamType stream)
        {
            string description;

            switch (stream)
            {
                case StreamType.Video:
                    description = $"{format.Width}x{format.Height} {format.Label}";
                    if (string.IsNullOrWhiteSpace(description))
                        description = "Video " + format.Id;

                    return new StreamDescription
                    {
                        Default = format.RoleFlags.HasFlag(RoleFlags.Main),
                        Description = description,
                        Id = format.Id,
                        StreamType = stream
                    };

                case StreamType.Audio:
                    description = $"{format.Language} {format.ChannelCount} {format.Label}";
                    if (string.IsNullOrWhiteSpace(description))
                        description = "Audio " + format.Id;

                    return new StreamDescription
                    {
                        Default = format.RoleFlags.HasFlag(RoleFlags.Main),
                        Description = description,
                        Id = format.Id,
                        StreamType = stream
                    };

                default:
                    description = format.Label;
                    if (string.IsNullOrWhiteSpace(description))
                        description = $"{stream} {format.Id}";

                    return new StreamDescription
                    {
                        Default = format.RoleFlags.HasFlag(RoleFlags.Main),
                        Description = description,
                        Id = format.Id,
                        StreamType = stream
                    };

            }
        }

        public static IEnumerable<StreamDescription> GetStreamDescriptionsFromStreamType(this StreamGroup[] groups,
            StreamType type)
        {
            ContentType content = type.ToContentType();
            return groups
                .Where(group => group.ContentType == content)
                .SelectMany(group => group.Streams)
                .Select(format => format.Format.ToStreamDescription(type));
        }

        public static (StreamGroup group, IStreamSelector selector) SelectStream(this StreamGroup[] groups, ContentType type, string id)
        {
            StreamGroup selectedContent = groups.FirstOrDefault(group => group.ContentType == type);

            if (selectedContent?.Streams.Count != selectedContent?.Streams.Select(stream => stream.Format.Id).Distinct().Count())
                Logger.Warn("Stream Format IDs are not unique. Stream selection may not be accurate");

            int index = selectedContent?.Streams.IndexOf(
                selectedContent.Streams.FirstOrDefault(stream => stream.Format.Id == id)) ?? -1;

            return (selectedContent, index == -1 ? null : new FixedStreamSelector(index));
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
    }
}
