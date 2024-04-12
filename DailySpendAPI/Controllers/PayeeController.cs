using AutoMapper;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/payee")]
    public class PayeeController : Controller
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public PayeeController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpGet("getpayeelist/{BudgetId}")]
        public IActionResult GetPayeeList([FromRoute] int BudgetId)
        {
            try
            {
                Budgets? Budget = _db.Budgets?
                .Include(x => x.Transactions)
                .Where(x => x.BudgetID == BudgetId)
                .FirstOrDefault();

                List<string>? Payee = new List<string>();

                Payee = Budget.Transactions.Where(t => !string.IsNullOrEmpty(t.Payee)).OrderBy(t => t.Payee).Select(t => t.Payee).Distinct().ToList();

                if (Budget != null)
                {
                    return Ok(Payee);
                }
                else
                {
                    return NotFound(new { ErrorMessage = "Budget Not Found" });
                }
            }
            catch (Exception ex) 
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getpayeelastcategory/{BudgetId}/{PayeeName}")]
        public IActionResult GetPayeeLastCategory([FromRoute] int BudgetId, [FromRoute] string PayeeName)
        {
            try
            {
                Budgets? Budget = _db.Budgets?
                .Include(x => x.Transactions)
                .Where(x => x.BudgetID == BudgetId)
                .FirstOrDefault();

                Transactions? transactions = Budget.Transactions.Where(t => t.Payee == PayeeName && t.Payee != null).OrderByDescending(t => t.TransactionID).FirstOrDefault();


                if (Budget != null)
                {
                    if (transactions != null)
                    {
                        Categories Category = new Categories
                        {
                            CategoryName = transactions.Category ?? "",
                            CategoryID = transactions.CategoryID.GetValueOrDefault()
                        };
                        return Ok(Category);
                    }
                    else
                    {
                        Categories Category = new Categories
                        {
                            CategoryName = "",
                            CategoryID = 0
                        };
                        return Ok(Category);
                    }
                }
                else
                {
                    return NotFound(new { ErrorMessage = "Budget Not Found" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getpayeelistfull/{BudgetId}")]
        public IActionResult GetPayeeListFull([FromRoute] int BudgetId)
        {
            try
            {       
                Budgets? Budget = _db.Budgets?
                .Include(x => x.Transactions)
                .Include(x => x.PayPeriodStats.OrderByDescending(p => p.PayPeriodID).Where(p => p.EndDate >= DateTime.UtcNow.AddYears(-1)))
                .Where(x => x.BudgetID == BudgetId)
                .FirstOrDefault();

                List<Payees> payees = new List<Payees>();
                List<string>? PayeesNames = new List<string>();

                PayeesNames = Budget.Transactions.Where(t => !string.IsNullOrEmpty(t.Payee)).OrderBy(t => t.Payee).Select(t => t.Payee).Distinct().ToList();

                foreach(string payeeName in PayeesNames) 
                { 
                    Payees p = new Payees
                    {
                        Payee = payeeName
                    };

                    payees.Add(p);
                }

                DateTime PayPeriodStartDate = Budget.PayPeriodStats.Where(p => p.isCurrentPeriod).Select(p => p.StartDate).FirstOrDefault();

                foreach (Payees payee in payees)
                {
                    var TotalAmount = Budget.Transactions.Where(t => t.isTransacted && t.Payee == payee.Payee).Sum(t => t.TransactionAmount);
                    payee.PayeeSpendAllTime = TotalAmount.GetValueOrDefault();

                    var PayPeriodTotalAmount = Budget.Transactions.Where(t => t.isTransacted && t.TransactionDate > PayPeriodStartDate && t.Payee == payee.Payee).Sum(t => t.TransactionAmount);
                    payee.PayeeSpendPayPeriod = PayPeriodTotalAmount.GetValueOrDefault();

                    foreach (var PayPeriod in Budget.PayPeriodStats)
                    {
                        DateTime FromDate = PayPeriod.StartDate;
                        DateTime ToDate = PayPeriod.EndDate;

                        decimal SpendTotalAmount = Budget.Transactions.Where(t => t.isTransacted && t.TransactionDate > FromDate && t.TransactionDate <= ToDate && t.Payee == payee.Payee).Sum(t => t.TransactionAmount).GetValueOrDefault();

                        SpendPeriods spendPeriods = new SpendPeriods
                        {
                            FromDate = FromDate,
                            ToDate = ToDate,
                            SpendTotalAmount = SpendTotalAmount,
                            IsCurrentPeriod = PayPeriod.isCurrentPeriod
                        };

                        payee.PayeeSpendPeriods.Add(spendPeriods);
                    }
                }

                if (Budget != null)
                {
                    return Ok(payees);
                }
                else
                {
                    return NotFound(new { ErrorMessage = "Budget Not Found" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("deletepayee/{BudgetId}/{OldPayeeName}/{NewPayeeName}")]
        public IActionResult DeletePayee([FromRoute] int BudgetId, [FromRoute] string OldPayeeName, [FromRoute] string NewPayeeName)
        {
            try
            {
                if(BudgetId == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID can not be 0" });
                }

                if(string.IsNullOrEmpty(OldPayeeName))
                {
                    return BadRequest(new { ErrorMessage = "Must provide a payee" });
                }

                Budgets b = _db.Budgets.Include(b => b.Transactions).Where(b => b.BudgetID == BudgetId).FirstOrDefault();

                foreach(Transactions t in b.Transactions)
                {
                    if(t.Payee == OldPayeeName)
                    {
                        t.Payee = NewPayeeName;
                        _db.Update(t);
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

        [HttpGet("updatepayee/{BudgetId}/{OldPayeeName}/{NewPayeeName}")]
        public IActionResult UpdatePayee([FromRoute] int BudgetId, [FromRoute] string OldPayeeName, [FromRoute] string NewPayeeName)
        {
            try
            {
                if (BudgetId == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID can not be 0" });
                }

                if (string.IsNullOrEmpty(OldPayeeName))
                {
                    return BadRequest(new { ErrorMessage = "Must provide a payee" });
                }

                Budgets b = _db.Budgets.Include(b => b.Transactions).Where(b => b.BudgetID == BudgetId).FirstOrDefault();

                foreach (Transactions t in b.Transactions)
                {
                    if (t.Payee == OldPayeeName)
                    {
                        t.Payee = NewPayeeName;
                        _db.Update(t);
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
    }

}
