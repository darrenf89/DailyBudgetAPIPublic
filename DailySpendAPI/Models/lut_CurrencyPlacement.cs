

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class lut_CurrencyPlacement
    {

        [Key]
        public int id { get; set; }
        public string CurrencyPlacement { get; set; } = "";
        public int CurrencyPositivePatternRef { get; set; } = 0;

    }
}
