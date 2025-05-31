namespace VideoStreamApp.Models;

public class Room
{
    public string RoomId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsPlaying { get; set; }
    public double CurrentTime { get; set; }
}