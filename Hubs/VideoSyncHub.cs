using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace VideoStreamApp.Hubs;

public class VideoSyncHub : Hub
{
    private static ConcurrentDictionary<string, StreamState> streamStates = new();
    private static ConcurrentDictionary<string, List<ChatMessage>> streamChats = new();
    private static ConcurrentDictionary<string, HashSet<string>> streamViewers = new();

    public async Task CreateStream(string videoUrl)
    {
        var streamId = Guid.NewGuid().ToString("N")[..6];

        var state = new StreamState
        {
            VideoUrl = videoUrl,
            CurrentTime = 0,
            IsPlaying = false,
            HostConnectionId = Context.ConnectionId
        };

        streamStates[streamId] = state;
        streamChats[streamId] = new List<ChatMessage>();
        streamViewers[streamId] = new HashSet<string> { Context.ConnectionId };

        await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
        await Clients.Caller.SendAsync("StreamCreated", streamId);
    }

    public async Task JoinStream(string streamId, string username)
    {
        if (!streamStates.TryGetValue(streamId, out var state))
        {
            await Clients.Caller.SendAsync("StreamError", "Stream not found");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, streamId);

        if (streamViewers.TryGetValue(streamId, out var viewers))
        {
            viewers.Add(Context.ConnectionId);
            state.ViewerCount = viewers.Count - 1; 
        }

        var offsetSeconds = state.StreamStartTime.HasValue
            ? (DateTime.UtcNow - state.StreamStartTime.Value).TotalSeconds
            : 0;

        await Clients.Caller.SendAsync("ReceiveStreamState",
            state.VideoUrl,
            state.CurrentTime,
            state.IsPlaying,
            state.ViewerCount,
            offsetSeconds
        );

        if (streamChats.TryGetValue(streamId, out var chatHistory))
        {
            await Clients.Caller.SendAsync("ReceiveChatHistory", chatHistory);
        }

        await Clients.Group(streamId).SendAsync("ViewerCountChanged", state.ViewerCount);
        await Clients.Group(streamId).SendAsync("ViewerJoined", username);
    }

    public async Task SendControl(string streamId, string action, double timestamp, bool isPlaying)
    {
        if (!streamStates.TryGetValue(streamId, out var state))
        {
            await Clients.Caller.SendAsync("StreamError", "Stream not found");
            return;
        }

        if (state.HostConnectionId != Context.ConnectionId)
        {
            await Clients.Caller.SendAsync("StreamError", "Only the host can control the stream");
            return;
        }

        state.CurrentTime = timestamp;
        state.IsPlaying = isPlaying;

        // If video just started playing, set the stream start time
        if (isPlaying && action == "play" && state.StreamStartTime == null)
        {
            state.StreamStartTime = DateTime.UtcNow;
        }

        await Clients.GroupExcept(streamId, Context.ConnectionId)
            .SendAsync("ReceiveControl", action, timestamp, isPlaying);
    }

    public async Task SendChatMessage(string streamId, string username, string message)
    {
        if (!streamStates.ContainsKey(streamId)) return;

        var chatMessage = new ChatMessage
        {
            Username = username,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        if (streamChats.TryGetValue(streamId, out var chatHistory))
        {
            chatHistory.Add(chatMessage);
            if (chatHistory.Count > 100)
            {
                streamChats[streamId] = chatHistory.Skip(chatHistory.Count - 100).ToList();
            }
        }

        await Clients.Group(streamId).SendAsync("ReceiveChatMessage", chatMessage);
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? hostedStreamId = null;

        foreach (var kvp in streamStates)
        {
            if (kvp.Value.HostConnectionId == Context.ConnectionId)
            {
                hostedStreamId = kvp.Key;
                break;
            }
        }

        if (!string.IsNullOrEmpty(hostedStreamId))
        {
            await Clients.Group(hostedStreamId).SendAsync("StreamEnded", "Host disconnected");

            streamStates.TryRemove(hostedStreamId, out _);
            streamChats.TryRemove(hostedStreamId, out _);
            streamViewers.TryRemove(hostedStreamId, out _);
        }
        else
        {
            foreach (var stream in streamViewers)
            {
                if (stream.Value.Remove(Context.ConnectionId))
                {
                    if (streamStates.TryGetValue(stream.Key, out var state))
                    {
                        state.ViewerCount = stream.Value.Count - 1;
                        await Clients.Group(stream.Key).SendAsync("ViewerCountChanged", state.ViewerCount);
                    }
                    break;
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}