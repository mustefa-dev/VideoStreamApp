
using Microsoft.AspNetCore.Mvc;
using VideoStreamApp.Models;
using VideoStreamApp.Service;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] RoomForm form)
    {
        if (string.IsNullOrWhiteSpace(form.HostName) || string.IsNullOrWhiteSpace(form.VideoUrl))
        {
            return BadRequest("Host name and video URL are required");
        }

        var room = await _roomService.CreateRoomAsync(form.HostName, form.VideoUrl);
        return Ok(room);
    }

    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetRoom(string roomId)
    {
        var room = await _roomService.GetRoomAsync(roomId);
        if (room == null)
        {
            return NotFound("Room not found");
        }

        return Ok(room);
    }

    [HttpDelete("{roomId}")]
    public async Task<IActionResult> DeleteRoom(string roomId)
    {
        var deleted = await _roomService.DeleteRoomAsync(roomId);
        if (!deleted)
        {
            return NotFound("Room not found");
        }

        return Ok("Room deleted successfully");
    }
}
