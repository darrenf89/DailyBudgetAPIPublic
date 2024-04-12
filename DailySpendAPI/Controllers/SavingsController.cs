using Microsoft.AspNetCore.Mvc;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.JsonPatch;
using AutoMapper;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/savings")]
    public class SavingsController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public SavingsController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpGet("getsavingfromid/{SavingID}")]
        public IActionResult GetSavingFromID([FromRoute] int SavingID)
        {
            if(SavingID == 0)
            {
                return BadRequest(new { ErrorMessage = "Saving ID can not be zero"});
            }
            else
            {
                Savings? Saving = _db.Savings.Where(b => b.SavingID == SavingID).First();

                if(Saving == null)
                {
                    return NotFound(new { ErrorMessage = $"No bill with Bill ID {SavingID} found"});
                }
                else
                {
                    return Ok(Saving);
                }
            }

        }

        [HttpPost("savenewsaving/{BudgetID}")]
        public async Task<IActionResult> SaveNewSaving([FromRoute] int BudgetID, [FromBody] Savings Saving)
        {
            if(BudgetID == 0)
            {
                return BadRequest(new { ErrorMessage = "Budget ID can not be zero"});
            }
            else
            {
                Budgets? Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID).Include(s => s.Savings).First();

                if(Budget == null)
                {
                    return NotFound(new { ErrorMessage = $"Budget with ID {BudgetID} not found"});
                }
                else
                {
                    try
                    {
                        _db.Attach(Budget);
                        Budget.Savings.Add(Saving);
                        _db.SaveChanges();

                        if(Saving.SavingID == 0)
                        {
                            return BadRequest(new { ErrorMessage = "Saving did not save correctly"});
                        }
                        else
                        {
                            string status = await _pt.UpdateBudgetAsync(BudgetID);

                            Budget.LastUpdated = DateTime.UtcNow;
                            _db.SaveChanges();
                            return Ok(Saving.SavingID);
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { ErrorMessage = ex.Message });
                    }

                }
            }
        }

        [HttpPost("updatesaving")]
        public async Task<IActionResult> UpdateSaving([FromBody] Savings Saving)
        {
            if(Saving.SavingID == 0)
            {
                return BadRequest(new { ErrorMessage = "Saving does not already exsist" });
            }
            else
            {
                try
                {
                    _db.Update(Saving);
                    _db.SaveChanges();

                    var ClearSaving = _db.Savings.Find(Saving.SavingID);
                    _db.Entry(ClearSaving).State = EntityState.Detached;
                    ClearSaving = _db.Savings.Find(Saving.SavingID);

                    int BudgetID = _db.Budgets.Where(b => b.Savings.Contains(Saving)).Select(b => b.BudgetID).FirstOrDefault();
                    string status = await _pt.UpdateBudgetAsync(BudgetID);

                    return Ok();
                }
                catch (Exception ex)
                {
                    return BadRequest(new { ErrorMessage = ex.Message });
                }

            }
        }

        [HttpPatch("patchsaving/{SavingID}")]
        public async Task<IActionResult> PatchSaving([FromRoute] int SavingID, JsonPatchDocument<Savings> PatchDoc)
        {
            try
            {
                if(SavingID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Saving ID can not be zero"});
                }

                Savings? Saving = _db.Savings
                    .Where(s => s.SavingID == SavingID).First();

                if (Saving == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                PatchDoc.ApplyTo(Saving, ModelState);

                if (!TryValidateModel(Saving))
                {
                    return ValidationProblem(ModelState);
                }

                Budgets? Budget = _db.Budgets.Where(b => b.Savings.Contains(Saving)).First();
                string status = await _pt.UpdateBudgetAsync(Budget.BudgetID);

                Budget.LastUpdated = DateTime.UtcNow;
                _db.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("deletesaving/{SavingID}")]
        public async Task<IActionResult> DeleteSaving([FromRoute] int SavingID)
        {
            try
            {
                if (SavingID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Bill ID can not be zero" });
                }

                Savings? Saving = _db.Savings
                    .Where(s => s.SavingID == SavingID).First();

                if (Saving == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                _db.Attach(Saving);

                Budgets? Budget = _db.Budgets.Where(b => b.Savings.Contains(Saving)).First();                

                List<Transactions> Transactions = new List<Transactions>();                            
                Transactions = _db.Transactions.Where(t => !t.isTransacted && t.SavingID == SavingID).ToList();
                
                if(Transactions.Count() != 0)
                {
                    _db.RemoveRange(Transactions);
                }

                _db.Remove(Saving);
                _db.SaveChanges();

                string status = await _pt.UpdateBudgetAsync(Budget.BudgetID);

                Budget.LastUpdated = DateTime.UtcNow;
                _db.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("getbudgetregularsaving/{BudgetID}")]
        public async Task<IActionResult> GetBudgetRegularSaving([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.Savings.Where(s => s.isRegularSaving && !(s.isSavingsClosed && s.CurrentBalance == 0)))
                    .First();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                List<Savings> Savings = new List<Savings>();

                if (Budget.Savings != null)
                {
                    Savings = Budget.Savings;
                }

                return Ok(Savings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("getbudgetenvelopesaving/{BudgetID}")]
        public async Task<IActionResult> GetBudgetEnvelopeSaving([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.Savings.Where(s => !s.isRegularSaving && !s.isSavingsClosed))
                    .First();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                List<Savings> Savings = new List<Savings>();

                if(Budget.Savings != null)
                {
                    Savings = Budget.Savings;
                }

                return Ok(Savings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("getallbudgetsavings/{BudgetID}")]
        public async Task<IActionResult> GetAllBudgetSavings([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.Savings)
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                List<Savings> Savings = new List<Savings>();

                if (Budget.Savings != null)
                {
                    Savings = Budget.Savings;
                }

                return Ok(Savings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

    }
}