using DailyBudgetAPI.Data;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DailyBudgetAPI.Models;
using Azure;

namespace DailyBudgetAPI.Services
{
    public class FIleStorageService : IFIleStorageService
    {
        private readonly BlobServiceClient _bsc;
        private readonly ApplicationDBContext _db;

        public FIleStorageService(ApplicationDBContext db, BlobServiceClient bsc)
        {
            _bsc = bsc;
            _db = db;
        }

        public async Task<string> Upload(Stream File, string FileName ,string Container, int UniqueId)
        {
            BlobContainerClient ContainerInstance = _bsc.GetBlobContainerClient(Container);
            string FileLocation = DateTime.UtcNow.Year.ToString() + "/" + DateTime.UtcNow.Month.ToString() + "/" + DateTime.UtcNow.Day.ToString() + "/" + DateTime.UtcNow.Hour.ToString() + DateTime.UtcNow.Minute.ToString() + DateTime.UtcNow.Second.ToString() + "_" + UniqueId + "_" + FileName ;
            BlobClient BlobInstance = ContainerInstance.GetBlobClient(FileLocation);

            try
            {
                await BlobInstance.UploadAsync(File);
            }
            catch 
            {
                return "UploadFailed";
            }

            return FileLocation;
        }

        public async Task<Stream> Download(string FileLocation, string Container, int UniqueId)
        {
            BlobContainerClient ContainerInstance = _bsc.GetBlobContainerClient(Container);
            BlobClient BlobInstance = ContainerInstance.GetBlobClient(FileLocation);
            BlobDownloadResult DownloadedContent = await BlobInstance.DownloadContentAsync();

            if(DownloadedContent == null) 
            {
                return null;    
            }

            return DownloadedContent.Content.ToStream();
        }


        public async Task DeleteFile(string FileLocation, string Container)
        {
            BlobContainerClient ContainerInstance = _bsc.GetBlobContainerClient(Container);
            BlobClient BlobInstance = ContainerInstance.GetBlobClient(FileLocation);

            await BlobInstance.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
        }

        public async Task<string> GetFileLocation(int UniqueID, string DatabaseTable)
        {
            string? FileLocation = "";

            switch(DatabaseTable)
            {
                case "ProfilePictureImage":
                    ProfilePictureImage? Image = _db.ProfilePictureImages.Where(p => p.UserID == UniqueID).OrderByDescending(p => p.Id).FirstOrDefault();
                    if (Image == null)
                    {
                        throw new Exception("File not found");
                    }    
                    FileLocation = Image.FileLocation;
                    break;
                default:
                    throw new Exception("Database table does not exist");
            }

            return FileLocation;
        }


        public async Task SaveFileLocation(string FileLocation, int UniqueID, string DatabaseTable, string Container)
        {
            switch (DatabaseTable)
            {
                case "ProfilePictureImage":

                    ProfilePictureImage? Image = _db.ProfilePictureImages.Where(p => p.UserID == UniqueID).OrderByDescending(p => p.Id).FirstOrDefault();

                    if (Image == null)
                    {
                        Image = new ProfilePictureImage
                        {
                            UserID = UniqueID,
                            FileLocation = FileLocation,
                            WhenAdded = DateTime.UtcNow
                            
                        };

                        _db.Add(Image);
                    }
                    else
                    {
                        await DeleteFile(Image.FileLocation, Container);

                        Image.FileLocation = FileLocation;
                        Image.WhenAdded = DateTime.UtcNow;

                        _db.Update(Image);
                    }

                    _db.SaveChanges();

                    break;
                default:
                    throw new Exception("Database table does not exist");
            }
        }

    }


}

