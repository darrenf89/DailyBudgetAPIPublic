using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DailyBudgetAPI.Models
{
    public class Payees
    {
        public string? Payee { get; set; }
        public decimal PayeeSpendAllTime { get; set; }
        public decimal PayeeSpendPayPeriod { get; set; }
        public List<SpendPeriods> PayeeSpendPeriods { get; set; } = new List<SpendPeriods>();

    }
}
