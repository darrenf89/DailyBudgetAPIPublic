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
    [Route("api/v1/bills")]
    public class BillsController: ControllerBase
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public BillsController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpGet("getbillfromid/{BillID}")]
        public IActionResult GetBillFromID([FromRoute] int BillID)
        {
            if(BillID == 0)
            {
                return BadRequest(new { ErrorMessage = "Bill ID can not be zero"});
            }
            else
            {
                Bills? Bill = _db.Bills.Where(b => b.BillID == BillID).First();

                if(Bill == null)
                {
                    return NotFound(new { ErrorMessage = $"No bill with Bill ID {BillID} found"});
                }
                else
                {
                    return Ok(Bill);
                }
            }

        }

        [HttpPost("savenewbill/{BudgetID}")]
        public async Task<IActionResult> SaveNewBill([FromRoute] int BudgetID, [FromBody] Bills Bill)
        {
            if(BudgetID == 0)
            {
                return BadRequest(new { ErrorMessage = "Budget ID can not be zero"});
            }
            else
            {
                Budgets? Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID).Include(b => b.Bills).First();

                if(Budget == null)
                {
                    return NotFound(new { ErrorMessage = $"Budget with ID {BudgetID} not found"});
                }
                else
                {
                    try
                    {
                        _db.Attach(Budget);
                        Budget.Bills.Add(Bill);
                        Budget.LastUpdated = DateTime.UtcNow;
                        _db.SaveChanges();

                        if(Bill.BillID == 0)
                        {
                            return BadRequest(new { ErrorMessage = "Bill did not save correctly"});
                        }
                        else
                        {
                            string status = await _pt.UpdateBudgetAsync(BudgetID);
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

        [HttpPost("updatebill")]
        public async Task<IActionResult> UpdateBill([FromBody] Bills Bill)
        {
            if(Bill.BillID == 0)
            {
                return BadRequest(new { ErrorMessage = "Bill does not already exsist" });
            }
            else
            {
                try
                {
                    _db.Attach(Bill);
                    _db.Update(Bill);
                    _db.SaveChanges();

                    Budgets? Budget = _db.Budgets.Where(b => b.Bills.Contains(Bill)).First();
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

        [HttpPatch("patchbill/{BillID}")]
        public async Task<IActionResult> PatchBill([FromRoute] int BillID, JsonPatchDocument<Bills> PatchDoc)
        {
            try
            {
                if(BillID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Bill ID can not be zero"});
                }

                Bills? Bill = _db.Bills
                    .Where(b => b.BillID == BillID).First();

                if (Bill == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                PatchDoc.ApplyTo(Bill, ModelState);

                if (!TryValidateModel(Bill))
                {
                    return ValidationProblem(ModelState);
                }

                _db.SaveChanges();

                Budgets? Budget = _db.Budgets.Where(b => b.Bills.Contains(Bill)).First();
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

        [HttpGet("deletebill/{BillID}")]
        public async Task<IActionResult> DeleteBill([FromRoute] int BillID)
        {
            try
            {
                if (BillID == 0)
                {
                    return BadRequest(new { ErrorMessage = "Bill ID can not be zero" });
                }

                Bills? Bill = _db.Bills
                    .Where(b => b.BillID == BillID).First();

                if (Bill == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                _db.Attach(Bill);

                Budgets? Budget = _db.Budgets.Where(b => b.Bills.Contains(Bill)).First();                

                _db.Remove(Bill);
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

        [HttpGet("getbudgetbills/{BudgetID}")]
        public async Task<IActionResult> GetBudgetBills([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID ID can not be zero" });
                }

                Budgets? Budget = _db.Budgets
                    .Where(b => b.BudgetID == BudgetID)
                    .Include(b => b.Bills.Where(b => !b.isClosed))
                    .FirstOrDefault();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                List<Bills> Bills = new List<Bills>();

                if (Budget.Savings != null)
                {
                    Bills = Budget.Bills;
                }

                return Ok(Bills);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }
    }
}