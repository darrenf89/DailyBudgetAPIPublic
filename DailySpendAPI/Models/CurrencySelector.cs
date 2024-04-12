namespace DailyBudgetAPI.Models
{
    public class CurrencySelector
    {
        public string? symbol { get; set; } = default!;
        public string? name { get; set; }
        public string? symbol_native { get; set; }
        public int? decimal_digits { get; set; }
        public decimal? rounding { get; set; }
        public string? code { get; set; }
        public string? name_plural { get; set; }

    }
}
