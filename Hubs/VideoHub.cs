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
            string? SubtitleFileName,
            List<ChatMessage> ChatHistory
            )> _streams = new();

        private static ConcurrentDictionary<string, System.Timers.Timer> _cleanupTimers = new();
        private static ConcurrentDictionary<string, (string Action, double Time)> _playbackStates = new();
        private static Random _rand = new();

        // Chat message model
        public class ChatMessage
        {
            public string Sender { get; set; } = "";
            public string? Text { get; set; } // Text content (emojis included)
            public long Timestamp { get; set; } // Unix milliseconds
            public string? AudioUrl { get; set; } // For voice messages
            public double? AudioDuration { get; set; }
            public string Type { get; set; } = "text"; // "text" or "audio"
            public string? Id { get; set; }
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

        public async Task CreateSession(string name, string videoUrl)
        {
            var streamId = GenerateStreamId();
            _streams[streamId] = (name, videoUrl, Context.ConnectionId, new(), null, null, new List<ChatMessage>());
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

            // Send chat history after joining
            if (session.ChatHistory != null && session.ChatHistory.Count > 0)
            {
                await Clients.Caller.SendAsync("ChatHistory", session.ChatHistory);
            }
        }

        public async Task SendMessage(string streamId, string sender, string text)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Sender = sender,
                Text = text,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Type = "text"
            };
            session.ChatHistory.Add(msg);
            _streams[streamId] = session;

            await Clients.Client(session.HostConnectionId).SendAsync("ReceiveMessage", msg);
            foreach (var viewer in session.Viewers)
            {
                await Clients.Client(viewer.ConnectionId).SendAsync("ReceiveMessage", msg);
            }
        }

        public async Task SendVoiceMessage(string streamId, string sender, string base64Audio, double audioDuration)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;

            // For demo, we store byte[] in-memory and generate a fake URL (in production, store to persistent storage)
            var bytes = Convert.FromBase64String(base64Audio);
            string audioUrl = $"data:audio/wav;base64,{base64Audio}";
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Sender = sender,
                AudioUrl = audioUrl,
                AudioDuration = audioDuration,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Type = "audio"
            };
            session.ChatHistory.Add(msg);
            _streams[streamId] = session;

            await Clients.Client(session.HostConnectionId).SendAsync("ReceiveMessage", msg);
            foreach (var viewer in session.Viewers)
            {
                await Clients.Client(viewer.ConnectionId).SendAsync("ReceiveMessage", msg);
            }
        }

        public async Task FetchChatHistory(string streamId)
        {
            if (!_streams.TryGetValue(streamId, out var session)) return;
            await Clients.Caller.SendAsync("ChatHistory", session.ChatHistory);
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
                fileName,
                session.ChatHistory
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

            await Clients.Client(session.HostConnectionId).SendAsync("Action", action, time);
            foreach (var viewer in session.Viewers)
            {
                await Clients.Client(viewer.ConnectionId).SendAsync("Action", action, time);
            }
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
                session.SubtitleFileName,
                session.ChatHistory
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

            // On reconnect, send chat history as well
            if (session.ChatHistory != null && session.ChatHistory.Count > 0)
            {
                await Clients.Caller.SendAsync("ChatHistory", session.ChatHistory);
            }
        }

        public async Task GetCurrentPlaybackState(string streamId)
        {
            if (_playbackStates.TryGetValue(streamId, out var state))
            {
                await Clients.Caller.SendAsync("PlaybackState", (object)state.Action, (object)state.Time);
            }
        }
    }
    
}
