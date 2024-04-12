using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class lut_CurrencySymbol
    {

        [Key]
        public int id { get; set; }
        public string CurrencySymbol { get; set; } = "";
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
    }
}
