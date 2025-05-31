namespace VideoStreamApp;

public class ChatMessage
{
    public required string Username { get; set; }
    public required string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
