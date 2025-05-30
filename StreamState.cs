namespace VideoStreamApp;

public class StreamState
{
    public required string VideoUrl { get; set; }
    public double CurrentTime { get; set; }
    public bool IsPlaying { get; set; }
    public DateTime? StreamStartTime { get; set; }
    public int ViewerCount { get; set; } = 0;
    public required string HostConnectionId { get; set; }
}
