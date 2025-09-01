using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsMediaController;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GorillaInfoMediaProcess
{
    class Program
    {
        static MediaManager mediaManager;
        static readonly object _writeLock = new object();

        public static async Task Main()
        {
            bool hasQuitApplication = false;

            Thread inputThread = new Thread(() =>
            {
                string line;
                while ((line = Console.ReadLine()) != null)
                {
                    if (line.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        hasQuitApplication = true;
                        break;
                    }
                }
            })
            {
                IsBackground = true
            };
            inputThread.Start();

            mediaManager = new MediaManager()
            {
                Logger = BuildLogger("MediaManager"),
            };

            mediaManager.OnAnySessionOpened += MediaManager_OnAnySessionOpened;
            mediaManager.OnAnySessionClosed += MediaManager_OnAnySessionClosed;
            mediaManager.OnFocusedSessionChanged += MediaManager_OnFocusedSessionChanged;
            mediaManager.OnAnyPlaybackStateChanged += MediaManager_OnAnyPlaybackStateChanged;
            mediaManager.OnAnyMediaPropertyChanged += MediaManager_OnAnyMediaPropertyChanged;
            mediaManager.OnAnyTimelinePropertyChanged += MediaManager_OnAnyTimelinePropertyChanged;

            mediaManager.Start();

            while (!hasQuitApplication) await Task.Yield();

            mediaManager.Dispose();
        }

        private static void MediaManager_OnAnySessionOpened(MediaManager.MediaSession session)
        {
            var data = new Dictionary<string, object>
            {
                {
                    "EventName",
                    "AddSession"
                },
                {
                    "SessionId",
                    session.Id
                }
            };
            WriteLineColor(JsonSerializer.Serialize(data), ConsoleColor.Green);
        }
        private static void MediaManager_OnAnySessionClosed(MediaManager.MediaSession session)
        {
            var data = new Dictionary<string, object>
            {
                {
                    "EventName",
                    "RemoveSession"
                },
                {
                    "SessionId",
                    session.Id
                }
            };
            WriteLineColor(JsonSerializer.Serialize(data), ConsoleColor.Red);
        }

        private static void MediaManager_OnFocusedSessionChanged(MediaManager.MediaSession mediaSession)
        {
            var data = new Dictionary<string, object>
            {
                {
                    "EventName",
                    "SessionFocusChanged"
                },
                {
                    "SessionId",
                    mediaSession?.ControlSession?.SourceAppUserModelId
                }
            };
            WriteLineColor(JsonSerializer.Serialize(data), ConsoleColor.Gray);
        }

        private static void MediaManager_OnAnyPlaybackStateChanged(MediaManager.MediaSession sender, GlobalSystemMediaTransportControlsSessionPlaybackInfo args)
        {
            var data = new Dictionary<string, object>
            {
                {
                    "EventName",
                    "PlaybackStateChanged"
                },
                {
                    "SessionId",
                    sender.Id
                },
                {
                    "PlaybackStatus",
                    args.PlaybackStatus.ToString()
                }
            };
            WriteLineColor(JsonSerializer.Serialize(data), ConsoleColor.Yellow);
        }

        private static async void MediaManager_OnAnyMediaPropertyChanged(MediaManager.MediaSession sender, GlobalSystemMediaTransportControlsSessionMediaProperties args)
        {
            var data = new Dictionary<string, object>
            {
                {
                    "EventName",
                    "MediaPropertyChanged"
                },
                {
                    "SessionId",
                    sender.Id
                },
                {
                    "Title",
                    args.Title
                },
                {
                    "Artist",
                    args.Artist
                },
                {
                    "Genres",
                    args.Genres.ToArray()
                },
                {
                    "TrackNumber",
                    args.TrackNumber
                },
                {
                    "AlbumTitle",
                    args.AlbumTitle
                },
                {
                    "AlbumArtist",
                    args.AlbumArtist
                },
                {
                    "AlbumTrackCount",
                    args.AlbumTrackCount
                }
            };

            string base64String = string.Empty;

            if (args.Thumbnail != null)
            {
                IRandomAccessStream randomAccessStream = await args.Thumbnail.OpenReadAsync();
                MemoryStream memoryStream = new MemoryStream();
                await randomAccessStream.AsStreamForRead().CopyToAsync(memoryStream);
                base64String = Convert.ToBase64String(memoryStream.ToArray());
                randomAccessStream.Dispose();
                memoryStream.Dispose();
            }

            data.Add("Thumbnail", base64String);

            WriteLineColor(JsonSerializer.Serialize(data), ConsoleColor.Cyan);
        }

        private static void MediaManager_OnAnyTimelinePropertyChanged(MediaManager.MediaSession sender, GlobalSystemMediaTransportControlsSessionTimelineProperties args)
        {
            var data = new Dictionary<string, object>
            {
                {
                    "EventName",
                    "TimelinePropertyChanged"
                },
                {
                    "SessionId",
                    sender.Id
                },
                {
                    "StartTime",
                    args.StartTime.TotalSeconds
                },
                {
                    "Position",
                    args.Position.TotalSeconds
                },
                {
                    "EndTime",
                    args.EndTime.TotalSeconds
                }
            };

            WriteLineColor(JsonSerializer.Serialize(data), ConsoleColor.Magenta);
        }

        public static void WriteLineColor(object toprint, ConsoleColor color = ConsoleColor.White)
        {
            lock (_writeLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(/*"[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + */toprint);
                Console.Out.Flush();
            }
        }

        private static Microsoft.Extensions.Logging.ILogger BuildLogger(string sourceContext = null)
        {
            return new LoggerFactory().AddSerilog(logger: new LoggerConfiguration()
                    .MinimumLevel.Is(LogEventLevel.Information)
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] " + (sourceContext ?? "{SourceContext}") + ": {Message:lj}{NewLine}{Exception}")
                    .CreateLogger())
                    .CreateLogger(string.Empty);
        }
    }
}
