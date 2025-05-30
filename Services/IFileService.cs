namespace VideoStreamApp.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile file);
    }
}

