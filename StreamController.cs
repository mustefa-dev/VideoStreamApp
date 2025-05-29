using Microsoft.AspNetCore.Mvc;
using System;

[ApiController]
[Route("api/stream")]
public class StreamController : ControllerBase
{
    [HttpGet("generate")]
    public IActionResult Generate()
    {
        string streamId = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        return Ok(new { streamId });
    }
}