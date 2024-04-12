using System.ComponentModel.DataAnnotations;

namespace DailyBudgetAPI.Models
{
    public class ShareBudgetRequest

    {
        [Key]
        public int SharedBudgetRequestID { get; set; }
        public int SharedBudgetID { get; set; }
        public int SharedWithUserAccountID {  get; set; }
        public string? SharedWithUserEmail { get; set; }
        public string? SharedByUserEmail { get; set; }
        public bool IsVerified { get; set; }
        public DateTime RequestInitiated { get; set; } = DateTime.UtcNow;

    }
}