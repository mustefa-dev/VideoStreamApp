using System.Collections.Concurrent;
using VideoStreamApp.Models;

namespace VideoStreamApp.Service;

public interface IRoomService
{
    Task<Room> CreateRoomAsync(string hostName, string videoUrl);
    Task<Room?> GetRoomAsync(string roomId);
    Task<bool> DeleteRoomAsync(string roomId);
    Task AddViewerAsync(string roomId, string connectionId);
    Task RemoveViewerAsync(string roomId, string connectionId);
    Task<int> GetViewerCountAsync(string roomId);
}

public class RoomService : IRoomService
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _roomViewers = new();

    public Task<Room> CreateRoomAsync(string hostName, string videoUrl)
    {
        var roomId = GenerateRoomId();
        var room = new Room
        {
            RoomId = roomId,
            HostName = hostName,
            VideoUrl = videoUrl,
            CreatedAt = DateTime.UtcNow,
            IsPlaying = false,
            CurrentTime = 0
        };

        _rooms[roomId] = room;
        _roomViewers[roomId] = new HashSet<string>();
        
        return Task.FromResult(room);
    }

    public Task<Room?> GetRoomAsync(string roomId)
    {
        _rooms.TryGetValue(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<bool> DeleteRoomAsync(string roomId)
    {
        var removed = _rooms.TryRemove(roomId, out _);
        _roomViewers.TryRemove(roomId, out _);
        return Task.FromResult(removed);
    }

    public Task AddViewerAsync(string roomId, string connectionId)
    {
        _roomViewers.AddOrUpdate(roomId, 
            new HashSet<string> { connectionId },
            (key, existing) => { existing.Add(connectionId); return existing; });
        return Task.CompletedTask;
    }

    public Task RemoveViewerAsync(string roomId, string connectionId)
    {
        if (_roomViewers.TryGetValue(roomId, out var viewers))
        {
            viewers.Remove(connectionId);
        }
        return Task.CompletedTask;
    }

    public Task<int> GetViewerCountAsync(string roomId)
    {
        var count = _roomViewers.TryGetValue(roomId, out var viewers) ? viewers.Count : 0;
        return Task.FromResult(count);
    }

    private static string GenerateRoomId()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpper();
    }
}