using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace VideoStreamApp.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile file);
    }
}

