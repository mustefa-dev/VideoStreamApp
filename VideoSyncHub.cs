using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class StreamState
{
    public string VideoUrl { get; set; }
    public double CurrentTime { get; set; }
    public bool IsPlaying { get; set; }
}

public class VideoSyncHub : Hub
{
    private static ConcurrentDictionary<string, StreamState> streamStates = new();
    private static ConcurrentDictionary<string, string> hostConnections = new();

    public async Task CreateStream(string streamId, string videoUrl)
    {
        streamStates[streamId] = new StreamState {
            VideoUrl = videoUrl,
            CurrentTime = 0,
            IsPlaying = false
        };
        hostConnections[streamId] = Context.ConnectionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
    }

    public async Task JoinStream(string streamId)
    {
        if (streamStates.TryGetValue(streamId, out var state))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
            await Clients.Caller.SendAsync("ReceiveStreamState", 
                state.VideoUrl, 
                state.CurrentTime, 
                state.IsPlaying
            );
        }
    }

    public async Task SendControl(string streamId, string action, double timestamp, bool isPlaying)
    {
        if (streamStates.TryGetValue(streamId, out var state))
        {
            state.CurrentTime = timestamp;
            state.IsPlaying = isPlaying;
            
            await Clients.GroupExcept(streamId, Context.ConnectionId)
                .SendAsync("ReceiveControl", action, timestamp, isPlaying);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var hostEntry = hostConnections.FirstOrDefault(x => x.Value == Context.ConnectionId);
        if (!string.IsNullOrEmpty(hostEntry.Key))
        {
            streamStates.TryRemove(hostEntry.Key, out _);
            hostConnections.TryRemove(hostEntry.Key, out _);
        }
        await base.OnDisconnectedAsync(exception);
    }
}