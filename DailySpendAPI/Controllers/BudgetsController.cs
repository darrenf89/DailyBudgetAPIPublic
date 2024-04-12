using AutoMapper;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FirebaseAdmin.Messaging;
using Microsoft.IdentityModel.Tokens;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/budgets")]
    public class BudgetsController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        private readonly IEmailService _es;

        public BudgetsController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt, IEmailService es)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
            _es = es;
        }

        [HttpGet("nextincomepayday/{BudgetId}")]
        public IActionResult GetNextIncomePayday([FromRoute] int BudgetId)
        {
            Budgets Budget = _db.Budgets
                .Where(b => b.BudgetID == BudgetId)
                .First();

            if (Budget != null)
            {
                return Ok(new { NextIncomePayday = Budget.NextIncomePayday.GetValueOrDefault() });
            }
            else
            {
                return NotFound();
            }

        }

        [HttpGet("daystopaydaynext/{BudgetId}")]
        public IActionResult DaysToPayDayNext([FromRoute] int BudgetId)
        {
            Budgets Budget = _db.Budgets
                .Where(b => b.BudgetID == BudgetId)
                .First();

            if (Budget != null)
            {
                int DaysBetweenPayDay = _pt.CalculateBudgetDaysBetweenPay(Budget);

                return Ok(new { AproxDaysBetweenPay = DaysBetweenPayDay });
            }
            else
            {
                return NotFound();
            }

        }

        [HttpGet("getbudgetvalueslastupdated/{BudgetId}")]
        public IActionResult GetBudgetValuesLastUpdated([FromRoute] int BudgetId)
        {
            Budgets? Budget = _db.Budgets
                .Where(b => b.BudgetID == BudgetId).FirstOrDefault();


            return Budget != null ? Ok(new { Budget.BudgetValuesLastUpdated }) : NotFound();

        }

        [HttpGet("getlastupdated/{BudgetId}")]
        public IActionResult GetLastUpdated([FromRoute] int BudgetId)
        {
            Budgets? Budget = _db.Budgets
                .Where(b => b.BudgetID == BudgetId).FirstOrDefault();

            return Budget != null ? Ok(new { Budget.LastUpdated }) : NotFound();

        }

        [HttpGet("getbudgetdetailsonly/{BudgetId}")]
        public IActionResult GetBudgetDetailsOnly([FromRoute] int BudgetId)
        {
            Budgets? Budget = _db.Budgets.Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                .Where(b => b.BudgetID == BudgetId)
                .FirstOrDefault();

            if (Budget == null)
            {
                return NotFound(new { ErrorMessage = "Budget Not found" });
            }

            UserAccounts? User = _db.UserAccounts.Where(u=>u.Budgets.Contains(Budget)).FirstOrDefault();

            ShareBudgetRequest? ShareRequest = _db.ShareBudgetRequest.Where(b => b.SharedWithUserAccountID == User.UserID && !b.IsVerified).FirstOrDefault();

            DateTime PeriodStart = Budget.PayPeriodStats[0].StartDate;
            Budgets? BudgetWithTrans = _db.Budgets.Where(b=>b.BudgetID == BudgetId).Include(b=>b.Transactions.Where(t=>t.TransactionDate.Date >= PeriodStart.Date && t.isTransacted)).FirstOrDefault();
            
            foreach(Transactions T in BudgetWithTrans.Transactions)
            {
                if(!T.isIncome)
                {
                    Budget.AccountInfo.TransactionValueThisPeriod += T.TransactionAmount.GetValueOrDefault();
                    if(T.TransactionDate.Date == _pt.GetBudgetLocalTime(DateTime.UtcNow, BudgetId).Date)
                    {
                        Budget.AccountInfo.TransactionValueToday += T.TransactionAmount.GetValueOrDefault();
                        Budget.AccountInfo.NumberOfTransactionsToday += 1;
                    }
                }
            }

            Budget.AccountInfo.IncomeThisPeriod = Budget.PayPeriodStats[0].IncomeToDate;
            
            if(ShareRequest != null)
            {
                Budget.AccountInfo.BudgetShareRequestID = ShareRequest.SharedBudgetRequestID;
            }   

            Budget.AccountInfo.NumberOfTransactions = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.Transactions).Count();
            Budget.AccountInfo.NumberOfSavings = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.Savings).Count();
            Budget.AccountInfo.NumberOfBills = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.Bills).Count();
            Budget.AccountInfo.NumberOfIncomeEvents = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.IncomeEvents).Count();

            if (Budget != null)
            {
                Budget.Transactions.Clear();
                Budget.Savings.Clear();
                Budget.IncomeEvents.Clear();
                Budget.Bills.Clear();
                Budget.PayPeriodStats.Clear();

                return Ok(Budget);
            }
            else
            {
                return NotFound(new { ErrorMessage = "Budget Not found" });
            }

        }

        [HttpGet("getbudgetdetailsfull/{BudgetId}")]
        public IActionResult GetBudgetDetailsFull([FromRoute] int BudgetId)
        {
            Budgets? Budget = _db.Budgets
                .Include(x => x.IncomeEvents.Where(i => !i.isClosed).OrderByDescending(i => i.IncomeEventID))
                .Include(x => x.Savings.Where(s => !(s.isSavingsClosed && s.CurrentBalance == 0)).OrderByDescending(i => i.SavingID))
                .Include(x => x.Bills.Where(b => !b.isClosed).OrderByDescending(i => i.BillID))
                .Include(x => x.Transactions.Where(t => !t.isTransacted).OrderByDescending(i => i.TransactionID))
                .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                .Where(b => b.BudgetID == BudgetId)
                .FirstOrDefault();

            if(Budget == null)
            {
                return NotFound(new { ErrorMessage = "Budget Not found" });
            }

            UserAccounts? User = _db.UserAccounts.Where(u=>u.Budgets.Contains(Budget)).FirstOrDefault();

            ShareBudgetRequest? ShareRequest = _db.ShareBudgetRequest.Where(b => b.SharedWithUserAccountID == User.UserID && !b.IsVerified && b.RequestInitiated.AddDays(5) >= DateTime.UtcNow).FirstOrDefault();

            DateTime PeriodStart = Budget.PayPeriodStats[0].StartDate;
            Budgets? BudgetWithTrans = _db.Budgets.Where(b=>b.BudgetID == BudgetId).Include(b=>b.Transactions.Where(t=>t.TransactionDate.Date >= PeriodStart.Date && t.isTransacted)).FirstOrDefault();
            

            foreach(Transactions T in BudgetWithTrans.Transactions)
            {
                if(!T.isIncome)
                {
                    Budget.AccountInfo.TransactionValueThisPeriod += T.TransactionAmount.GetValueOrDefault();
                    if(T.TransactionDate.Date == _pt.GetBudgetLocalTime(DateTime.UtcNow, BudgetId).Date)
                    {
                        Budget.AccountInfo.TransactionValueToday += T.TransactionAmount.GetValueOrDefault();
                        Budget.AccountInfo.NumberOfTransactionsToday += 1;
                    }
                }
            }

            Budget.AccountInfo.IncomeThisPeriod = Budget.PayPeriodStats[0].IncomeToDate;
            
            if(ShareRequest != null)
            {
                Budget.AccountInfo.BudgetShareRequestID = ShareRequest.SharedBudgetRequestID;
            }   

            Budget.AccountInfo.NumberOfTransactions = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.Transactions).Count();
            Budget.AccountInfo.NumberOfSavings = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.Savings).Count();
            Budget.AccountInfo.NumberOfBills = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.Bills).Count();
            Budget.AccountInfo.NumberOfIncomeEvents = _db.Budgets.Where(o => o.BudgetID == BudgetId).SelectMany(o => o.IncomeEvents).Count();


            if (Budget != null)
            {
                return Ok(Budget);
            }
            else
            {
                return NotFound(new { ErrorMessage = "Budget Not found" });
            }

        }

        [HttpGet("createnewbudget/{UserEmail}/{BudgetType?}")]
        public IActionResult CreateNewBudget([FromRoute] string UserEmail, [FromRoute] string? BudgetType = "Basic")
        {
            try
            {
                UserAccounts User = _db.UserAccounts.Where(u => u.Email == UserEmail).FirstOrDefault();

                if(User != null)
                {
                    Budgets Budget = new Budgets();

                    Budget.IsCreated = false;
                    Budget.BudgetCreatedOn = DateTime.UtcNow;
                    Budget.LastUpdated = DateTime.UtcNow;
                    Budget.BudgetType = BudgetType;

                    User.Budgets.Add(Budget);
                    _db.SaveChanges();

                    _pt.CreateDefaultCategories(Budget.BudgetID);

                    BudgetSettings budgetSettings = new BudgetSettings();
                    budgetSettings.BudgetID = Budget.BudgetID;

                    _db.BudgetSettings.Add(budgetSettings);
                    _db.SaveChanges();

                    _pt.CreateNewPayPeriodStats(Budget.BudgetID);

                    return Ok(new { BudgetID = Budget.BudgetID });
                }
                else
                {
                    return NotFound(new { ErrorMessage = "User not found" });
                }
            }
            catch (Exception ex) 
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpPatch("updatebudget/{BudgetID}")]
        public IActionResult UpdateBudget([FromRoute] int BudgetID, JsonPatchDocument<Budgets> PatchDoc)
        {
            try
            {
                var Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID).FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                PatchDoc.ApplyTo(Budget, ModelState);

                if (!TryValidateModel(Budget))
                {
                    return ValidationProblem(ModelState);
                }

                foreach (var operation in PatchDoc.Operations)
                {
                    if(operation.path.Contains("IsBorrowPay") || operation.path.Contains("PayDayAmount") || operation.path.Contains("NextIncomePayday"))
                    {
                        _pt.RecalculateAfterTransactionUpdate(BudgetID, 0);
                    }
                }

                _db.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("updatebudgetvalues/{BudgetID}")]
        public IActionResult UpdateBudgetValues([FromRoute] int BudgetID)
        {
            try
            {

                if (BudgetID != 0)
                {
                    string status;

                    status = _pt.UpdateBudget(BudgetID);

                    if (status == "OK") 
                    {
                        return Ok();
                    }
                    else
                    {
                        return BadRequest(new { ErrorMessage = status });
                    }                    
                }
                else
                {
                    return NotFound(new { ErrorMessage = "Budget ID not found" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("createnewpayperiodstats/{BudgetId}")]
        public IActionResult CreateNewPayPeriodStats([FromRoute] int BudgetId)
        {

            if (BudgetId != 0)
            {
                PayPeriodStats stats = _pt.CreateNewPayPeriodStats(BudgetId);
                return Ok(stats);
            }
            else
            {
                return NotFound();
            }

        }

        [HttpPost("updatepayperiodstats")]
        public IActionResult UpdatePayPeriodStats([FromBody] PayPeriodStats Stats)
        {
            if (Stats != null && Stats.PayPeriodID != 0)
            {
                _db.Attach(Stats);
                _db.Update(Stats);
                _db.SaveChanges();

                return Ok();
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("savebudgetdailycycle")]
        public IActionResult SaveBudgetDailyCycle([FromBody] Budgets Budget)
        {
            if (Budget != null && Budget.BudgetID != 0)
            {
                _db.Attach(Budget);
                _db.Update(Budget);

                foreach(Savings saving in Budget.Savings)
                {
                    _db.Attach(saving);
                    _db.Update(saving);
                }

                foreach (Bills Bill in Budget.Bills)
                {
                    _db.Attach(Bill);
                    _db.Update(Bill);
                }

                foreach (IncomeEvents Income in Budget.IncomeEvents)
                {
                    _db.Attach(Income);
                    _db.Update(Income);
                }

                foreach (Transactions Transaction in Budget.Transactions)
                {
                    _db.Attach(Transaction);
                    _db.Update(Transaction);
                }


                _db.SaveChanges();

                int BudgetId = Budget.BudgetID;
                Budget = _db.Budgets
                    .Include(x => x.IncomeEvents.Where(i => !i.isClosed).OrderByDescending(i => i.IncomeEventID))
                    .Include(x => x.Savings.Where(s => !(s.isSavingsClosed && s.CurrentBalance == 0)).OrderByDescending(i => i.SavingID))
                    .Include(x => x.Bills.Where(b => !b.isClosed).OrderByDescending(i => i.BillID))
                    .Include(x => x.Transactions.Where(t => !t.isTransacted).OrderByDescending(i => i.TransactionID))
                    .Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                    .Where(b => b.BudgetID == BudgetId)
                    .First();
                
                return Ok(Budget);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("sharebudgetrequest")]
        public IActionResult ShareBudgetRequest([FromBody] ShareBudgetRequest BudgetShare)
        {
            try
            {
                if (BudgetShare == null)
                {
                    return BadRequest(new { ErrorMessage = "Budget Share Details not correct" });
                }

                UserAccounts? WithUserAccount = _db.UserAccounts.Where(u => u.Email == BudgetShare.SharedWithUserEmail).FirstOrDefault();

                if(WithUserAccount == null) 
                {
                    return BadRequest(new { ErrorMessage = "User Not Found" });
                }

                BudgetShare.SharedWithUserAccountID = WithUserAccount.UserID;

                Budgets Budget = _db.Budgets.Where(u => u.BudgetID == BudgetShare.SharedBudgetID).First();

                if(Budget == null) 
                {
                    return BadRequest(new { ErrorMessage = "Budget Not Found" });
                }


                if(Budget.IsSharedValidated)
                {
                    return BadRequest(new { ErrorMessage = "Budget Already Shared" });
                }

                if(Budget.SharedUserID != 0)
                {
                    return BadRequest(new { ErrorMessage = "Share Request Active" });
                }

                _db.Attach(Budget);

                Budget.SharedUserID = BudgetShare.SharedWithUserAccountID;
                Budget.IsSharedValidated = false;

                _db.Add(BudgetShare);

                OTP NewOTP = new OTP();
                NewOTP.OTPCode = (BetterRandom.NextInt() % 100000000).ToString("00000000");
                NewOTP.OTPExpiryTime = DateTime.UtcNow.AddDays(5);
                NewOTP.UserAccountID = BudgetShare.SharedWithUserAccountID;
                NewOTP.IsValidated = false;
                NewOTP.OTPType = "ShareBudget";

                _db.Add(NewOTP);
                _db.SaveChanges();

                try                
                {
                    string status = _es.SendTransactionEmail(_es.CreateShareBudgetEmail(BudgetShare.SharedByUserEmail, NewOTP));

                    if (status == "OK")
                    {
                        //Send Push Notification To Share With User
                        FirebaseDevices? Device = _db.FirebaseDevices.Where(u => u.UserAccountID == BudgetShare.SharedWithUserAccountID && u.LoginExpiryDate >= DateTime.UtcNow).OrderByDescending(u => u.LoginExpiryDate).FirstOrDefault();

                        if(Device != null)
                        {
                            Dictionary<string, string> AndroidData = new Dictionary<string, string>
                            {
                                { "NavigationType", "ShareBudget" },
                                { "NavigationID", BudgetShare.SharedBudgetRequestID.ToString() }
                            };

                            Message Message = new Message
                            {
                                Token = Device.FirebaseToken,
                                Notification = new Notification
                                {
                                    Title = "Share a budget with a friend!",
                                    Body = $"{BudgetShare.SharedByUserEmail} wants to share their budget with you, so you can budget together!"
                                },
                                Data = AndroidData
                            };

                            status = _pt.SendPushNotification(Message).Result;
                        }
   
                        return Ok(BudgetShare);
                                                
                    }
                    else
                    {
                        _db.Remove(NewOTP);
                        _db.SaveChanges();
                        return BadRequest(new { ErrorMessage = "Email Did not send" });
                    }

                }
                catch (Exception ex)
                {
                    _db.Remove(NewOTP);
                    _db.SaveChanges();
                    return BadRequest(new { ErrorMessage = ex.Message });
                }



            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getsharebudgetrequestbyid/{ShareBudgetRequestID}")]
        public IActionResult GetShareBudgetRequestByID([FromRoute] int ShareBudgetRequestID)
        {
            try
            {
                ShareBudgetRequest? ShareRequest = _db.ShareBudgetRequest.Where(s=>s.SharedBudgetRequestID == ShareBudgetRequestID).FirstOrDefault();

                if (ShareRequest == null)
                {
                    return BadRequest(new { ErrorMessage = "No Share Request" });
                }
                else
                {
                    return Ok(ShareRequest);
                }
                
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("cancelcurrentsharebudgetrequest/{BudgetID}")]
        public IActionResult CancelCurrentShareBudgetRequest([FromRoute] int BudgetID)
        {
            try
            {
                if(BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "No BudgetID" });
                }

                Budgets? budget = _db.Budgets.Where(s => s.BudgetID == BudgetID).FirstOrDefault();

                if (budget == null) 
                {
                    return BadRequest(new { ErrorMessage = "No Budget Found" });
                }

                budget.SharedUserID = 0;
                budget.IsSharedValidated = false;

                _db.Update(budget);

                List<ShareBudgetRequest>? ShareRequests = _db.ShareBudgetRequest.Where(s => s.SharedBudgetID == BudgetID).ToList();

                foreach(ShareBudgetRequest shareBudgetRequest in ShareRequests)
                {
                    _db.Remove(shareBudgetRequest);
                }

                if(User == null)
                {
                    return BadRequest(new { ErrorMessage = "User not found" });
                }   

                List<OTP> OTPs = _db.OTP.Where(o => o.UserAccountID == budget.SharedUserID && o.OTPType == "ShareBudget" && !o.IsValidated).ToList();

                foreach (OTP oTP in OTPs)
                {
                    _db.Remove(oTP);
                }

                _db.SaveChanges();
                return Ok();
                

            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }


        [HttpGet("stopsharingbudget/{BudgetID}")]
        public IActionResult StopSharingBudget([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "No BudgetID" });
                }

                Budgets? budget = _db.Budgets.Where(s => s.BudgetID == BudgetID).FirstOrDefault();

                if (budget == null)
                {
                    return BadRequest(new { ErrorMessage = "No Budget Found" });
                }

                budget.SharedUserID = 0;
                budget.IsSharedValidated = false;

                _db.Update(budget);               

                _db.SaveChanges();
                return Ok();


            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("deletebudget/{BudgetID}/{UserID}")]
        public IActionResult DeleteBudget([FromRoute] int BudgetID, [FromRoute] int UserID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "No BudgetID" });
                }

                Budgets? budget = _db.Budgets
                    .Include(b => b.Savings)
                    .Include(b => b.IncomeEvents)
                    .Include(b => b.Bills)
                    .Include(b => b.Transactions)
                    .Include(b => b.PayPeriodStats)
                    .Include(b => b.Categories)
                    .Include(b => b.BudgetHistory)
                    .Where(s => s.BudgetID == BudgetID).FirstOrDefault();
                
                
                List<BudgetSettings>? BS = _db.BudgetSettings.Where(b => b.BudgetID == BudgetID).ToList();
                List<ShareBudgetRequest>? SBR = _db.ShareBudgetRequest.Where(b => b.SharedBudgetID == BudgetID).ToList();

                if (budget == null)
                {
                    return BadRequest(new { ErrorMessage = "No Budget Found" });
                }

                UserAccounts? BudgetUser = _db.UserAccounts
                    .Include(u=>u.Budgets)
                    .Where(u => u.Budgets.Contains(budget)).FirstOrDefault();

                if(BudgetUser.UserID != UserID)
                {
                    return Ok(new { result = "SharedBudget" });
                }

                UserAccounts? User = _db.UserAccounts
                    .Include(u=>u.Budgets)
                    .Where(u => u.UserID == UserID).FirstOrDefault();

                if(User.Budgets.Count() == 1)
                {
                    return Ok(new { result = "LastBudget" });
                }

                foreach (Savings s in budget.Savings)
                {
                    _db.Remove(s);
                }

                foreach (IncomeEvents i in budget.IncomeEvents)
                {
                    _db.Remove(i);
                }

                foreach (Bills b in budget.Bills)
                {
                    _db.Remove(b);
                }

                foreach (Transactions t in budget.Transactions)
                {
                    _db.Remove(t);
                }

                foreach (PayPeriodStats p in budget.PayPeriodStats)
                {
                    _db.Remove(p);
                }

                foreach (Categories c in budget.Categories)
                {
                    _db.Remove(c);
                }

                foreach (BudgetHstoryLastPeriod b in budget.BudgetHistory)
                {
                    _db.Remove(b);
                }
                
                if(BS != null)
                {
                    foreach (BudgetSettings b in BS)
                    {
                        _db.Remove(b);
                    }
                }

                if(SBR != null)
                {
                    foreach (ShareBudgetRequest s in SBR)
                    {
                        _db.Remove(s);
                    }
                }

                _db.SaveChanges();

                _db.Remove(budget);
                _db.SaveChanges();

                return Ok(new { result = "OK" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }
    }
}
