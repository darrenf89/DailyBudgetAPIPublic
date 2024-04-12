using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class lut_CurrencyDecimalDigits
    {

        [Key]
        public int id { get; set; }
        public string CurrencyDecimalDigits { get; set; } = "";

    }
}
