using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class FirebaseDevices
    {
        [Key]
        public int FirebaseDeviceID { get; set; }
        [MaxLength(250)]
        public string? FirebaseToken { get; set; }
        [MaxLength(40)]
        public string? DeviceName { get; set; }
        [MaxLength(40)]
        public string? DeviceModel { get; set; }
        [ForeignKey("UserAccounts")]
        public int UserAccountID { get; set; }
        public DateTime LoginExpiryDate { get; set; }
    }
}
