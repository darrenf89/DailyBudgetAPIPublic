using Microsoft.EntityFrameworkCore;
using DailyBudgetAPI.Models;

namespace DailyBudgetAPI.Data
{
    public class ApplicationDBContext: DbContext
    {
        public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options) : base(options)
        {

        }

        public DbSet<UserAccounts>? UserAccounts { get; set; }
        public DbSet<Budgets>? Budgets { get; set; }
        public DbSet<Bills>? Bills { get; set; }
        public DbSet<IncomeEvents>? IncomeEvents { get; set; }
        public DbSet<Categories>? Categories { get; set; }
        public DbSet<Transactions>? Transactions { get; set; }
        public DbSet<Savings>? Savings { get; set; }
        public DbSet<BudgetSettings>? BudgetSettings { get; set; }
        public DbSet<BudgetSettings>? BudgetHistory { get; set; }
        public DbSet<lut_CurrencySymbol> lut_CurrencySymbols {get; set;}
        public DbSet<lut_CurrencyDecimalDigits> lut_CurrencyDecimalDigits {get; set;}
        public DbSet<lut_CurrencyDecimalSeparator> lut_CurrencyDecimalSeparators {get; set;}
        public DbSet<lut_CurrencyGroupSeparator> lut_CurrencyGroupSeparators {get; set;}
        public DbSet<lut_DateSeperator> lut_DateSeperators {get; set;}
        public DbSet<lut_ShortDatePattern> lut_ShortDatePatterns {get; set;}
        public DbSet<lut_DateFormat> lut_DateFormats {get; set;}
        public DbSet<lut_NumberFormat> lut_NumberFormats {get; set;}
        public DbSet<lut_CurrencyPlacement> lut_CurrencyPlacements {get; set;}
        public DbSet<lut_BudgetTimeZone> lut_BudgetTimeZone { get; set; }
        public DbSet<PayPeriodStats> PayPeriodStats { get; set; }
        public DbSet<ErrorLog> ErrorLog { get; set; }
        public DbSet<OTP> OTP { get; set; }
        public DbSet<ShareBudgetRequest> ShareBudgetRequest { get; set; }
        public DbSet<FirebaseDevices> FirebaseDevices { get; set; }
        public DbSet<ProfilePictureImage> ProfilePictureImages { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<UserAccounts>(entity => {
                entity.HasIndex(e => e.Email).IsUnique();
            });
        }
    }
}
