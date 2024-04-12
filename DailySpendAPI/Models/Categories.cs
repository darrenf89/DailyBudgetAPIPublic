using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography.X509Certificates;

namespace DailyBudgetAPI.Models
{
    public class Categories
    {
        [Key]
        public int CategoryID { get; set; }
        public string? CategoryName { get; set; }
        public bool isSubCategory { get; set; } = true;
        public int? CategoryGroupID { get; set; }
        public string? CategoryIcon { get; set; }
        [NotMapped]
        public decimal CategorySpendAllTime { get; set; }
        [NotMapped]
        public decimal CategorySpendPayPeriod { get; set; }
        [NotMapped]
        public List<SpendPeriods> CategorySpendPeriods { get; set; } = new List<SpendPeriods>();


    }

    public class SpendPeriods
    {
        public DateTime FromDate {  get; set; }
        public DateTime ToDate { get; set; }
        public decimal SpendTotalAmount { get; set; }
        public bool IsCurrentPeriod { get; set; }
    }

    public class DefaultCategories
    {
        public string CatName { get; set; }
        public string CategoryIcon { get; set; }
        public List<SubCategories> SubCategories { get; set; }
    }

    public class SubCategories
    {
        public string SubCatName { get; set; }
    }
}
