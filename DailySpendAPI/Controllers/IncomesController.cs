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
    [Route("api/v1/incomes")]
    public class IncomesController: ControllerBase
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public IncomesController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpGet("getincomefromid/{IncomeID}")]
        public IActionResult GetIncomeFromID([FromRoute] int IncomeID)
        {
            if(IncomeID == 0)
            {
                return BadRequest(new { ErrorMessage = "Income ID can not be zero"});
            }
            else
            {
                IncomeEvents? Income = _db.IncomeEvents.Where(b => b.IncomeEventID == IncomeID).First();

                if(Income == null)
                {
                    return NotFound(new { ErrorMessage = $"No bill with Bill ID {IncomeID} found"});
                }
                else
                {
                    return Ok(Income);
                }
            }

        }

        [HttpPost("savenewincome/{BudgetID}")]
        public async Task<IActionResult> SaveNewIncome([FromRoute] int BudgetID, [FromBody] IncomeEvents Income)
        {
            if(BudgetID == 0)
            {
                return BadRequest(new { ErrorMessage = "Budget ID can not be zero"});
            }
            else
            {
                Budgets? Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID).Include(i => i.IncomeEvents).First();

                if(Budget == null)
                {
                    return NotFound(new { ErrorMessage = $"Budget with ID {BudgetID} not found"});
                }
                else
                {
                    try
                    {
                        _db.Attach(Budget);
                        Budget.IncomeEvents.Add(Income);
                        _db.SaveChanges();
                        

                        if(Income.IncomeEventID == 0)
                        {
                            return BadRequest(new { ErrorMessage = "Income did not save correctly"});
                        }
                        else
                        {
                            string status = await _pt.UpdateBudgetAsync(BudgetID);
                            Budget.LastUpdated = DateTime.UtcNow;
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

        [HttpPost("updateincome")]
        public async Task<IActionResult> UpdateIncome([FromBody] IncomeEvents Income)
        {
            if(Income.IncomeEventID == 0)
            {
                return BadRequest(new { ErrorMessage = "Income does not already exsist" });
            }
            else
            {
                try
                {
                    _db.Attach(Income);
                    _db.Update(Income);
                    _db.SaveChanges();

                    Budgets? Budget = _db.Budgets.Where(b => b.IncomeEvents.Contains(Income)).First();
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
        }

        [HttpPatch("patchincome/{IncomeID}")]
        public async Task<IActionResult> PatchIncome([FromRoute] int IncomeID, JsonPatchDocument<IncomeEvents> PatchDoc)
        {
            try
            {
                if(IncomeID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Income ID can not be zero"});
                }

                IncomeEvents? Income = _db.IncomeEvents
                    .Where(b => b.IncomeEventID == IncomeID).First();

                if (Income == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                PatchDoc.ApplyTo(Income, ModelState);

                if (!TryValidateModel(Income))
                {
                    return ValidationProblem(ModelState);
                }

                Budgets? Budget = _db.Budgets.Where(b => b.IncomeEvents.Contains(Income)).First();
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

        [HttpGet("deleteincome/{IncomeID}")]
        public async Task<IActionResult> DeleteIncomeEvent([FromRoute] int IncomeID)
        {
            try
            {
                if (IncomeID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Income ID can not be zero" });
                }

                IncomeEvents? Income = _db.IncomeEvents
                    .Where(b => b.IncomeEventID == IncomeID).First();

                if (Income == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                _db.Attach(Income);

                Budgets? Budget = _db.Budgets.Where(b => b.IncomeEvents.Contains(Income)).First();

                _db.Remove(Income);
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

        [HttpGet("getbudgetincomeevents/{BudgetID}")]
        public async Task<IActionResult> GetBudgetIncomeEvents([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.IncomeEvents.Where(b => !b.isClosed))
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                List<IncomeEvents> IncomeEvents = new List<IncomeEvents>();

                if (Budget.Savings != null)
                {
                    IncomeEvents = Budget.IncomeEvents;
                }

                return Ok(IncomeEvents);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

    }
}