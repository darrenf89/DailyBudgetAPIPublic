using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class BudgetSettings
    {

        [Key]
        public int SettingsID { get; set; }
        [ForeignKey("Budget")]
        public int? BudgetID { get; set; }
        public int? CurrencyPattern { get; set; } = 1;
        public int? CurrencySymbol { get; set; } = 1;
        public int? CurrencyDecimalDigits { get; set; } = 2;
        public int? CurrencyDecimalSeparator { get; set; } = 1;
        public int? CurrencyGroupSeparator { get; set; } = 2;
        public int? DateSeperator { get; set; } = 1;
        public int? ShortDatePattern { get; set; } = 2;
        public int? TimeZone { get; set; } = 47;
    }
}
