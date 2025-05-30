using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VideoStreamApp.Services;

namespace VideoStreamApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileUploadController : ControllerBase
    {
        private readonly IFileService _fileService;
        public FileUploadController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost]
        [Route("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file == null)
                return BadRequest("No file uploaded.");

            var path = await _fileService.SaveFileAsync(file);
            return Ok(new { path });
        }
    }
}

