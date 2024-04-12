using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class ErrorLog
    {
        [Key]
        public int ErrorLogID { get; set; }
        public string? ErrorMessage { get; set;}
        public string? ErrorPage { get; set; }
        public string? ErrorMethod { get; set; }
        public string? DeviceName { get; set; }
        public string? DevicePlatform { get; set; }
        public string? DeviceIdiom { get; set; }
        public string? DeviceOSVersion { get; set; }
        public string? DeviceModel { get; set; }
        public int BudgetID { get; set; }
        public DateTime? WhenAdded { get; set; }

    }
}