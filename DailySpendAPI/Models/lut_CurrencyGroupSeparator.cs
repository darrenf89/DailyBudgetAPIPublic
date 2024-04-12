using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class lut_CurrencyGroupSeparator
    {

        [Key]
        public int id { get; set; }
        public string CurrencyGroupSeparator { get; set; } = "";

    }
}
