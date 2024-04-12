using System.Drawing.Printing;
using System.Globalization;
using System.Security.Policy;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using FirebaseAdmin.Messaging;
using static DailyBudgetAPI.Services.ProductTools;

namespace DailyBudgetAPI.Services
{
    public interface IProductTools
    {
        public Task<string> RecalculateAfterTransactionUpdate(int BudgetID, int TransactionID);
        public Task UnTransact(Transactions Transaction, int BudgetID);
        public Task ReTransactTransaction(Transactions T, int? BudgetID);
        public Task TransactUpdate(Transactions Transaction, int BudgetID);
        public Task<string> UpdateBudgetAsync(int BudgetID);
        public string UpdateBudget(int BudgetID);
        public string UpdateBudgetCreateSavings(int BudgetID);
        public string UpdateBudgetCreateIncome(int BudgetID);
        public string UpdateBudgetCreateSavingsSpend(int BudgetID);
        public string UpdateBudgetCreateBillsSpend(int BudgetID);
        public string UpdateBudgetCreateBills(int BudgetID);
        public DateTime CalculateNextDate(DateTime LastDate, string Type, int Value, string? Duration);
        public string CalculateNextDateEverynth(ref DateTime NextDate, DateTime LastDate, int Value, string? Duration);
        public string CalculateNextDateWorkingDays(ref DateTime NextDate, DateTime LastDate, int Value);
        public string CalculateNextDateOfEveryMonth(ref DateTime NextDate, DateTime LastDate, int Value);
        public string CalculateNextDateLastOfTheMonth(ref DateTime NextDate, DateTime LastDate, string? Duration);
        public int CreateCategory(int BudgetID, DefaultCategories Category);
        public string CreateDefaultCategories(int BudgetID);
        public CultureInfo LoadCurrencySetting(int BudgetID);
        public string TransactTransaction(ref Transactions T, int? BudgetID, bool IsUpdate = false);
        public string TransactSavingsTransaction(ref Transactions T, int? BudgetID, bool IsUpdate = false);
        public string RecalculateRegularSavingFromTransaction(ref Savings S);
        public string CalculateSavingsTargetAmount(ref Savings S);
        public string CalculateSavingsTargetDate(ref Savings S);
        public PayPeriodStats CreateNewPayPeriodStats(int? BudgetID);
        public string GetBudgetDatePattern(int BudgetID);
        public string GetBudgetShortDatePattern(int BudgetID);
        public string UpdatePayPeriodStats(int? BudgetID);
        public string RecalculateBudgetDetails(int? BudgetID);
        public string RegularBudgetUpdateLoop(int? BudgetID);
        public string BudgetUpdateDailyy(int BudgetID, DateTime LastUpdated);
        public void UpdateTransactionDaily(ref Budgets Budget);
        public void TransactSavingsTransactionDaily(ref Budgets Budget, int ID);
        public void TransactTransactionDaily(ref Budgets Budget, int ID);
        public void UpdateSavingsDaily(ref Budgets Budget);
        public string UpdateApproxDaysBetweenPay(int BudgetID);
        public int CalculateBudgetDaysBetweenPay(Budgets Budget);
        public DateTime GetBudgetLocalTime(DateTime UtcDate, int BudgetID);
        public Task ReadFireBaseAdminSdk();
        public Task<string> SendPushNotification(Message Message);
        public Task<string> SendAllPushNotification(List<Message> Messages);
    }
}
