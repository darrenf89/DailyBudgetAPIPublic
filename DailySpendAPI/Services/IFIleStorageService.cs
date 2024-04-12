
namespace DailyBudgetAPI.Services
{
    public interface IFIleStorageService
    {
        public Task<string> Upload(Stream File, string FileName, string Container, int UniqueId);
        public Task<Stream> Download(string FileLocation, string Container, int UniqueId);
        public Task DeleteFile(string FIleLocation, string Container);
        public Task<string> GetFileLocation(int UniqueID, string DatabaseTable);
        public Task SaveFileLocation(string FileLocation, int UniqueID, string DatabaseTable, string Container);
    }
}
