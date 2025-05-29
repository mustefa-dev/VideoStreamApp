using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
public class StreamState
{
    public string VideoUrl { get; set; }
    public double CurrentTime { get; set; }
    public bool IsPlaying { get; set; }
    public DateTime StreamStartTime { get; set; }
    public int ViewerCount { get; set; } = 0;
    public string HostConnectionId { get; set; }
}

public class ChatMessage
{
    public string Username { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}

public class VideoSyncHub : Hub
{
    private static ConcurrentDictionary<string, StreamState> streamStates = new();
    private static ConcurrentDictionary<string, List<ChatMessage>> streamChats = new();
    private static ConcurrentDictionary<string, HashSet<string>> streamViewers = new();

    public async Task CreateStream(string streamId, string videoUrl)
    {
        streamStates[streamId] = new StreamState {
            VideoUrl = videoUrl,
            CurrentTime = 0,
            IsPlaying = false,
            StreamStartTime = DateTime.UtcNow,
            HostConnectionId = Context.ConnectionId
        };

        streamChats[streamId] = new List<ChatMessage>();
        streamViewers[streamId] = new HashSet<string> { Context.ConnectionId };

        await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
        await Clients.Caller.SendAsync("StreamCreated", streamId);
    }

    public async Task JoinStream(string streamId, string username)
    {
        if (streamStates.TryGetValue(streamId, out var state))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);

            if (streamViewers.TryGetValue(streamId, out var viewers))
            {
                viewers.Add(Context.ConnectionId);
                state.ViewerCount = viewers.Count - 1; 
            }

            await Clients.Caller.SendAsync("ReceiveStreamState",
                state.VideoUrl,
                state.CurrentTime,
                state.IsPlaying,
                state.ViewerCount,
                (DateTime.UtcNow - state.StreamStartTime).TotalSeconds
            );

            if (streamChats.TryGetValue(streamId, out var chatHistory))
            {
                await Clients.Caller.SendAsync("ReceiveChatHistory", chatHistory);
            }

            await Clients.Group(streamId).SendAsync("ViewerCountChanged", state.ViewerCount);

            await Clients.Group(streamId).SendAsync("ViewerJoined", username);
        }
        else
        {
            await Clients.Caller.SendAsync("StreamError", "Stream not found");
        }
    }

    public async Task SendControl(string streamId, string action, double timestamp, bool isPlaying)
    {
        if (streamStates.TryGetValue(streamId, out var state))
        {
            if (state.HostConnectionId != Context.ConnectionId)
            {
                await Clients.Caller.SendAsync("StreamError", "Only the host can control the stream");
                return;
            }

            state.CurrentTime = timestamp;
            state.IsPlaying = isPlaying;

            await Clients.GroupExcept(streamId, Context.ConnectionId)
                .SendAsync("ReceiveControl", action, timestamp, isPlaying);
        }
    }

    public async Task SendChatMessage(string streamId, string username, string message)
    {
        if (streamStates.ContainsKey(streamId))
        {
            var chatMessage = new ChatMessage
            {
                Username = username,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            if (streamChats.TryGetValue(streamId, out var chatHistory))
            {
                chatHistory.Add(chatMessage);

                // Keep only the last 100 messages
                if (chatHistory.Count > 100)
                {
                    chatHistory = chatHistory.Skip(chatHistory.Count - 100).ToList();
                    streamChats[streamId] = chatHistory;
                }
            }

            await Clients.Group(streamId).SendAsync("ReceiveChatMessage", chatMessage);
        }
    }

    public async Task RequestStreamQuality(string streamId)
    {
        if (streamStates.TryGetValue(streamId, out var state) &&
            Context.ConnectionId == state.HostConnectionId)
        {
            await Clients.Group(streamId).SendAsync("StreamQualityCheck");
        }
    }

    public async Task ReportStreamQuality(string streamId, double latency, string connectionQuality)
    {
        if (streamStates.ContainsKey(streamId))
        {
            if (streamStates.TryGetValue(streamId, out var state))
            {
                await Clients.Client(state.HostConnectionId).SendAsync(
                    "ReceiveQualityReport",
                    Context.ConnectionId,
                    latency,
                    connectionQuality
                );
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var hostedStream = streamStates.FirstOrDefault(x => x.Value.HostConnectionId == Context.ConnectionId);
        if (!string.IsNullOrEmpty(hostedStream.Key))
        {
            await Clients.Group(hostedStream.Key).SendAsync("StreamEnded", "Host disconnected");

            streamStates.TryRemove(hostedStream.Key, out _);
            streamChats.TryRemove(hostedStream.Key, out _);
            streamViewers.TryRemove(hostedStream.Key, out _);
        }
        else
        {
            foreach (var stream in streamViewers)
            {
                if (stream.Value.Contains(Context.ConnectionId))
                {
                    stream.Value.Remove(Context.ConnectionId);

                    if (streamStates.TryGetValue(stream.Key, out var state))
                    {
                        state.ViewerCount = stream.Value.Count - 1; 

                        await Clients.Group(stream.Key).SendAsync("ViewerCountChanged", state.ViewerCount);
                    }
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
