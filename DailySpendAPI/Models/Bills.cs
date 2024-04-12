using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class Bills
    {
        [Key]
        public int BillID { get; set; }
        [Required]
        public string? BillName { get; set; }
        public string? BillType { get; set; }
        public int? BillValue { get; set; }
        public string? BillDuration { get; set; }
        [Required]
        public decimal? BillAmount { get; set; }
        [Required]
        public DateTime? BillDueDate { get; set; }
        public decimal BillCurrentBalance { get; set; } = decimal.Zero;
        public bool isRecuring { get; set; } = false;
        public DateTime LastUpdatedDate { get; set; } = DateTime.UtcNow;
        public bool isClosed { get; set; }
        public decimal? RegularBillValue { get; set; }
        public string? BillPayee { get; set; }
        [MaxLength(50)]
        public string? Category { get; set; } = "";
        public int? CategoryID { get; set; }


    }

    

}
