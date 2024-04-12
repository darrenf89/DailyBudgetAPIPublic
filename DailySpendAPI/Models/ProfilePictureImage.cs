using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class ProfilePictureImage
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int UserID { get; set; }
        [Required]
        [MaxLength(200)]
        public string FileLocation { get; set; }
        [Required]
        public DateTime WhenAdded { get; set; } = DateTime.UtcNow;
        [NotMapped]
        public Stream File { get; set; }
        [NotMapped]
        public string FileName { get; set; }
    }

    

}
