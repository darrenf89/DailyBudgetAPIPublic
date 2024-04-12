using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class lut_CurrencyDecimalSeparator
    {

        [Key]
        public int id { get; set; }
        public string CurrencyDecimalSeparator { get; set; } = "";

    }
}
