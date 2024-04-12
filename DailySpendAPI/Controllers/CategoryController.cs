using AutoMapper;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/categories")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public CategoryController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpGet("getcategories/{BudgetId}")]
        public IActionResult GetCategories([FromRoute] int BudgetId)
        {
            try
            {
                Budgets? Budget = _db.Budgets?
                .Include(x => x.Categories)
                .Where(x => x.BudgetID == BudgetId)
                .FirstOrDefault();

                List<Categories>? categories = Budget.Categories.ToList();

                if (Budget != null)
                {
                    return Ok(categories);
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

        [HttpGet("getcategoryfromid/{CategoryID}")]
        public IActionResult GetCategoryFromID([FromRoute] int CategoryID)
        {
            try
            {
                if(CategoryID == 0)
                {
                    return BadRequest(new { ErrorMessage = "CategoryID can not be zero" });
                }

                Categories? Cat = _db.Categories?
                .Where(x => x.CategoryID == CategoryID)
                .FirstOrDefault();

                if (Cat != null)
                {
                    return Ok(Cat);
                }
                else
                {
                    return NotFound(new { ErrorMessage = "Category not found" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpPost("addnewsubcategory/{BudgetID}")]
        public async Task<IActionResult> AddNewSubCategory([FromRoute] int BudgetID, [FromBody] Categories Category)
        {
            if (BudgetID == 0)
            {
                return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
            }
            else
            {
                Budgets? Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID).First();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = $"Budget with ID {BudgetID} not found" });
                }
                else
                {
                    try
                    {
                        _db.Attach(Budget);
                        Budget.Categories.Add(Category);
                        _db.SaveChanges();

                        if (Category.CategoryID == 0)
                        {
                            return BadRequest(new { ErrorMessage = "Category did not save correctly" });
                        }
                        else
                        {
                            return Ok(Category);
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { ErrorMessage = ex.Message });
                    }

                }
            }
        }

        [HttpPost("addnewcategory/{BudgetID}")]
        public async Task<IActionResult> AddNewCategory([FromRoute] int BudgetID, [FromBody] DefaultCategories Category)
        {
            if (BudgetID == 0)
            {
                return BadRequest(new { ErrorMessage = "Budget ID can not be zero" });
            }
            else
            {
                Budgets? Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID).First();

                if (Budget == null)
                {
                    return NotFound(new { ErrorMessage = $"Budget with ID {BudgetID} not found" });
                }
                else
                {
                    try
                    {
                        int CategoryID = _pt.CreateCategory(BudgetID, Category);

                        if (CategoryID == 0)
                        {
                            return BadRequest(new { ErrorMessage = "Category did not save correctly" });
                        }
                        else
                        {
                            return Ok(CategoryID);
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { ErrorMessage = ex.Message });
                    }

                }
            }
        }

        [HttpPatch("patchcategory/{CategoryID}")]
        public async Task<IActionResult> PatchBill([FromRoute] int CategoryID, JsonPatchDocument<Categories> PatchDoc)
        {
            try
            {
                if (CategoryID == 0)
                {
                    return BadRequest(new { ErrorMessage = "CategoryID ID can not be zero" });
                }

                Categories? Cat = _db.Categories
                    .Where(b => b.CategoryID == CategoryID).First();

                if (Cat == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                PatchDoc.ApplyTo(Cat, ModelState);

                if (!TryValidateModel(Cat))
                {
                    return ValidationProblem(ModelState);
                }

                _db.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("Updatealltransactionscategoryname/{CategoryID}")]
        public async Task<IActionResult> UpdateAllTransactionsCategoryName([FromRoute] int CategoryID)
        {
            try
            {
                if (CategoryID == 0)
                {
                    return BadRequest(new { ErrorMessage = "CategoryID ID can not be zero" });
                }

                Categories? Cat = _db.Categories
                    .Where(b => b.CategoryID == CategoryID).First();

                if (Cat == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                _db.Transactions.Where(t =>t.CategoryID == Cat.CategoryID).ExecuteUpdate(t => t.SetProperty(c => c.Category, Cat.CategoryName));

                return Ok();

            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("getallheadercategorydetailsfull/{BudgetId}")]
        public IActionResult GetAllHeaderCategoryDetailsFull([FromRoute] int BudgetId)
        {
            try
            {
                Budgets? Budget = _db.Budgets?
                .Include(x => x.Categories.Where(c => !c.isSubCategory))
                .Include(x => x.PayPeriodStats.OrderByDescending(p => p.PayPeriodID).Where(p => p.EndDate >= DateTime.UtcNow.AddYears(-1)))
                .Where(x => x.BudgetID == BudgetId)
                .FirstOrDefault();

                List<Categories>? categories = Budget.Categories.ToList();
                DateTime PayPeriodStartDate = Budget.PayPeriodStats.Where(p => p.isCurrentPeriod).Select(p => p.StartDate).FirstOrDefault();

                foreach(var category in categories)
                {
                    List<int> SubCategories = _db.Categories.Where(c => c.CategoryGroupID == category.CategoryID && c.isSubCategory).Select(c => c.CategoryID).ToList();
                    
                    var TotalAmount = _db.Transactions.Where(t => t.isTransacted && SubCategories.Contains(t.CategoryID.GetValueOrDefault())).Sum(t => t.TransactionAmount);
                    category.CategorySpendAllTime = TotalAmount.GetValueOrDefault();

                    var PayPeriodTotalAmount = _db.Transactions.Where(t => t.isTransacted && t.TransactionDate > PayPeriodStartDate && SubCategories.Contains(t.CategoryID.GetValueOrDefault())).Sum(t => t.TransactionAmount);
                    category.CategorySpendPayPeriod = PayPeriodTotalAmount.GetValueOrDefault();   
                    
                    foreach(var PayPeriod in Budget.PayPeriodStats)
                    {
                        DateTime FromDate = PayPeriod.StartDate;
                        DateTime ToDate = PayPeriod.EndDate;

                        decimal SpendTotalAmount = _db.Transactions.Where(t => t.isTransacted && t.TransactionDate > FromDate && t.TransactionDate <= ToDate && SubCategories.Contains(t.CategoryID.GetValueOrDefault())).Sum(t => t.TransactionAmount).GetValueOrDefault();

                        SpendPeriods spendPeriods = new SpendPeriods
                        {
                            FromDate = FromDate,
                            ToDate = ToDate,
                            SpendTotalAmount = SpendTotalAmount,
                            IsCurrentPeriod = PayPeriod.isCurrentPeriod
                        };

                        category.CategorySpendPeriods.Add(spendPeriods);
                    }

                }

                if (Budget != null)
                {
                    return Ok(categories);
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

        [HttpGet("getheadercategorydetailsfull/{CategoryID}/{BudgetID}")]
        public IActionResult GetHeaderCategoryDetailsFull([FromRoute] int CategoryID, [FromRoute] int BudgetID)
        {
            try
            {
                Budgets? Budget = _db.Budgets?
                    .Include(x => x.Categories.Where(c => c.CategoryGroupID == CategoryID))
                    .Include(x => x.PayPeriodStats.OrderByDescending(p => p.PayPeriodID).Where(p => p.EndDate >= DateTime.UtcNow.AddYears(-1)))
                    .Where(x => x.BudgetID == BudgetID)
                    .FirstOrDefault();

                List<Categories>? categories = Budget.Categories.ToList();
                DateTime PayPeriodStartDate = Budget.PayPeriodStats.Where(p => p.isCurrentPeriod).Select(p => p.StartDate).FirstOrDefault();

                foreach (var category in categories)
                {
                    var TotalAmount = _db.Transactions.Where(t => t.isTransacted && t.CategoryID == category.CategoryID).Sum(t => t.TransactionAmount);
                    category.CategorySpendAllTime = TotalAmount.GetValueOrDefault();

                    var PayPeriodTotalAmount = _db.Transactions.Where(t => t.isTransacted && t.TransactionDate > PayPeriodStartDate && t.CategoryID == category.CategoryID).Sum(t => t.TransactionAmount);
                    category.CategorySpendPayPeriod = PayPeriodTotalAmount.GetValueOrDefault();

                    foreach (var PayPeriod in Budget.PayPeriodStats)
                    {
                        DateTime FromDate = PayPeriod.StartDate;
                        DateTime ToDate = PayPeriod.EndDate;

                        decimal SpendTotalAmount = _db.Transactions.Where(t => t.isTransacted && t.TransactionDate > FromDate && t.TransactionDate <= ToDate && t.CategoryID == category.CategoryID).Sum(t => t.TransactionAmount).GetValueOrDefault();

                        SpendPeriods spendPeriods = new SpendPeriods
                        {
                            FromDate = FromDate,
                            ToDate = ToDate,
                            SpendTotalAmount = SpendTotalAmount,
                            IsCurrentPeriod = PayPeriod.isCurrentPeriod
                        };

                        category.CategorySpendPeriods.Add(spendPeriods);
                    }
                }

                if (Budget != null)
                {
                    return Ok(categories);
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

        [HttpGet("deletecategory/{CategoryID}/{IsReassign}/{ReAssignID}")]
        public IActionResult GetHeaderCategoryDetailsFull([FromRoute] int CategoryID, [FromRoute] bool IsReassign, [FromRoute] int ReAssignID)
        {
            try
            {
                if (CategoryID == 0)
                {
                    return BadRequest(new { ErrorMessage = "CategoryID ID can not be zero" });
                }


                if(IsReassign)
                {
                    if(ReAssignID == 0)
                    {
                        return BadRequest(new { ErrorMessage = "ReAssign CategoryID ID can not be zero" });
                    }

                    string? CategoryName = _db.Categories.Where(c => c.CategoryID == ReAssignID).Select(c => c.CategoryName).FirstOrDefault();

                    _db.Transactions.Where(t => t.CategoryID == CategoryID).ExecuteUpdate(t => t.SetProperty(c => c.Category, CategoryName).SetProperty(c => c.CategoryID, ReAssignID));

                }

                _db.Categories.Where(c => c.CategoryID == CategoryID).ExecuteDelete();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getallcategorynames/{BudgetID}")]
        public IActionResult GetAllCategoryNames([FromRoute] int BudgetID)
        {
            try
            {
                if (BudgetID == 0)
                {
                    return BadRequest(new { ErrorMessage = "BudgetID ID can not be zero" });
                }

                Dictionary<string, int>? Categories = new Dictionary<string, int>();

                var Budget = _db.Budgets.Where(b => b.BudgetID == BudgetID).Include(b => b.Categories).FirstOrDefault();

                foreach(var cat in Budget.Categories)
                {                    
                    if(cat.isSubCategory)
                    {
                        string GroupCatName = _db.Categories.Where(c => c.CategoryID == cat.CategoryGroupID).Select(c => c.CategoryName).FirstOrDefault();
                        Categories.Add($"{GroupCatName} | {cat.CategoryName}", cat.CategoryID);
                    }                    
                }

                return Ok(Categories);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }
    }
}
