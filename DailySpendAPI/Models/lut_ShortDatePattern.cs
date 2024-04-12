
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class lut_ShortDatePattern
    {

        [Key]
        public int id { get; set; }
        public string ShortDatePattern { get; set; } = "";

    }
}
