using DailyBudgetAPI.Models;
using DailyBudgetAPI.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using FirebaseAdmin.Messaging;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace DailyBudgetAPI.Services
{
    public class ProductTools : IProductTools
    {

        private readonly ApplicationDBContext _db;

        public ProductTools(ApplicationDBContext db)
        {
            _db = db;
        }

        public async Task<string> RecalculateAfterTransactionUpdate(int BudgetID, int TransactionID)
        {
            DateTime Today = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Date;

            Budgets? Budget = _db.Budgets
                .Include(b => b.Transactions.Where(t => t.TransactionDate.Date == Today))
                .Where(b => b.BudgetID == BudgetID)
                .FirstOrDefault();

            List<Transactions> TransactionList = new List<Transactions>();

            if (Budget.Transactions.Count != 0)
            {
                foreach (Transactions Transaction in Budget.Transactions)
                {
                    if(Transaction.isTransacted && !string.Equals(Transaction.EventType, "PayDay",StringComparison.OrdinalIgnoreCase))
                    {
                        decimal TransactionAmount = 0;
                        if(Transaction.isIncome)
                        {
                            TransactionAmount = Transaction.TransactionAmount.GetValueOrDefault() * -1;
                        }
                        else
                        {
                            TransactionAmount = Transaction.TransactionAmount.GetValueOrDefault();
                        }

                        Budget.BankBalance = Budget.BankBalance + TransactionAmount;
                        TransactionList.Add(Transaction);
                    }
                }
            }

            Budget.LeftToSpendBalance = Budget.BankBalance;
            Budget.MoneyAvailableBalance = Budget.BankBalance;

            _db.SaveChanges();

            string status = "OK";

            status = status == "OK" ? UpdateBudgetCreateIncome(BudgetID) : status;
            status = status == "OK" ? UpdateBudgetCreateSavingsSpend(BudgetID) : status;
            status = status == "OK" ? UpdateBudgetCreateBillsSpend(BudgetID) : status;

            Budget = _db.Budgets
                .Where(b => b.BudgetID == BudgetID)
                .FirstOrDefault();

            if (Budget.IsBorrowPay)
            {
                Budget.MoneyAvailableBalance += Budget.PaydayAmount;
                Budget.LeftToSpendBalance += Budget.PaydayAmount;
            }

            int DaysToPayDay = (int)Math.Ceiling((Budget.NextIncomePayday.GetValueOrDefault().Date - DateTime.Today.Date).TotalDays);
            Budget.LeftToSpendDailyAmount = (Budget.LeftToSpendBalance ?? 0) / DaysToPayDay;
            Budget.StartDayDailyAmount = Budget.LeftToSpendDailyAmount;

            _db.SaveChanges();

            foreach(Transactions T in TransactionList)
            {
                await ReTransactTransaction(T, BudgetID);
            }            

            return status;
        }

        public async Task TransactUpdate(Transactions Transaction, int BudgetID)
        {
            if (Transaction.isSpendFromSavings)
            {
                TransactSavingsTransaction(ref Transaction, BudgetID, true);
            }
            else
            {
                TransactTransaction(ref Transaction, BudgetID, true);
            }
        }       

        public async Task UnTransact(Transactions Transaction, int BudgetID)
        {

            Budgets? Budget = _db.Budgets?
                .Where(x => x.BudgetID == BudgetID)
                .Include(x => x.PayPeriodStats.Where(p => p.StartDate.Date < Transaction.TransactionDate.Date && p.EndDate.Date > Transaction.TransactionDate.Date))
                .FirstOrDefault();

            if (Transaction.isSpendFromSavings)
            {
                Budget.PayPeriodStats[0].IncomeToDate -= Transaction.TransactionAmount.GetValueOrDefault();
                _db.SaveChanges(); 

                UnTransactSavingsTransaction(Transaction, BudgetID);
            }
            else
            {
                Budget.PayPeriodStats[0].SpendToDate -= Transaction.TransactionAmount.GetValueOrDefault();
                _db.SaveChanges();

                UnTransactTransaction(Transaction, BudgetID);                
            }

        }
        
        private void UnTransactSavingsTransaction(Transactions T, int BudgetID)
        {
            Budgets? Budget = _db.Budgets?
                .Where(x => x.BudgetID == BudgetID)
                .FirstOrDefault();

            _db.Attach(Budget);

            int TransactionsSavingsID = T.SavingID ?? 0;

            Savings? S = _db.Savings.Where(s => s.SavingID == TransactionsSavingsID).FirstOrDefault();

            if (S != null)
            {
                _db.Attach(S);
            }


            if (T.SavingsSpendType == "UpdateValues")
            {
                if (T.isIncome)
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    Budget.LastUpdated = DateTime.UtcNow;
                    S.CurrentBalance -= T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                }
                else
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    Budget.LastUpdated = DateTime.UtcNow;
                    S.CurrentBalance += T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;

                }

                _db.SaveChanges();

                RecalculateRegularSavingFromTransaction(ref S);
                
            }
            else if (T.SavingsSpendType == "MaintainValues")
            {
                if (T.isIncome)
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    Budget.LeftToSpendBalance -= T.TransactionAmount;
                    Budget.LastUpdated = DateTime.UtcNow;
                    S.CurrentBalance -= T.TransactionAmount;
                    S.SavingsGoal -= T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;

                }
                else
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    Budget.LeftToSpendBalance += T.TransactionAmount;
                    Budget.LastUpdated = DateTime.UtcNow;
                    S.CurrentBalance += T.TransactionAmount;
                    S.SavingsGoal += T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                }
            }
            else if (T.SavingsSpendType == "BuildingSaving" | T.SavingsSpendType == "EnvelopeSaving")
            {
                if (T.isIncome)
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    Budget.LastUpdated = DateTime.UtcNow;
                    S.CurrentBalance -= T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;

                }
                else
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    Budget.LastUpdated = DateTime.UtcNow;
                    S.CurrentBalance += T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                }
            }
            _db.SaveChanges();
        }

        private void UnTransactTransaction(Transactions T, int BudgetID)
        {
            Budgets? Budget = _db.Budgets?
                .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                .Where(x => x.BudgetID == BudgetID)
                .FirstOrDefault();

            _db.Attach(Budget);

            if (T.isIncome)
            {
                Budget.BankBalance -= T.TransactionAmount;
                Budget.MoneyAvailableBalance -= T.TransactionAmount;
                Budget.LeftToSpendBalance -= T.TransactionAmount;
                Budget.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                Budget.BankBalance += T.TransactionAmount;
                Budget.MoneyAvailableBalance += T.TransactionAmount;
                Budget.LeftToSpendBalance += T.TransactionAmount;
                Budget.LeftToSpendDailyAmount += T.TransactionAmount ?? 0;
                Budget.LastUpdated = DateTime.UtcNow;
            }

            _db.SaveChanges();
        }

        public async Task ReTransactTransaction(Transactions T, int? BudgetID)
        {
            Budgets? Budget = _db.Budgets?
                .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                .Where(x => x.BudgetID == BudgetID)
                .FirstOrDefault();

            _db.Attach(Budget);

            if(T.isSpendFromSavings)
            {
                if (T.SavingsSpendType == "UpdateValues")
                {
                    if (T.isIncome)
                    {
                        Budget.BankBalance += T.TransactionAmount;
                        Budget.MoneyAvailableBalance += T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                    }
                    else
                    {
                        Budget.BankBalance -= T.TransactionAmount;
                        Budget.MoneyAvailableBalance -= T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                    }
                }
                else if (T.SavingsSpendType == "MaintainValues")
                {
                    if (T.isIncome)
                    {
                        Budget.BankBalance += T.TransactionAmount;
                        Budget.MoneyAvailableBalance += T.TransactionAmount;
                        Budget.LeftToSpendBalance += T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                    }
                    else
                    {
                        Budget.BankBalance -= T.TransactionAmount;
                        Budget.MoneyAvailableBalance -= T.TransactionAmount;
                        Budget.LeftToSpendBalance += T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                    }
                }
                else if (T.SavingsSpendType == "BuildingSaving" | T.SavingsSpendType == "EnvelopeSaving")
                {
                    if (T.isIncome)
                    {
                        Budget.BankBalance += T.TransactionAmount;
                        Budget.MoneyAvailableBalance += T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                    }
                    else
                    {
                        Budget.BankBalance -= T.TransactionAmount;
                        Budget.MoneyAvailableBalance -= T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                    }
                }
            }
            else
            {
                if (T.isIncome)
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    Budget.LeftToSpendBalance += T.TransactionAmount;
                    //Recalculate how much you have left to spend
                    int DaysToPayDay = (int)Math.Ceiling((Budget.NextIncomePayday.GetValueOrDefault().Date - GetBudgetLocalTime(DateTime.UtcNow, BudgetID.GetValueOrDefault()).Date).TotalDays);
                    if(DaysToPayDay > 0)
                    {
                        Budget.LeftToSpendDailyAmount += (T.TransactionAmount ?? 0) / DaysToPayDay;
                    }
                    Budget.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    Budget.LeftToSpendBalance -= T.TransactionAmount;
                    if(string.Equals(T.EventType, "Transaction",StringComparison.OrdinalIgnoreCase))
                    {
                        Budget.LeftToSpendDailyAmount -= T.TransactionAmount ?? 0;
                    }                    
                    Budget.LastUpdated = DateTime.UtcNow;
                }
            }

            _db.SaveChanges();
        }

        public async Task<string> UpdateBudgetAsync(int BudgetID)
        {
            return UpdateBudget(BudgetID);
        }

        public string UpdateBudget(int BudgetID)
        {
            
            Budgets? Budget = _db.Budgets?
            .Where(x => x.BudgetID == BudgetID)
            .FirstOrDefault();

            _db.Attach(Budget);

            Budget.LeftToSpendBalance = Budget.BankBalance;
            Budget.MoneyAvailableBalance = Budget.BankBalance;

            _db.SaveChanges();

            string status = "OK";


            status = status == "OK" ? UpdateApproxDaysBetweenPay(BudgetID) : status;
            status = status == "OK" ? UpdateBudgetCreateSavings(BudgetID) : status;
            status = status == "OK" ? UpdateBudgetCreateBills(BudgetID) : status;
            status = status == "OK" ? UpdateBudgetCreateIncome(BudgetID) : status;
            status = status == "OK" ? UpdateBudgetCreateSavingsSpend(BudgetID) : status;
            status = status == "OK" ? UpdateBudgetCreateBillsSpend(BudgetID) : status;

            Budget.LastUpdated = DateTime.UtcNow;
            int DaysToPayDay = (int)Math.Ceiling((Budget.NextIncomePayday.GetValueOrDefault().Date - GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Date).TotalDays);

            //If Budget is Borrow Pay add Next Pay day value to MaB and Lts
            if (Budget.IsBorrowPay)
            {
                Budget.MoneyAvailableBalance += Budget.PaydayAmount;
                Budget.LeftToSpendBalance += Budget.PaydayAmount;
            }

            _db.SaveChanges();

            return status;
        }

        public string UpdateBudgetCreateSavings(int BudgetID)
        {
            if (BudgetID == 0)
            {
                return "No Budget Detected";
            }
            else
            {

                Budgets? Budget = _db.Budgets?
                    .Include(x => x.Savings.Where(s => s.isSavingsClosed == false))
                    .Where(x => x.BudgetID == BudgetID)
                    .FirstOrDefault();

                if(Budget != null)
                {
                    int? DaysBetweenPay = Budget.AproxDaysBetweenPay;

                    foreach (Savings Saving in Budget.Savings)
                    {
                        if (!Saving.isSavingsClosed)
                        {
                            if (Saving.isRegularSaving)
                            {
                                if (Saving.SavingsType == "TargetAmount")
                                {
                                    if (!Saving.isDailySaving)
                                    {
                                        Saving.RegularSavingValue = Saving.PeriodSavingValue / Budget.AproxDaysBetweenPay;
                                    }

                                    decimal? BalanceLeft = Saving.SavingsGoal - (Saving.CurrentBalance ?? 0);
                                    int NumberOfDays = (int)Math.Ceiling(BalanceLeft / Saving.RegularSavingValue ?? 0);

                                    DateTime Today = GetBudgetLocalTime(DateTime.UtcNow, BudgetID);
                                    Saving.GoalDate = Today.AddDays(NumberOfDays);

                                }
                                else if (Saving.SavingsType == "TargetDate")
                                {
                                    decimal? BalanceLeft = Saving.SavingsGoal - (Saving.CurrentBalance ?? 0);

                                    TimeSpan TimeToGoal = (Saving.GoalDate.GetValueOrDefault().Date - Budget.BudgetValuesLastUpdated.Date);
                                    int? DaysToGoal = (int)TimeToGoal.TotalDays;

                                    Saving.RegularSavingValue = BalanceLeft / DaysToGoal;
                                }
                                else if (Saving.SavingsType == "SavingsBuilder")
                                {
                                    if (!Saving.isDailySaving)
                                    {
                                        Saving.RegularSavingValue = Saving.PeriodSavingValue / Budget.AproxDaysBetweenPay;
                                    }
                                }
                            }
                        }

                        _db.SaveChanges();
                    }
                }
                else
                {
                    return "No Budget Detected";
                }
            }

            return "OK";
        }

        public string UpdateBudgetCreateIncome(int BudgetID)
        {
            if (BudgetID == 0)
            {
                return "No Budget Detected";
            }
            else
            {
                Budgets? Budget = _db.Budgets?
                    .Include(i => i.IncomeEvents.Where(i => i.isClosed == false))
                    .Where(x => x.BudgetID == BudgetID)
                    .FirstOrDefault();


                if (Budget != null)
                {
                    DateTime Today = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Date;
                    Budget.CurrentActiveIncome = 0;
                    foreach (IncomeEvents Income in Budget.IncomeEvents)
                    {
                        if (!Income.isClosed)
                        {
                            DateTime NextPayDay = Budget.NextIncomePayday ?? default;
                            if (Income.isInstantActive ?? false)
                            {
                                DateTime PayDayAfterNext = CalculateNextDate(NextPayDay, Budget.PaydayType, Budget.PaydayValue ?? 1, Budget.PaydayDuration);
                                DateTime NextIncomeDate = CalculateNextDate(Income.DateOfIncomeEvent, Income.RecurringIncomeType, Income.RecurringIncomeValue ?? 1, Income.RecurringIncomeDuration);
                                //Next Income Date happens in this Pay window so process
                                if (Income.DateOfIncomeEvent.Date < NextPayDay.Date)
                                {
                                    Income.IncomeActiveDate = DateTime.UtcNow;
                                    Budget.MoneyAvailableBalance = Budget.MoneyAvailableBalance + Income.IncomeAmount;
                                    Budget.LeftToSpendBalance = Budget.LeftToSpendBalance + Income.IncomeAmount;
                                    Budget.CurrentActiveIncome += Income.IncomeAmount;
                                    while (NextIncomeDate.Date < NextPayDay.Date)
                                    {
                                        Budget.MoneyAvailableBalance = Budget.MoneyAvailableBalance + Income.IncomeAmount;
                                        Budget.LeftToSpendBalance = Budget.LeftToSpendBalance + Income.IncomeAmount;
                                        Budget.CurrentActiveIncome += Income.IncomeAmount;
                                        //TODO: Add a Transaction into transactions
                                        NextIncomeDate = CalculateNextDate(NextIncomeDate, Income.RecurringIncomeType, Income.RecurringIncomeValue ?? 1, Income.RecurringIncomeDuration);
                                    }
                                }
                                else
                                {
                                    DateTime CalPayDate = NextPayDay.Date;
                                    while (Income.DateOfIncomeEvent.Date >= NextPayDay.Date)
                                    {
                                        CalPayDate = NextPayDay;
                                        NextPayDay = CalculateNextDate(NextPayDay, Budget.PaydayType, Budget.PaydayValue ?? 1, Budget.PaydayDuration);
                                    }
                                    Income.IncomeActiveDate = CalPayDate.Date;
                                }
                            }
                        }
                    }
                }
                else
                {
                    return "No Budget Detected";
                }

            _db.SaveChanges();

            return "OK";

            }
        }

        public string UpdateBudgetCreateSavingsSpend(int BudgetID)
        {
            decimal DailySavingOutgoing = new();
            decimal PeriodTotalSavingOutgoing = new();

            if (BudgetID == 0)
            {
                return "No Budget Detected";
            }
            else
            {
                Budgets? Budget = _db.Budgets?
                    .Include(x => x.Savings.Where(s => s.CurrentBalance != 0 || s.isSavingsClosed == false))
                    .Where(x => x.BudgetID == BudgetID)
                    .FirstOrDefault();

                int DaysToPayDay = (int)Math.Ceiling((Budget.NextIncomePayday.GetValueOrDefault().Date - Budget.BudgetValuesLastUpdated.Date).TotalDays);

                foreach (Savings Saving in Budget.Savings)
                {
                    if (!Saving.isSavingsClosed)
                    {
                        if (Saving.isRegularSaving & Saving.SavingsType == "SavingsBuilder")
                        {
                            DailySavingOutgoing += Saving.RegularSavingValue ?? 0;
                            PeriodTotalSavingOutgoing += ((Saving.RegularSavingValue ?? 0) * DaysToPayDay);
                        }
                        else if (Saving.isRegularSaving)
                        {
                            DailySavingOutgoing += Saving.RegularSavingValue ?? 0;
                            //check if goal date is before pay day
                            int DaysToSaving = (int)Math.Ceiling((Saving.GoalDate.GetValueOrDefault().Date - Budget.BudgetValuesLastUpdated.Date).TotalDays);
                            if (DaysToSaving < DaysToPayDay)
                            {
                                PeriodTotalSavingOutgoing += ((Saving.RegularSavingValue ?? 0) * DaysToSaving);
                            }
                            else
                            {
                                PeriodTotalSavingOutgoing += ((Saving.RegularSavingValue ?? 0) * DaysToPayDay);
                            }

                        }
                    }

                    PeriodTotalSavingOutgoing += Saving.CurrentBalance ?? 0;
                }

                Budget.DailySavingOutgoing = DailySavingOutgoing;
                Budget.LeftToSpendBalance = Budget.LeftToSpendBalance - PeriodTotalSavingOutgoing;

                _db.SaveChanges();

                return "OK";
            }
        }

        public string UpdateBudgetCreateBills(int BudgetID)
        {
            Budgets? Budget = _db.Budgets?
                .Include(x => x.Bills.Where(b => b.isClosed == false))
                .Where(x => x.BudgetID == BudgetID)
                .FirstOrDefault();

            if (Budget != null)
            {
                foreach (Bills Bill in Budget.Bills)
                {
                    if (!Bill.isClosed)
                    {
                        decimal? BalanceLeft = Bill.BillAmount - Bill.BillCurrentBalance;

                        TimeSpan TimeToGoal = (Bill.BillDueDate.GetValueOrDefault().Date - Budget.BudgetValuesLastUpdated.Date);
                        int? DaysToGoal = (int)TimeToGoal.TotalDays;

                        Bill.RegularBillValue = BalanceLeft / DaysToGoal;
                    }
                    _db.SaveChanges();
                }

                return "OK";
            }
            else
            {
                return "No Budget Detected";
            }
        }

        public string UpdateBudgetCreateBillsSpend(int BudgetID)
        {
            decimal DailyBillOutgoing = new();
            decimal PeriodTotalBillOutgoing = new();

            if (BudgetID == 0)
            {
                return "No Budget Detected";
            }
            else
            {
                Budgets? Budget = _db.Budgets?
                    .Include(x => x.Bills.Where(b => b.isClosed == false))
                    .Where(x => x.BudgetID == BudgetID)
                    .FirstOrDefault();

                int DaysToPayDay = (int)Math.Ceiling((Budget.NextIncomePayday.GetValueOrDefault().Date - Budget.BudgetValuesLastUpdated.Date).TotalDays);

                foreach (Bills Bill in Budget.Bills)
                {
                    if (!Bill.isClosed)
                    {
                        DailyBillOutgoing += Bill.RegularBillValue ?? 0;
                        //Check if Due Date is before Pay Dat
                        int DaysToBill = (int)Math.Ceiling((Bill.BillDueDate.GetValueOrDefault().Date - Budget.BudgetValuesLastUpdated.Date).TotalDays);
                        if (Bill.isRecuring)
                        {
                            if (DaysToBill < DaysToPayDay)
                            {
                                PeriodTotalBillOutgoing += (Bill.RegularBillValue ?? 0) * DaysToBill;

                                DateTime BillDueAfterNext = CalculateNextDate(Bill.BillDueDate.GetValueOrDefault(), Bill.BillType, Bill.BillValue.GetValueOrDefault(), Bill.BillDuration);
                                int NumberOfDaysBill = (int)Math.Ceiling((BillDueAfterNext - Bill.BillDueDate.GetValueOrDefault()).TotalDays);
                                decimal? BillRegularValue = Bill.BillAmount / NumberOfDaysBill;

                                PeriodTotalBillOutgoing += BillRegularValue.GetValueOrDefault() * (DaysToPayDay - DaysToBill);

                            }
                            else
                            {
                                PeriodTotalBillOutgoing += (Bill.RegularBillValue ?? 0) * DaysToPayDay;
                            }
                        }
                        else
                        {
                            if (DaysToBill < DaysToPayDay)
                            {
                                PeriodTotalBillOutgoing += (Bill.RegularBillValue ?? 0) * DaysToBill;
                            }
                            else
                            {
                                PeriodTotalBillOutgoing += (Bill.RegularBillValue ?? 0) * DaysToPayDay;
                            }
                        }

                        PeriodTotalBillOutgoing += Bill.BillCurrentBalance;

                    }
                }

                Budget.DailyBillOutgoing = DailyBillOutgoing;
                Budget.LeftToSpendBalance = Budget.LeftToSpendBalance - PeriodTotalBillOutgoing;
                Budget.MoneyAvailableBalance = Budget.MoneyAvailableBalance - PeriodTotalBillOutgoing;

            }
            _db.SaveChanges();
            return "OK";
        }

        public DateTime CalculateNextDate(DateTime LastDate, string Type, int Value, string? Duration)
        {
            DateTime NextDate = new DateTime();
            string status = "";

            if (Type == "Everynth")
            {
                status = CalculateNextDateEverynth(ref NextDate, LastDate, Value, Duration);
            }
            else if (Type == "WorkingDays")
            {
                status = CalculateNextDateWorkingDays(ref NextDate, LastDate, Value);
            }
            else if (Type == "OfEveryMonth")
            {
                status = CalculateNextDateOfEveryMonth(ref NextDate, LastDate, Value);
            }
            else if (Type == "LastOfTheMonth")
            {
                status = CalculateNextDateLastOfTheMonth(ref NextDate, LastDate, Duration);
            }

            if (status == "OK")
            {
                return NextDate;
            }
            else
            {
                throw new Exception(status);
            }

        }

        public string CalculateNextDateEverynth(ref DateTime NextDate, DateTime LastDate, int Value, string? Duration)
        {
            try
            {
                int IntDuration;
                if (Duration == "days")
                {
                    IntDuration = 1;
                }
                else if (Duration == "weeks")
                {
                    IntDuration = 7;
                }
                else if (Duration == "months")
                {
                    IntDuration = 30;
                }
                else if (Duration == "years")
                {
                    IntDuration = 365;
                }
                else
                {
                    return "Duration not valid or null";
                }

                int DaysBetween = IntDuration * Value;

                NextDate = LastDate.AddDays(DaysBetween);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return "OK";
        }

        public string CalculateNextDateWorkingDays(ref DateTime NextDate, DateTime LastDate, int Value)
        {
            try
            {
                int year = LastDate.Year;
                int month = LastDate.Month;

                int NextYear = new int();
                int NextMonth = new int();

                if (month != 12)
                {
                    NextYear = LastDate.Year;
                    NextMonth = month + 1;
                }
                else
                {
                    NextYear = year + 1;
                    NextMonth = 1;
                }

                DateTime NextCurrentDate = new DateTime();
                var i = DateTime.DaysInMonth(NextYear, NextMonth);
                var j = 1;
                while (i > 0)
                {
                    var dtCurrent = new DateTime(NextYear, NextMonth, i);
                    if (dtCurrent.DayOfWeek < DayOfWeek.Saturday && dtCurrent.DayOfWeek > DayOfWeek.Sunday)
                    {
                        NextCurrentDate = dtCurrent;
                        if (j == Value)
                        {
                            i = 0;
                        }
                        else
                        {
                            i = i - 1;
                            j = j + 1;
                        }
                    }
                    else
                    {
                        i = i - 1;
                    }
                }

                NextDate = NextCurrentDate.Date;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return "OK";
        }

        public string CalculateNextDateOfEveryMonth(ref DateTime NextDate, DateTime LastDate, int Value)
        {
            try
            {
                NextDate = LastDate.AddMonths(1);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return "OK";
        }

        public string CalculateNextDateLastOfTheMonth(ref DateTime NextDate, DateTime LastDate, string? Duration)
        {

            try
            {
                int dayNumber = ((int)Enum.Parse(typeof(DayOfWeek), Duration));

                int year = LastDate.Year;
                int month = LastDate.Month;

                int NextYear = new int();
                int NextMonth = new int();

                if (month != 12)
                {
                    NextYear = LastDate.Year;
                    NextMonth = month + 1;
                }
                else
                {
                    NextYear = year + 1;
                    NextMonth = 1;
                }

                DateTime NewDate = new DateTime();

                var i = DateTime.DaysInMonth(NextYear, NextMonth);
                while (i > 0)
                {
                    var dtCurrent = new DateTime(NextYear, NextMonth, i);
                    if ((int)dtCurrent.DayOfWeek == dayNumber)
                    {
                        NewDate = dtCurrent;
                        i = 0;
                    }
                    else
                    {
                        i = i - 1;
                    }
                }

                NextDate = NewDate.Date;

            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return "OK";
        }

        public int CreateCategory(int BudgetID, DefaultCategories Category)
        {
            Budgets? Budget = _db.Budgets?
                .Include(x => x.Categories)
                .Where(x => x.BudgetID == BudgetID)
                .FirstOrDefault();

            _db.Attach(Budget);

            Categories HeaderCat = new Categories();
            HeaderCat.CategoryName = Category.CatName;
            HeaderCat.CategoryIcon = Category.CategoryIcon;
            HeaderCat.isSubCategory = false;
            Budget.Categories.Add(HeaderCat);

            _db.SaveChanges();
            HeaderCat.CategoryGroupID = HeaderCat.CategoryID;

            foreach (var item in Category.SubCategories)
            {
                Categories SubCat = new Categories();
                SubCat.CategoryName = item.SubCatName;
                SubCat.isSubCategory = true;
                SubCat.CategoryGroupID = HeaderCat.CategoryID;
                Budget.Categories.Add(SubCat);

                _db.SaveChanges();
            }           

            _db.SaveChanges();

            return HeaderCat.CategoryGroupID.GetValueOrDefault();
        }

        public string CreateDefaultCategories(int BudgetID)
        {

            string fileName = "Data/DefaultCategories.json";
            string jsonString = System.IO.File.ReadAllText(fileName, Encoding.UTF8);
            List<DefaultCategories> DefaultCategories = JsonSerializer.Deserialize<List<DefaultCategories>>(jsonString)!;

            Budgets? Budget = _db.Budgets?
                .Include(x => x.Categories)
                .Where(x => x.BudgetID == BudgetID)
                .FirstOrDefault();

            _db.Attach(Budget);

            foreach (var category in DefaultCategories)
            {
                Categories HeaderCat = new Categories();
                HeaderCat.CategoryName = category.CatName;
                HeaderCat.CategoryIcon = category.CategoryIcon;
                HeaderCat.isSubCategory = false;
                Budget.Categories.Add(HeaderCat);

                _db.SaveChanges();
                HeaderCat.CategoryGroupID = HeaderCat.CategoryID;

                foreach (var item in category.SubCategories)
                {
                    Categories SubCat = new Categories();
                    SubCat.CategoryName = item.SubCatName;
                    SubCat.isSubCategory = true;
                    SubCat.CategoryGroupID = HeaderCat.CategoryID;
                    Budget.Categories.Add(SubCat);

                }
                _db.SaveChanges();
            }

            _db.SaveChanges();

            return "OK";
        }

        public CultureInfo LoadCurrencySetting(int BudgetID)
        {
            BudgetSettings? BS = _db.BudgetSettings?.Where(b => b.BudgetID == BudgetID).FirstOrDefault();
            lut_CurrencySymbol? Symbol = _db.lut_CurrencySymbols?.Where(s => s.id == BS.CurrencySymbol).FirstOrDefault();
            lut_CurrencyDecimalSeparator? DecimalSep = _db.lut_CurrencyDecimalSeparators?.Where(d => d.id == BS.CurrencyDecimalSeparator).FirstOrDefault();
            lut_CurrencyGroupSeparator? GroupSeparator = _db.lut_CurrencyGroupSeparators?.Where(g => g.id == BS.CurrencyGroupSeparator).FirstOrDefault();
            lut_CurrencyDecimalDigits? DecimalDigits = _db.lut_CurrencyDecimalDigits?.Where(d => d.id == BS.CurrencyDecimalDigits).FirstOrDefault();
            lut_CurrencyPlacement? CurrencyPositivePat = _db.lut_CurrencyPlacements?.Where(c => c.id == BS.CurrencyPattern).FirstOrDefault();
            lut_DateSeperator? DateSeperator = _db.lut_DateSeperators?.Where(c => c.id == BS.DateSeperator).FirstOrDefault();
            lut_DateFormat? DateFormat = _db.lut_DateFormats?.Where(c => c.DateSeperatorID == BS.DateSeperator & c.ShortDatePatternID == BS.ShortDatePattern).FirstOrDefault();

            CultureInfo nfi = new CultureInfo("en-GB");

            nfi.NumberFormat.CurrencySymbol = Symbol.CurrencySymbol;
            nfi.NumberFormat.CurrencyDecimalSeparator = DecimalSep.CurrencyDecimalSeparator;
            nfi.NumberFormat.CurrencyGroupSeparator = GroupSeparator.CurrencyGroupSeparator;
            nfi.NumberFormat.CurrencyDecimalDigits = Convert.ToInt32(DecimalDigits.CurrencyDecimalDigits);
            nfi.NumberFormat.CurrencyPositivePattern = CurrencyPositivePat.CurrencyPositivePatternRef;
            nfi.DateTimeFormat.ShortDatePattern = DateFormat.DateFormat;
            nfi.DateTimeFormat.DateSeparator = DateSeperator.DateSeperator;

            return nfi;
        }

        public string TransactTransaction(ref Transactions T, int? BudgetID, bool IsUpdate = false)
        {
            Budgets? Budget = new Budgets();
            Transactions Transaction = T;

            if (IsUpdate)
            {
                Budget = _db.Budgets?
                   .Include(x => x.PayPeriodStats.Where(p => p.StartDate.Date < Transaction.TransactionDate.Date && p.EndDate.Date > Transaction.TransactionDate.Date))
                   .Include(x => x.Savings)
                   .Where(x => x.BudgetID == BudgetID)
                   .First();
            }
            else
            {
                Budget = _db.Budgets?
                   .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                   .Include(x => x.Savings)
                   .Where(x => x.BudgetID == BudgetID)
                   .FirstOrDefault();
            }

            _db.Attach(Budget);

            if (BudgetID == 0)
            {
                return "Couldnt find budget";
            }
            else
            {

                if (T.isIncome)
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    Budget.LeftToSpendBalance += T.TransactionAmount;
                    //Recalculate how much you have left to spend
                    int DaysToPayDay = (int)Math.Ceiling((Budget.NextIncomePayday.GetValueOrDefault().Date - GetBudgetLocalTime(DateTime.UtcNow, BudgetID.GetValueOrDefault()).Date).TotalDays);
                    if(DaysToPayDay > 0)
                    {
                        Budget.LeftToSpendDailyAmount += (T.TransactionAmount ?? 0) / DaysToPayDay;
                        Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;
                    }
                    Budget.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    Budget.LeftToSpendBalance -= T.TransactionAmount;
                    Budget.LeftToSpendDailyAmount -= T.TransactionAmount ?? 0;
                    Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
                    Budget.LastUpdated = DateTime.UtcNow;
                }

                T.isTransacted = true;
                _db.SaveChanges();

                return "OK";                
            }

        }

        public string TransactSavingsTransaction(ref Transactions T, int? BudgetID, bool IsUpdate = false)
        {
            Budgets? Budget = new Budgets();
            Transactions Transaction = T;

            if (IsUpdate)
            {
                Budget = _db.Budgets?
                .Include(x => x.PayPeriodStats.Where(p => p.StartDate.Date < Transaction.TransactionDate.Date && p.EndDate.Date > Transaction.TransactionDate.Date))
                   .Include(x => x.Savings)
                   .Where(x => x.BudgetID == BudgetID)
                   .FirstOrDefault();
            }
            else
            {
                Budget = _db.Budgets?
                   .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                   .Include(x => x.Savings)
                   .Where(x => x.BudgetID == BudgetID)
                   .FirstOrDefault();
            }

            int TransactionsSavingsID = T.SavingID ?? 0;

            Savings S = Budget.Savings.Where(s => s.SavingID == TransactionsSavingsID).First();

            _db.Attach(Budget);

            if (BudgetID == 0)
            {
                return "Couldnt find budget";
            }
            else
            {
                if (T.SavingsSpendType == "UpdateValues")
                {
                    if (T.isIncome)
                    {
                        Budget.BankBalance += T.TransactionAmount;
                        Budget.MoneyAvailableBalance += T.TransactionAmount;
                        Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;
                        Budget.PayPeriodStats[0].SavingsToDate += T.TransactionAmount ?? 0;
                        Budget.LastUpdated = DateTime.UtcNow;
                        S.CurrentBalance += T.TransactionAmount;
                        S.LastUpdatedValue = T.TransactionAmount;
                        S.LastUpdatedDate = DateTime.UtcNow;
                    }
                    else
                    {
                        Budget.BankBalance -= T.TransactionAmount;
                        Budget.MoneyAvailableBalance -= T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                        S.CurrentBalance -= T.TransactionAmount;
                        S.LastUpdatedValue = T.TransactionAmount;
                        S.LastUpdatedDate = DateTime.UtcNow;
                        Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
                    }

                    _db.SaveChanges();

                    //Update Regular Saving Value
                    RecalculateRegularSavingFromTransaction(ref S);
                    
                }
                else if (T.SavingsSpendType == "MaintainValues")
                {
                    if (T.isIncome)
                    {
                        Budget.BankBalance += T.TransactionAmount;
                        Budget.MoneyAvailableBalance += T.TransactionAmount;
                        Budget.LeftToSpendBalance += T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                        S.CurrentBalance += T.TransactionAmount;
                        S.SavingsGoal += T.TransactionAmount;
                        S.LastUpdatedValue = T.TransactionAmount;
                        S.LastUpdatedDate = DateTime.UtcNow;
                        Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;
                        Budget.PayPeriodStats[0].SavingsToDate += T.TransactionAmount ?? 0;
                    }
                    else
                    {
                        Budget.BankBalance -= T.TransactionAmount;
                        Budget.MoneyAvailableBalance -= T.TransactionAmount;
                        Budget.LeftToSpendBalance -= T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                        S.CurrentBalance -= T.TransactionAmount;
                        S.SavingsGoal -= T.TransactionAmount;
                        S.LastUpdatedValue = T.TransactionAmount;
                        S.LastUpdatedDate = DateTime.UtcNow;
                        Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
                    }
                }
                else if (T.SavingsSpendType == "BuildingSaving" | T.SavingsSpendType == "EnvelopeSaving")
                {
                    if (T.isIncome)
                    {
                        Budget.BankBalance += T.TransactionAmount;
                        Budget.MoneyAvailableBalance += T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                        S.CurrentBalance += T.TransactionAmount;
                        S.LastUpdatedValue = T.TransactionAmount;
                        S.LastUpdatedDate = DateTime.UtcNow;
                        Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;
                        Budget.PayPeriodStats[0].SavingsToDate += T.TransactionAmount ?? 0;
                    }
                    else
                    {
                        Budget.BankBalance -= T.TransactionAmount;
                        Budget.MoneyAvailableBalance -= T.TransactionAmount;
                        Budget.LastUpdated = DateTime.UtcNow;
                        S.CurrentBalance -= T.TransactionAmount;
                        S.LastUpdatedValue = T.TransactionAmount;
                        S.LastUpdatedDate = DateTime.UtcNow;
                        Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
                    }
                }

                T.isTransacted = true;
                _db.SaveChanges();

                return "OK";
            }

        }

        public string RecalculateRegularSavingFromTransaction(ref Savings S)
        {
            if(S.isRegularSaving)
            {
                if (S.SavingsType == "TargetAmount")
                {
                    CalculateSavingsTargetAmount(ref S);
                }
                else if (S.SavingsType == "TargetDate")
                {
                    CalculateSavingsTargetDate(ref S);
                }
            }

            return "OK";
        }

        public string CalculateSavingsTargetAmount(ref Savings S)
        {
            Savings Saving = S;

            decimal? BalanceLeft = S.SavingsGoal - (S.CurrentBalance ?? 0);
            int NumberOfDays = (int)Math.Ceiling(BalanceLeft / S.RegularSavingValue ?? 0);

            Budgets Budget = _db.Budgets.Where(b => b.Savings.Contains(Saving)).First();

            DateTime Today = GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID);
            S.GoalDate = Today.AddDays(NumberOfDays);

            return "OK";
        }

        public string CalculateSavingsTargetDate(ref Savings S)
        {
            int DaysToSavingDate = (int)Math.Ceiling((S.GoalDate.GetValueOrDefault().Date - DateTime.Today.Date).TotalDays);
            decimal? AmountOutstanding = S.SavingsGoal - S.CurrentBalance;

            S.RegularSavingValue = AmountOutstanding / DaysToSavingDate;

            return "OK";
        }

        public PayPeriodStats CreateNewPayPeriodStats(int? BudgetID)
        {
            Budgets? Budget = _db.Budgets?
               .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
               .Where(x => x.BudgetID == BudgetID)
               .FirstOrDefault();

            _db.Attach(Budget);

            if (Budget.PayPeriodStats.Count > 1)
            {
                Budget.PayPeriodStats[0].isCurrentPeriod = false;
                Budget.PayPeriodStats[0].EndLtSDailyAmount = Budget.LeftToSpendDailyAmount;
                Budget.PayPeriodStats[0].EndLtSPeiordAmount = Budget.LeftToSpendBalance;
                Budget.PayPeriodStats[0].EndBBPeiordAmount = Budget.BankBalance;
                Budget.PayPeriodStats[0].EndMaBPeiordAmount = Budget.MoneyAvailableBalance;
            }

            PayPeriodStats stats = new PayPeriodStats();

            stats.isCurrentPeriod = true;

            stats.StartDate = GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID).Date;
            stats.EndDate = Budget.NextIncomePayday ?? GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID);
            stats.DurationOfPeriod = Budget.AproxDaysBetweenPay ?? 0;

            stats.SavingsToDate = 0;
            stats.BillsToDate = 0;
            stats.IncomeToDate = 0;
            stats.SpendToDate = 0;

            stats.StartLtSDailyAmount = 0;
            stats.StartLtSPeiordAmount = 0;
            stats.StartBBPeiordAmount = 0;
            stats.StartMaBPeiordAmount = 0;

            Budget.PayPeriodStats.Add(stats);
            _db.SaveChanges();

            return stats;
        }

        public string GetBudgetDatePattern(int BudgetID)
        {
            if(BudgetID == 0)
            {

                return "dd MM yyyy";
            }
            else
            {
                BudgetSettings? BS = _db.BudgetSettings.Where(x => x.BudgetID == BudgetID).FirstOrDefault();

                lut_ShortDatePattern? DatePattern = _db.lut_ShortDatePatterns.Where(x => x.id == BS.ShortDatePattern).FirstOrDefault();
                lut_DateSeperator? dateSeperator = _db.lut_DateSeperators.Where(x => x.id == BS.DateSeperator).FirstOrDefault();

                lut_DateFormat? DateFormat = _db.lut_DateFormats.Where(d => d.DateSeperatorID == dateSeperator.id & d.ShortDatePatternID == DatePattern.id).FirstOrDefault(); 

                return DateFormat.DateFormat ?? "dd MM yyyy";
            }

        }

        public string GetBudgetShortDatePattern(int BudgetID)
        {
            if (BudgetID == 0)
            {

                return "ddMMyyyy";
            }
            else
            {
                BudgetSettings? BS = _db.BudgetSettings.Where(x => x.BudgetID == BudgetID).FirstOrDefault();

                lut_ShortDatePattern? DatePattern = _db.lut_ShortDatePatterns.Where(x => x.id == BS.ShortDatePattern).FirstOrDefault();

                return DatePattern.ShortDatePattern ?? "ddMMyyyy";
            }

        }
        public string UpdatePayPeriodStats(int? BudgetID)
        {
            Budgets? Budget = _db.Budgets?
               .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
               .Where(x => x.BudgetID == BudgetID)
               .FirstOrDefault();

            _db.Attach(Budget);


            if (Budget.PayPeriodStats.Count() == 0)
            {
                CreateNewPayPeriodStats(BudgetID);
            }
            else
            {
                PayPeriodStats stats = Budget.PayPeriodStats[0];
                _db.Attach(stats);

                stats.EndDate = Budget.NextIncomePayday ?? GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID);
                stats.DurationOfPeriod = (int)Math.Ceiling((stats.EndDate - stats.StartDate).TotalDays);

                stats.StartLtSDailyAmount = Budget.LeftToSpendDailyAmount;
                stats.StartLtSPeiordAmount = Budget.LeftToSpendBalance;
                stats.StartBBPeiordAmount = Budget.BankBalance;
                stats.StartMaBPeiordAmount = Budget.MoneyAvailableBalance;

                _db.SaveChanges();
            }

            return "OK";
        }
        public string RecalculateBudgetDetails(int? BudgetID)
        {
            string status = "OK";
            Budgets? Budget = _db.Budgets?
               .Where(x => x.BudgetID == BudgetID)
               .FirstOrDefault();

            _db.Attach(Budget);

            Budget.MoneyAvailableBalance = Budget.BankBalance;
            Budget.LeftToSpendBalance = Budget.BankBalance;

            status = UpdateBudgetCreateSavings(BudgetID ?? 0);
            status = UpdateBudgetCreateIncome(BudgetID ?? 0);
            status = UpdateBudgetCreateSavingsSpend(BudgetID ?? 0);
            status = UpdateBudgetCreateBillsSpend(BudgetID?? 0);
            
            return status;
        }

        public string RegularBudgetUpdateLoop(int? BudgetID)
        {
            string status = "OK";
            if (BudgetID == 0)
            {
                return "Couldnt find budget";
            }
            else
            {
                Budgets Budget = _db.Budgets
                    .Where(x => x.BudgetID == BudgetID)
                    .First();

                _db.Attach(Budget);

                DateTime Today = GetBudgetLocalTime(DateTime.UtcNow, BudgetID.GetValueOrDefault()).Date;
                DateTime LastBudgetUpdated = Budget.BudgetValuesLastUpdated.Date;

                while(LastBudgetUpdated < Today)
                {
                    status = BudgetUpdateDailyy(Budget.BudgetID, LastBudgetUpdated);
                    if(status == "OK")
                    {
                        LastBudgetUpdated = LastBudgetUpdated.AddDays(1);
                        Budget.BudgetValuesLastUpdated = LastBudgetUpdated;
                    }
                    else
                    {
                        break;
                    }
                }

            }

            return "OK";
        }

        public string BudgetUpdateDailyy(int BudgetID, DateTime LastUpdated)
        {
            try
            {
                Budgets Budget = _db.Budgets
                    .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                    .Include(x => x.Transactions.Where(t => !t.isTransacted))
                    .Include(x => x.Savings.Where(s => !s.isSavingsClosed))
                    .Include(x => x.Bills.Where(b => !b.isClosed))
                    .Include(x => x.IncomeEvents.Where(i => !i.isClosed))
                    .Where(x => x.BudgetID == BudgetID)
                    .First();

                //Process All Types
                
                UpdateSavingsDaily(ref Budget);
                //ProcessBillsDaily(ref Budget);
                //ProcessIncomeDaily(ref Budget);
                UpdateTransactionDaily(ref Budget);

                //Check if PayDay

                //Recalculate Balances

                //Upate Stats if needed

                DateTime NextPayDay = Budget.NextIncomePayday.GetValueOrDefault().Date;

            }
            catch (System.Exception ex)
            {
                return ex.Message;
            }
            
            _db.SaveChanges();
            return "OK";
        }

        public void UpdateTransactionDaily(ref Budgets Budget)
        {
            foreach(Transactions Transaction in Budget.Transactions)
            {
                if(Transaction.TransactionDate.Date <= GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID).Date)
                {
                    if(Transaction.isSpendFromSavings)
                    {
                        TransactSavingsTransactionDaily(ref Budget, Transaction.TransactionID);
                    }
                    else
                    {
                        TransactTransactionDaily(ref Budget, Transaction.TransactionID);
                    }
                    Budget.PayPeriodStats[0].SpendToDate += Transaction.TransactionAmount ?? 0;
                    Transaction.isTransacted = true;
                }
            }
        }

        public void TransactSavingsTransactionDaily(ref Budgets Budget, int ID)
        {
            Transactions T = Budget.Transactions.Where(t => t.TransactionID == ID).First();
            int TransactionsSavingsID = T.SavingID ?? 0;

            Savings S = Budget.Savings.Where(s => s.SavingID == TransactionsSavingsID).First();

            if (T.SavingsSpendType == "UpdateValues")
            {
                if (T.isIncome)
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;
                    Budget.PayPeriodStats[0].SavingsToDate += T.TransactionAmount ?? 0;
                    S.CurrentBalance += T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                }
                else
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    S.CurrentBalance -= T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                    Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
                }

                //Update Regular Saving Value
                RecalculateRegularSavingFromTransaction(ref S);
                
            }
            else if (T.SavingsSpendType == "MaintainValues")
            {
                if (T.isIncome)
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    S.CurrentBalance += T.TransactionAmount;
                    S.SavingsGoal += T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                    Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;
                    Budget.PayPeriodStats[0].SavingsToDate += T.TransactionAmount ?? 0;
                }
                else
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    S.CurrentBalance -= T.TransactionAmount;
                    S.SavingsGoal -= T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                    Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
                }
            }
            else if (T.SavingsSpendType == "BuildingSaving" | T.SavingsSpendType == "EnvelopeSaving")
            {
                if (T.isIncome)
                {
                    Budget.BankBalance += T.TransactionAmount;
                    Budget.MoneyAvailableBalance += T.TransactionAmount;
                    S.CurrentBalance += T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                    Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;
                    Budget.PayPeriodStats[0].SavingsToDate += T.TransactionAmount ?? 0;
                }
                else
                {
                    Budget.BankBalance -= T.TransactionAmount;
                    Budget.MoneyAvailableBalance -= T.TransactionAmount;
                    S.CurrentBalance -= T.TransactionAmount;
                    S.LastUpdatedValue = T.TransactionAmount;
                    S.LastUpdatedDate = DateTime.UtcNow;
                    Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
                }
            }
        }

        public void TransactTransactionDaily(ref Budgets Budget, int ID)
        {
            
            Transactions T = Budget.Transactions.Where(t => t.TransactionID == ID).First();

            if (T.isIncome)
            {
                Budget.BankBalance += T.TransactionAmount;
                Budget.MoneyAvailableBalance += T.TransactionAmount;
                Budget.LeftToSpendBalance += T.TransactionAmount;
                //Recalculate how much you have left to spen
                int DaysToPayDay = (int)Math.Ceiling((Budget.NextIncomePayday.GetValueOrDefault().Date - DateTime.Today.Date).TotalDays);
                Budget.LeftToSpendDailyAmount = (Budget.LeftToSpendBalance ?? 0) / DaysToPayDay;
                Budget.PayPeriodStats[0].IncomeToDate += T.TransactionAmount ?? 0;                    
            }
            else
            {
                Budget.BankBalance -= T.TransactionAmount;
                Budget.MoneyAvailableBalance -= T.TransactionAmount;
                Budget.LeftToSpendBalance -= T.TransactionAmount;
                Budget.LeftToSpendDailyAmount -= T.TransactionAmount ?? 0;
                Budget.PayPeriodStats[0].SpendToDate += T.TransactionAmount ?? 0;
            }

        }

        public void UpdateSavingsDaily(ref Budgets Budget)
        {
            foreach(Savings Saving in Budget.Savings)
            {
                if(Saving.isRegularSaving)
                {
                    if(Saving.SavingsType == "SavingsBuilder")
                    {
                        Saving.CurrentBalance = Saving.CurrentBalance + Saving.RegularSavingValue;
                        Saving.LastUpdatedValue = Saving.RegularSavingValue;
                        Saving.LastUpdatedDate = DateTime.UtcNow.Date;
                        Budget.PayPeriodStats[0].SavingsToDate += Saving.RegularSavingValue ?? 0;
                    }
                    else if (Saving.SavingsType == "TargetAmount")
                    {
                        if(Saving.SavingsGoal > Saving.CurrentBalance)
                        {

                            decimal? Amount;
                            if(Saving.canExceedGoal)
                            {
                                Amount = Saving.RegularSavingValue;
                            }
                            else
                            {
                                Amount = (Saving.SavingsGoal - Saving.CurrentBalance) < Saving.RegularSavingValue ? (Saving.SavingsGoal - Saving.CurrentBalance) : Saving.RegularSavingValue;
                            }

                            Saving.CurrentBalance += Amount;
                            Saving.LastUpdatedValue = Amount;
                            Saving.LastUpdatedDate = DateTime.UtcNow.Date;
                            Budget.PayPeriodStats[0].SavingsToDate += Saving.RegularSavingValue ?? 0;

                            if(Saving.CurrentBalance == Saving.SavingsGoal & Saving.isAutoComplete)
                            {
                                Saving.isSavingsClosed = true;
                            }
                        }
                    }
                    else if (Saving.SavingsType == "TargetDate")
                    {
                        decimal? Amount;
                        if (Saving.GoalDate.GetValueOrDefault().Date <= GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID).Date)
                        {
                            Amount = (Saving.SavingsGoal - Saving.CurrentBalance);
                            Saving.CurrentBalance += Amount;
                            Saving.LastUpdatedValue = Amount;
                            Saving.LastUpdatedDate = DateTime.UtcNow.Date;
                            if (Saving.isAutoComplete)
                            {
                                Saving.isSavingsClosed = true;
                            }
                            Budget.PayPeriodStats[0].SavingsToDate += Saving.RegularSavingValue ?? 0;                            
                        }
                        else
                        {
                            Amount = Saving.RegularSavingValue;
                            Saving.CurrentBalance += Amount;
                            Saving.LastUpdatedValue = Amount;
                            Saving.LastUpdatedDate = DateTime.UtcNow.Date;
                            Budget.PayPeriodStats[0].SavingsToDate += Saving.RegularSavingValue ?? 0;
                        }
                    }
                }
            }
        }

        public string UpdateApproxDaysBetweenPay(int BudgetID)
        {
            Budgets? Budget = _db.Budgets?
                .Where(x => x.BudgetID == BudgetID)
                .FirstOrDefault();

            if (Budget != null)
            {
                int DaysBetweenPayDay = CalculateBudgetDaysBetweenPay(Budget);

                Budget.AproxDaysBetweenPay = DaysBetweenPayDay;

                _db.SaveChanges();

                return "OK";
            }
            else
            {
                return "Budget not found";
            }

        }

        public int CalculateBudgetDaysBetweenPay(Budgets Budget)
        {
            int NumberOfDays = 30;

            if (Budget.PaydayType == "Everynth")
            {
                int Duration = new int();
                if (Budget.PaydayDuration == "days")
                {
                    Duration = 1;
                }
                else if (Budget.PaydayDuration == "weeks")
                {
                    Duration = 7;
                }
                else if (Budget.PaydayDuration == "years")
                {
                    Duration = 365;
                }

                NumberOfDays = Duration * Budget.PaydayValue ?? 30;
            }
            else if (Budget.PaydayType == "WorkingDays")
            {
                int? NumberOfDaysBefore = Budget.PaydayValue;
                NumberOfDays = GetNumberOfDaysLastWorkingDay(NumberOfDaysBefore, Budget.BudgetID);
            }
            else if (Budget.PaydayType == "OfEveryMonth")
            {
                int year = GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID).Year;
                int month = GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID).Month;
                int days = DateTime.DaysInMonth(year, month);
                NumberOfDays = days;
            }
            else if (Budget.PaydayType == "LastOfTheMonth")
            {
                int dayNumber = ((int)Enum.Parse(typeof(DayOfWeek), Budget.PaydayDuration));
                NumberOfDays = GetNumberOfDaysLastDayOfWeek(dayNumber, Budget.BudgetID);
            }

            return NumberOfDays;
        }

        private int GetNumberOfDaysLastDayOfWeek(int dayNumber, int BudgetID)
        {

            int year = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Year;
            int month = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Month;

            int NextYear = new int();
            int NextMonth = new int();

            if (month != 12)
            {
                NextYear = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Year;
                NextMonth = month + 1;
            }
            else
            {
                NextYear = year + 1;
                NextMonth = 1;
            }

            DateTime CurrentDate = new DateTime();

            var i = DateTime.DaysInMonth(year, month);
            while (i > 0)
            {
                var dtCurrent = new DateTime(year, month, i);
                if ((int)dtCurrent.DayOfWeek == dayNumber)
                {
                    CurrentDate = dtCurrent;
                    i = 0;
                }
                else
                {
                    i = i - 1;
                }
            }


            DateTime NextDate = new DateTime();

            i = DateTime.DaysInMonth(NextYear, NextMonth);
            while (i > 0)
            {
                var dtCurrent = new DateTime(NextYear, NextMonth, i);
                if ((int)dtCurrent.DayOfWeek == dayNumber)
                {
                    NextDate = dtCurrent;
                    i = 0;
                }
                else
                {
                    i = i - 1;
                }
            }

            int DaysBetweenPay = (int)Math.Ceiling((NextDate.Date - CurrentDate.Date).TotalDays);

            return DaysBetweenPay;
        }

        private int GetNumberOfDaysLastWorkingDay(int? NumberOfDaysBefore, int BudgetID)
        {
            if (NumberOfDaysBefore == null)
            {
                NumberOfDaysBefore = 1;
            }

            int year = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Year;
            int month = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Month;

            int NextYear = new int();
            int NextMonth = new int();

            if (month != 12)
            {
                NextYear = GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Year;
                NextMonth = month + 1;
            }
            else
            {
                NextYear = year + 1;
                NextMonth = 1;
            }

            DateTime CurrentDate = new DateTime();
            var i = DateTime.DaysInMonth(year, month);
            int j = 1;
            while (i > 0)
            {
                var dtCurrent = new DateTime(year, month, i);
                if (dtCurrent.DayOfWeek < DayOfWeek.Saturday && dtCurrent.DayOfWeek > DayOfWeek.Sunday)
                {
                    CurrentDate = dtCurrent;
                    if (j == NumberOfDaysBefore)
                    {
                        i = 0;
                    }
                    else
                    {
                        i = i - 1;
                        j = j + 1;
                    }
                }
                else
                {
                    i = i - 1;
                }
            }

            DateTime NextDate = new DateTime();
            i = DateTime.DaysInMonth(NextYear, NextMonth);
            j = 1;
            while (i > 0)
            {
                var dtCurrent = new DateTime(NextYear, NextMonth, i);
                if (dtCurrent.DayOfWeek < DayOfWeek.Saturday && dtCurrent.DayOfWeek > DayOfWeek.Sunday)
                {
                    NextDate = dtCurrent;
                    if (j == NumberOfDaysBefore)
                    {
                        i = 0;
                    }
                    else
                    {
                        i = i - 1;
                        j = j + 1;
                    }
                }
                else
                {
                    i = i - 1;
                }
            }

            int DaysBetweenPay = (int)Math.Ceiling((NextDate.Date - CurrentDate.Date).TotalDays);

            return DaysBetweenPay;
        }

        public DateTime GetBudgetLocalTime(DateTime UtcDate, int BudgetID)
        {
            DateTime LocalDate = new DateTime();

            BudgetSettings BS = _db.BudgetSettings.Where(b => b.BudgetID == BudgetID).First();
            lut_BudgetTimeZone TimeZoneName = _db.lut_BudgetTimeZone.Where(t => t.TimeZoneID == BS.TimeZone).First();

            try
            {
                TimeZoneInfo BudgetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneName.TimeZoneName);
                LocalDate = TimeZoneInfo.ConvertTime(UtcDate, BudgetTimeZone);
            }
            catch (TimeZoneNotFoundException ex)
            {
                LocalDate = UtcDate.AddHours(TimeZoneName.TimeZoneUTCOffset);
            }

            return LocalDate;

        }

        public async Task ReadFireBaseAdminSdk()
        {
            string fileName = "Data/dbudget-c353b-firebase-adminsdk-us5ve-ea3c5cb31d.json";
            string jsonString = System.IO.File.ReadAllText(fileName, Encoding.UTF8);

            if(FirebaseMessaging.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromJson(jsonString)

                }); 
            }
        }

        public async Task<string> SendPushNotification(Message Message)
        {
            await ReadFireBaseAdminSdk();
            try
            {
                var Response = await FirebaseMessaging.DefaultInstance.SendAsync(Message);
                return "OK";
            }
            catch(Exception)
            {
                return "Bad Request";
            }            

        }

        public async Task<string> SendAllPushNotification(List<Message> Messages)
        {
            await ReadFireBaseAdminSdk();
            try
            {
                var Response = await FirebaseMessaging.DefaultInstance.SendAllAsync(Messages);
                return "OK";
            }
            catch (Exception)
            {
                return "Bad Request";
            }
        }
    }

}

