using System.Collections.Concurrent;
using System.Timers;
using Microsoft.AspNetCore.SignalR;

namespace VideoStreamApp.Hubs
{
    public class MovieHub : Hub
    {
        private static Dictionary<string, (
            string HostName,
            string VideoUrl,
            string HostConnectionId,
            List<(string Name, string ConnectionId)> Viewers,
            byte[]? SubtitleData,
            string? SubtitleFileName
            )> _streams = new();

        private static ConcurrentDictionary<string, System.Timers.Timer> _cleanupTimers = new();
        private static ConcurrentDictionary<string, (string Action, double Time)> _playbackStates = new();
        private static Random _rand = new();

        private static ConcurrentDictionary<string, List<(string Name, string Message, DateTime Timestamp)>>
            _chatMessages = new();

        public async Task CreateSession(string name, string videoUrl)
        {
            var streamId = GenerateStreamId();
            _streams[streamId] = (name, videoUrl, Context.ConnectionId, new(), null, null);
            CancelCleanup(streamId);
            await Clients.Caller.SendAsync("Created", streamId, videoUrl);
        }

        public async Task JoinSession(string name, string streamId)
        {
            if (!_streams.TryGetValue(streamId, out var session))
            {
                await Clients.Caller.SendAsync("Error", "Stream not found");
                return;
            }

            session.Viewers.Add((name, Context.ConnectionId));
            _streams[streamId] = session;
            CancelCleanup(streamId);
            await Clients.Caller.SendAsync("Joined", streamId, session.VideoUrl, session.HostName);
        }

        public async Task UploadSubtitle(string streamId, string base64SubtitleData, string fileName)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;
            if (session.HostConnectionId != Context.ConnectionId) return;

            byte[] subtitleData = Convert.FromBase64String(base64SubtitleData);

            var updatedSession = (
                session.HostName,
                session.VideoUrl,
                session.HostConnectionId,
                session.Viewers,
                subtitleData,
                fileName
            );

            _streams[streamId] = updatedSession;
        }

        public async Task FetchSubtitle(string streamId)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;
            if (session.SubtitleData == null) return;

            string base64Data = Convert.ToBase64String(session.SubtitleData);
            await Clients.Caller.SendAsync("SubtitleData", base64Data, session.SubtitleFileName);
        }

        public async Task VideoAction(string streamId, string action, double time)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;

            bool isParticipant = session.HostConnectionId == Context.ConnectionId ||
                                 session.Viewers.Any(v => v.ConnectionId == Context.ConnectionId);
            if (!isParticipant) return;

            _playbackStates[streamId] = (action, time);

            // Send to all participants, host + viewers
            await Clients.Client(session.HostConnectionId).SendAsync("Action", action, time);
            foreach (var viewer in session.Viewers)
            {
                await Clients.Client(viewer.ConnectionId).SendAsync("Action", action, time);
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var toRemove = _streams.FirstOrDefault(s => s.Value.HostConnectionId == Context.ConnectionId);
            if (!string.IsNullOrEmpty(toRemove.Key))
            {
                foreach (var viewer in toRemove.Value.Viewers)
                {
                    await Clients.Client(viewer.ConnectionId).SendAsync("Ended");
                }

                StartCleanupTimer(toRemove.Key);
            }
            else
            {
                foreach (var key in _streams.Keys.ToList())
                {
                    var session = _streams[key];
                    session.Viewers.RemoveAll(v => v.ConnectionId == Context.ConnectionId);
                    _streams[key] = session;
                    if (session.HostConnectionId == null && session.Viewers.Count == 0)
                    {
                        StartCleanupTimer(key);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private void StartCleanupTimer(string streamId)
        {
            if (_cleanupTimers.ContainsKey(streamId)) return;
            var timer = new System.Timers.Timer(2 * 60 * 1000);
            timer.Elapsed += (object? s, ElapsedEventArgs e) =>
            {
                _streams.Remove(streamId);
                _cleanupTimers.TryRemove(streamId, out _);
                timer.Dispose();
            };
            timer.AutoReset = false;
            timer.Start();
            _cleanupTimers[streamId] = timer;
        }

        private void CancelCleanup(string streamId)
        {
            if (_cleanupTimers.TryRemove(streamId, out var timer))
            {
                timer.Stop();
                timer.Dispose();
            }
        }

        private string GenerateStreamId()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[_rand.Next(s.Length)]).ToArray());
        }

        public async Task ReconnectAsHost(string name, string streamId, string videoUrl)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;

            var updatedSession = (
                session.HostName,
                session.VideoUrl,
                Context.ConnectionId,
                session.Viewers,
                session.SubtitleData,
                session.SubtitleFileName
            );

            _streams[streamId] = updatedSession;
            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
        }

        public async Task ReconnectAsViewer(string name, string streamId)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;

            var viewerIndex = session.Viewers.FindIndex(v => v.Name == name);
            if (viewerIndex == -1)
            {
                session.Viewers.Add((name, Context.ConnectionId));
            }
            else
            {
                session.Viewers[viewerIndex] = (name, Context.ConnectionId);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
            await Clients.Caller.SendAsync("Joined", streamId, session.VideoUrl, session.HostName);
        }

        public async Task GetCurrentPlaybackState(string streamId)
        {
            if (_playbackStates.TryGetValue(streamId, out var state))
            {
                await Clients.Caller.SendAsync("PlaybackState", (object)state.Action, (object)state.Time);
            }
        }

        // CHAT FEATURE: Add SendMessage method for chat support
        public async Task SendMessage(string streamId, string name, string message)
        {
            if (!_streams.ContainsKey(streamId)) return;

            var timestamp = DateTime.UtcNow;

            // Optional: Store messages in history for fetch later if you want
            _chatMessages.AddOrUpdate(
                streamId,
                _ => new List<(string, string, DateTime)> { (name, message, timestamp) },
                (_, list) =>
                {
                    list.Add((name, message, timestamp));
                    return list;
                }
            );

            // Send to host and all viewers (including sender)
            var session = _streams[streamId];
            var allConnectionIds = new List<string> { session.HostConnectionId };
            allConnectionIds.AddRange(session.Viewers.Select(v => v.ConnectionId));

            foreach (var connId in allConnectionIds.Distinct())
            {
                await Clients.Client(connId).SendAsync("Chat", name, message, timestamp);
            }
        }

        public async Task GetChatHistory(string streamId)
        {
            if (_chatMessages.TryGetValue(streamId, out var list))
            {
                await Clients.Caller.SendAsync("ChatHistory", list.Select(msg =>
                    new { name = msg.Name, message = msg.Message, timestamp = msg.Timestamp }
                ));
            }
            else
            {
                await Clients.Caller.SendAsync("ChatHistory", Array.Empty<object>());
            }
        }
    }
}