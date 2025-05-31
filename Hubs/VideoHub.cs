using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using VideoStreamApp.Service;

namespace VideoStreamApp.Hubs;

public class VideoHub : Hub
{
    private readonly IRoomService _roomService;
    private static ConcurrentDictionary<string, string> RoomHosts = new();

    public VideoHub(IRoomService roomService)
    {
        _roomService = roomService;
    }

    public async Task JoinRoom(string roomId, bool isHost)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await _roomService.AddViewerAsync(roomId, Context.ConnectionId);

        if (isHost)
        {
            RoomHosts[roomId] = Context.ConnectionId;
        }

        var viewerCount = await _roomService.GetViewerCountAsync(roomId);
        await Clients.Group(roomId).SendAsync("ViewerCountUpdate", viewerCount);

        if (!isHost)
        {
            if (RoomHosts.TryGetValue(roomId, out var hostConnId))
            {
                await Clients.Client(hostConnId).SendAsync("RequestVideoState", Context.ConnectionId);
            }
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await _roomService.RemoveViewerAsync(roomId, Context.ConnectionId);

        if (RoomHosts.TryGetValue(roomId, out var hostConnId) && hostConnId == Context.ConnectionId)
        {
            RoomHosts.TryRemove(roomId, out _);
        }

        var viewerCount = await _roomService.GetViewerCountAsync(roomId);
        await Clients.Group(roomId).SendAsync("ViewerCountUpdate", viewerCount);
    }

    public async Task UpdatePlayback(string roomId, bool isPlaying, double currentTime)
    {
        if (RoomHosts.TryGetValue(roomId, out var hostConnId) && hostConnId == Context.ConnectionId)
        {
            await Clients.Group(roomId).SendAsync("ReceivePlaybackUpdate", isPlaying, currentTime);
        }
    }

    // Host responds to viewer's request for current state
    public async Task SendVideoState(string roomId, string requesterId, double currentTime, bool isPlaying)
    {
        // Only allow the host to respond
        if (RoomHosts.TryGetValue(roomId, out var hostConnId) && hostConnId == Context.ConnectionId)
        {
            await Clients.Client(requesterId).SendAsync("ReceivePlaybackUpdate", isPlaying, currentTime);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var kvp in RoomHosts)
        {
            if (kvp.Value == Context.ConnectionId)
            {
                RoomHosts.TryRemove(kvp.Key, out _);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
    
}