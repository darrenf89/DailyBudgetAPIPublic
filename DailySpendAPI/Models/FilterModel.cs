using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class FilterModel 
    {
        public DateFilter? DateFilter { get; set; }
        public List<string>? TransactionEventTypeFilter { get; set; }
        public List<string>? PayeeFilter { get; set; }
        public List<int>? CategoryFilter { get; set; }
        public List<int>? SavingFilter { get; set; }
    }

    public class DateFilter 
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
