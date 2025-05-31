using Microsoft.AspNetCore.SignalR;
using VideoStreamApp.Service;

namespace VideoStreamApp.Hubs;

public class VideoHub : Hub
{
    private readonly IRoomService _roomService;

    public VideoHub(IRoomService roomService)
    {
        _roomService = roomService;
    }

    public async Task JoinRoom(string roomId, bool isHost)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await _roomService.AddViewerAsync(roomId, Context.ConnectionId);

        var viewerCount = await _roomService.GetViewerCountAsync(roomId);
        
        await Clients.Group(roomId).SendAsync("ViewerCountUpdate", viewerCount);

        if (!isHost)
        {
            await Clients.Group(roomId).SendAsync("RequestVideoState", Context.ConnectionId);
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await _roomService.RemoveViewerAsync(roomId, Context.ConnectionId);

        var viewerCount = await _roomService.GetViewerCountAsync(roomId);
        await Clients.Group(roomId).SendAsync("ViewerCountUpdate", viewerCount);
    }

    public async Task PlayVideo(string roomId, double currentTime)
    {
        await Clients.Group(roomId).SendAsync("VideoPlay", currentTime);
    }

    public async Task PauseVideo(string roomId, double currentTime)
    {
        await Clients.Group(roomId).SendAsync("VideoPause", currentTime);
    }

    public async Task SeekVideo(string roomId, double currentTime)
    {
        await Clients.Group(roomId).SendAsync("VideoSeek", currentTime);
    }

    public async Task SyncVideo(string roomId, double currentTime, bool isPlaying)
    {
        await Clients.Group(roomId).SendAsync("VideoSync", currentTime, isPlaying);
    }

    public async Task SendVideoState(string roomId, string requesterId, double currentTime, bool isPlaying)
    {
        await Clients.Client(requesterId).SendAsync("VideoState", currentTime, isPlaying);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}