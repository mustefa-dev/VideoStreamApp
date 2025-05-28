using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

public class VideoStreamHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
        if (exception != null)
        {
            Console.WriteLine($"Disconnection error: {exception.Message}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinAsStreamer(string streamId)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
            await Clients.Caller.SendAsync("StreamerJoined");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JoinAsStreamer error: {ex.Message}");
            throw;
        }
    }

    public async Task JoinAsViewer(string streamId)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
            await Clients.Group(streamId).SendAsync("ViewerJoined", streamId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JoinAsViewer error: {ex.Message}");
            await Clients.Caller.SendAsync("StreamNotFound", streamId);
        }
    }

    public async Task SendVideoChunk(string streamId, string chunk)
    {
        try
        {
            await Clients.Group(streamId).SendAsync("ReceiveVideoChunk", chunk);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SendVideoChunk error: {ex.Message}");
            throw;
        }
    }

    public async Task ControlPlayback(string streamId, string action, double? seekTime = null)
    {
        try
        {
            await Clients.Group(streamId).SendAsync("PlaybackControl", action, seekTime);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ControlPlayback error: {ex.Message}");
            throw;
        }
    }
}
