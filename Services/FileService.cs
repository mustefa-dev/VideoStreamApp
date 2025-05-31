

namespace BookStore.Services
{
    
    public interface IFileService {
        Task<(string? file , string? error)> Upload(IFormFile file);
        Task<(List<string>? files , string? error)> Upload(IFormFile[] files);
    }
    
    public class FileService  : IFileService
    {

        public async Task<(string? file , string? error)> Upload(IFormFile file) {
            var id = Guid.NewGuid();
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{id}{extension}";

            var attachmentsDir = Path.Combine(Directory.GetCurrentDirectory(),
                "wwwroot", "Attachments");
            if (!File.Exists(attachmentsDir)) Directory.CreateDirectory(attachmentsDir);


            var path = Path.Combine(attachmentsDir, fileName);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            var filePath = Path.Combine("Attachments", fileName);
            return (filePath , null);
        }
        public async Task<(List<string> files , string? error)> Upload(IFormFile[] files)
        {
            var fileList = new List<string>();
            foreach (var file in files)
            { 
                var fileToAdd = await Upload(file);
                fileList.Add(fileToAdd.file!);
            }
            return (fileList , null);
        }
    }
}