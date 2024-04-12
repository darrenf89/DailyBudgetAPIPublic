using Microsoft.AspNetCore.Mvc;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using System.Linq;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/transactions")]
    public class TransactionsController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public TransactionsController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpGet("gettransactionfromid/{TransactionID}")]
        public IActionResult GetTransactionFromID([FromRoute] int TransactionID)
        {
            if(TransactionID == 0)
            {
                return BadRequest(new { ErrorMessage = "Transaction ID can not be zero" });
            }
            else
            {
                Transactions? Transactions = _db.Transactions.Where(t => t.TransactionID == TransactionID).FirstOrDefault();

                if(Transactions == null)
                {
                    return NotFound(new { ErrorMessage = $"No Transaction with Transaction ID {TransactionID} found"});
                }
                else
                {
                    return Ok(Transactions);
                }
            }
        }

        [HttpGet("getallbudgettransactions/{BudgetID}")]
        public IActionResult GetAllBudgetTransactions([FromRoute] int BudgetID)
        {
            if (BudgetID == 0)
            {
                return BadRequest(new { ErrorMessage = "Transaction ID can not be zero" });
            }
            else
            {
                Budgets? Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID)
                    .Include(t => t.Transactions.Where(t => t.isTransacted))
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = $"No budget with budget ID {BudgetID} found" });
                }
                else
                {
                    return Ok(Budget);
                }
            }
        }

        [HttpGet("transacttransaction/{TransactionID}")]
        public async Task<IActionResult> TransactTransaction([FromRoute] int TransactionID)
        {
            if (TransactionID == 0)
            {
                return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
            }
            else
            {
                Transactions? Transaction = _db.Transactions.Where(b => b.TransactionID == TransactionID).First();

                if (Transaction == null)
                {
                    return NotFound(new { ErrorMessage = $"Transaction with ID {TransactionID} not found" });
                }
                else
                {
                    try
                    {
                        _db.Attach(Transaction);

                        Budgets? Budget = _db.Budgets.Where(b => b.Transactions.Contains(Transaction)).First();


                        if (Budget == null)
                        {
                            return BadRequest(new { ErrorMessage = "Budget not found" });
                        }
                        else
                        {
                            string status = "OK";
                            if (Transaction.isSpendFromSavings)
                            {
                                status = _pt.TransactSavingsTransaction(ref Transaction, Budget.BudgetID);
                            }
                            else
                            {
                                status = _pt.TransactTransaction(ref Transaction, Budget.BudgetID);
                            }

                            _db.SaveChanges();

                            return Ok();
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { ErrorMessage = ex.Message });
                    }

                }
            }
        }

        [HttpPost("savenewtransaction/{BudgetID}")]
        public async Task<IActionResult> SaveNewTransaction([FromRoute] int BudgetID, [FromBody] Transactions Transaction)
        {
            if(BudgetID == 0)
            {
                return BadRequest(new { ErrorMessage = "Budget ID can not be zero"});
            }
            else
            {
                Budgets? Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID).First();

                if(Budget == null)
                {
                    return NotFound(new { ErrorMessage = $"Budget with ID {BudgetID} not found"});
                }
                else
                {
                    try
                    {
                        _db.Attach(Budget);
                        Budget.Transactions.Add(Transaction);
                        _db.SaveChanges();

                        if(Transaction.TransactionID == 0)
                        {
                            return BadRequest(new { ErrorMessage = "Transaction did not save correctly"});
                        }
                        else
                        {
                            if(Transaction.TransactionDate.Date <= _pt.GetBudgetLocalTime(DateTime.UtcNow, BudgetID).Date) 
                            {
                                string status = "OK";
                                if (Transaction.isSpendFromSavings)
                                {
                                    status = _pt.TransactSavingsTransaction(ref Transaction, BudgetID);
                                }
                                else
                                {
                                    status = _pt.TransactTransaction(ref Transaction, BudgetID);
                                }
                            }

                            _db.SaveChanges();

                            return Ok(Transaction);
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { ErrorMessage = ex.Message });
                    }

                }
            }
        }

        [HttpPost("updatetransaction")]
        public async Task<IActionResult> UpdateTransaction([FromBody] Transactions Transaction)
        {
            if(Transaction.TransactionID == 0)
            {
                return BadRequest(new { ErrorMessage = "Transaction does not already exsist" });
            }
            else
            {
                try
                {      
                    Transactions? T = _db.Transactions.Where(t => t.TransactionID == Transaction.TransactionID).FirstOrDefault();
                    Transactions OldTransactionDetails = T;
                    decimal OldTransactoinAmount = OldTransactionDetails.TransactionAmount.GetValueOrDefault();

                    _db.ChangeTracker.Clear();
                    _db.Attach(Transaction);

                    if (OldTransactionDetails.isTransacted) 
                    {
                        Budgets? Budget = _db.Budgets.Where(b => b.Transactions.Contains(Transaction)).First();
                        _db.Attach(Budget);
                        if (Transaction.TransactionDate.Date > _pt.GetBudgetLocalTime(DateTime.UtcNow, Budget.BudgetID).Date)
                        {
                            await _pt.UnTransact(OldTransactionDetails, Budget.BudgetID);
                            Transaction.isTransacted = false;
                            _db.Update(Transaction);
                            _db.SaveChanges();
                            string status = await _pt.RecalculateAfterTransactionUpdate(Budget.BudgetID, Transaction.TransactionID);                           

                        }
                        else if (OldTransactoinAmount != Transaction.TransactionAmount)
                        {
                            await _pt.UnTransact(OldTransactionDetails, Budget.BudgetID);
                            _db.Update(Transaction);
                            _db.SaveChanges();

                            _pt.TransactUpdate(Transaction, Budget.BudgetID);

                            await _pt.RecalculateAfterTransactionUpdate(Budget.BudgetID, Transaction.TransactionID);
                            _db.SaveChanges();
                        }
                        else if((OldTransactionDetails.isSpendFromSavings && !Transaction.isSpendFromSavings) || (!OldTransactionDetails.isSpendFromSavings && Transaction.isSpendFromSavings))
                        {
                            await _pt.UnTransact(OldTransactionDetails, Budget.BudgetID);
                            _db.Update(Transaction);
                            _db.SaveChanges();

                            _pt.TransactUpdate(Transaction, Budget.BudgetID);

                            await _pt.RecalculateAfterTransactionUpdate(Budget.BudgetID, Transaction.TransactionID);
                            _db.SaveChanges();

                        }
                        else if(OldTransactionDetails.isSpendFromSavings && Transaction.isSpendFromSavings && OldTransactionDetails.SavingID != Transaction.SavingID)
                        {
                            await _pt.UnTransact(OldTransactionDetails, Budget.BudgetID);
                            _db.Update(Transaction);
                            _db.SaveChanges();

                            _pt.TransactUpdate(Transaction, Budget.BudgetID);

                            await _pt.RecalculateAfterTransactionUpdate(Budget.BudgetID, Transaction.TransactionID);
                            _db.SaveChanges();
                            
                        }
                        else if (OldTransactoinAmount == Transaction.TransactionAmount)
                        {
                            Transaction.isTransacted = true;

                            _db.Update(Transaction);
                            _db.SaveChanges();
                        }
                        else
                        {
                            await _pt.UnTransact(OldTransactionDetails, Budget.BudgetID);
                            _db.Update(Transaction);
                            _db.SaveChanges();

                            string status = "OK";
                            if (Transaction.isSpendFromSavings)
                            {
                                status = _pt.TransactSavingsTransaction(ref Transaction, Budget.BudgetID);
                            }
                            else
                            {
                                status = _pt.TransactTransaction(ref Transaction, Budget.BudgetID);
                            }

                            status = await _pt.RecalculateAfterTransactionUpdate(Budget.BudgetID, Transaction.TransactionID);
                            _db.SaveChanges();
                        }
                    }
                    else
                    {
                        _db.Update(Transaction);
                        _db.SaveChanges();
                    }

                    return Ok();
                }
                catch (Exception ex)
                {
                    return BadRequest(new { ErrorMessage = ex.Message });
                }

            }
        }

        [HttpGet("deletetransaction/{TransactionID}")]
        public async Task<IActionResult> DeleteTransaction([FromRoute] int TransactionID)
        {
            try
            {
                if (TransactionID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Transaction ID can not be zero" });
                }

                Transactions? Transaction = _db.Transactions
                    .Where(b => b.TransactionID == TransactionID).First();

                if (Transaction == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }
                
                if(Transaction.isTransacted)
                {
                    Budgets? Budget = _db.Budgets.Where(b => b.Transactions.Contains(Transaction)).First();
                    _pt.UnTransact(Transaction, Budget.BudgetID);

                    _db.Remove(Transaction);
                    _db.SaveChanges();

                    string status = await _pt.RecalculateAfterTransactionUpdate(Budget.BudgetID, 0);
                }
                else
                {
                    _db.Remove(Transaction);
                    _db.SaveChanges();
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getrecenttransactions/{BudgetID}/{NumberOf}")]
        public async Task<IActionResult> GetRecentTransactions([FromRoute] int BudgetID, [FromRoute] int NumberOf)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b=>b.Transactions.Where(t=>t.isTransacted).OrderByDescending(t => t.TransactionDate).Take(NumberOf))
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                return Ok(Budget.Transactions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getcurrentpayperiodtransactions/{BudgetID}")]
        public async Task<IActionResult> GetCurrentPayPeriodTransactions([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets.Include(x => x.PayPeriodStats.Where(p => p.isCurrentPeriod))
                .Where(b => b.BudgetID == BudgetID)
                .FirstOrDefault();

                DateTime PeriodStart = Budget.PayPeriodStats[0].StartDate;

                Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b=>b.Transactions.Where(t=>t.TransactionDate > PeriodStart.Date).OrderByDescending(t => t.TransactionDate))
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                return Ok(Budget.Transactions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getrecenttransactionsoffset/{BudgetID}/{NumberOf}/{Offset}")]
        public async Task<IActionResult> GetRecentTransactionsOffset([FromRoute] int BudgetID, [FromRoute] int NumberOf, [FromRoute] int Offset)
        {
            try
            {
                int SkipNumber = Offset;

                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.Transactions.OrderByDescending(t => t.TransactionDate).Skip(SkipNumber).Take(NumberOf))
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                return Ok(Budget.Transactions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getbudgeteventtypes/{BudgetID}")]
        public async Task<IActionResult> GetBudgetEventTypes([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.Transactions)
                    .FirstOrDefault();

                List<string> EventTypes = new List<string>();

                EventTypes = Budget.Transactions.Where(t => t.EventType != null).OrderBy(t => t.EventType).Select(t => t.EventType).Distinct().ToList();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                return Ok(EventTypes);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpPost("getfilteredtransactions/{BudgetID}")]
        public async Task<IActionResult> GetFilteredTransactions([FromRoute] int BudgetID, [FromBody] FilterModel Filters)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
                }

                List<Transactions> FilteredTransactions = new List<Transactions>();

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.Transactions)
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                if (Filters.TransactionEventTypeFilter != null )
                {
                    FilteredTransactions.AddRange(Budget.Transactions.Where(t => Filters.TransactionEventTypeFilter.Contains(t.EventType)).ToList());
                    Budget.Transactions.RemoveAll(t => Filters.TransactionEventTypeFilter.Contains(t.EventType));
                }

                if (Filters.PayeeFilter != null)
                {
                    FilteredTransactions.AddRange(Budget.Transactions.Where(t => Filters.PayeeFilter.Contains(t.Payee)).ToList());
                    Budget.Transactions.RemoveAll(t => Filters.PayeeFilter.Contains(t.Payee));
                }

                if (Filters.CategoryFilter != null)
                {
                    FilteredTransactions.AddRange(Budget.Transactions.Where(t => Filters.CategoryFilter.Contains(t.CategoryID.GetValueOrDefault())).ToList());
                    Budget.Transactions.RemoveAll(t => Filters.CategoryFilter.Contains(t.CategoryID.GetValueOrDefault()));
                }

                if (Filters.SavingFilter != null)
                {
                    FilteredTransactions.AddRange(Budget.Transactions.Where(t => Filters.SavingFilter.Contains(t.SavingID.GetValueOrDefault())).ToList());
                    Budget.Transactions.RemoveAll(t => Filters.SavingFilter.Contains(t.SavingID.GetValueOrDefault()));
                }

                if(Filters.DateFilter != null)
                {
                    if(Filters.DateFilter.DateTo != null && Filters.DateFilter.DateFrom != null)
                    {
                        FilteredTransactions.RemoveAll(t => t.TransactionDate < Filters.DateFilter.DateFrom || t.TransactionDate > Filters.DateFilter.DateTo);
                    }
                    else if(Filters.DateFilter.DateTo != null)
                    {
                        FilteredTransactions.RemoveAll(t => t.TransactionDate > Filters.DateFilter.DateTo);
                    }
                    else if(Filters.DateFilter.DateFrom != null)
                    {
                        FilteredTransactions.RemoveAll(t => t.TransactionDate < Filters.DateFilter.DateFrom);
                    }
                }

                return Ok(FilteredTransactions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }
    }
}