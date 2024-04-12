using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using DailyBudgetAPI.DTOS;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.JsonPatch;
using AutoMapper;
using sib_api_v3_sdk.Model;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/userAccounts")]
    public class AccountsController : ControllerBase
    {

        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        private readonly IFIleStorageService _fs;

        public AccountsController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt, IFIleStorageService fs)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
            _fs = fs;
        }

        [HttpGet("{UserId}")]
        public IActionResult GetUserDetails(int UserId)
        {
            UserAccounts? User = _db.UserAccounts.Include(x => x.Budgets).Where(x => x.UserID == UserId).FirstOrDefault();

            if(User == null) 
            {
                return NotFound(new { ErrorMessage = "User Not found" });
            }

            return Ok(User);
        }

        [HttpPatch("{UserId}")]
        public IActionResult UpdateUserDetails(int UserId, JsonPatchDocument<UserAccounts> jsonPatchDocument)
        {

            UserAccounts? User = _db.UserAccounts.Where(x => x.UserID == UserId).FirstOrDefault();

            if (User == null)
            {
                return NotFound(new { ErrorMessage = "User Not found" });
            }

            jsonPatchDocument.ApplyTo(User, ModelState);

            if(!TryValidateModel(User))
            {
                return ValidationProblem(ModelState);
            }

            _db.Update(User);
            _db.SaveChanges();

            return Ok();
        }

        [HttpPost("registeruser")]
        public IActionResult RegisterUser([FromBody] UserAccountsDTO User)
        {

            if (User == null)
            {
                return BadRequest(new { ErrorMessage = "Object is Null" });
            }
            else
            {
                if (User.Email != null)
                {
                    Regex regex = new Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$");
                    Match match = regex.Match(User.Email);

                    if (!match.Success)
                    {
                        return BadRequest(new { ErrorMessage = "Invalid Email" });
                    }

                    if (!_sh.CheckUniqueEmail(User.Email))
                    {
                        return BadRequest(new { ErrorMessage = "Email already in use" });
                    }
                }

                User.DefaultBudgetID = 0;
                User.SubscriptionExpiry = DateTime.UtcNow.AddYears(1);

                UserAccounts UserAccount = _am.Map<UserAccounts>(User);

                UserAccount.LastLoggedOn = DateTime.UtcNow;
                UserAccount.AccountCreated = DateTime.UtcNow;
                UserAccount.SubscriptionExpiry = User.SubscriptionExpiry.GetValueOrDefault();
                UserAccount.SubscriptionType = "PermiumPlus";

                Budgets DefaultBudget = new Budgets();
                DefaultBudget.IsCreated = false;
                DefaultBudget.BudgetType = "PermiumPlus";
                _db.Budgets.Add(DefaultBudget);
                _db.SaveChanges();

                BudgetSettings budgetSettings = new BudgetSettings();
                budgetSettings.BudgetID = DefaultBudget.BudgetID;

                _db.BudgetSettings.Add(budgetSettings);
                _db.SaveChanges();

                UserAccount.DefaultBudgetID = DefaultBudget.BudgetID;
                UserAccount.Budgets.Add(DefaultBudget);

                _db.UserAccounts.Add(UserAccount);
                _db.SaveChanges();

                _pt.CreateDefaultCategories(DefaultBudget.BudgetID);
                _pt.CreateNewPayPeriodStats(DefaultBudget.BudgetID);

                User.UserID = UserAccount.UserID;
                User.DefaultBudgetID = DefaultBudget.BudgetID;
                DefaultBudget.BudgetCreatedOn = DateTime.UtcNow;
                DefaultBudget.LastUpdated = DateTime.UtcNow;
                DefaultBudget.BudgetType = "Basic";
                _db.SaveChanges();

                return Ok(User);

            }
            
        }

        [HttpGet("getLogonDetails/{Email}")]
        public IActionResult GetLogonDetails(string Email)
        {
            var UserAccounts = _db.UserAccounts
                .Where(x => x.Email == Email);

            var UserDetails = UserAccounts.FirstOrDefault();

            UserAccountsDTO ReturnObject = new UserAccountsDTO();

            if (UserDetails == null)
            {
                return BadRequest(new { ErrorMessage = "User not found" });
            }
            else
            {
                ReturnObject.Email = UserDetails.Email;
                ReturnObject.isEmailVerified = UserDetails.isEmailVerified;
                ReturnObject.NickName = UserDetails.NickName;
                ReturnObject.Password = UserDetails.Password;
                ReturnObject.DefaultBudgetID = UserDetails.DefaultBudgetID ?? 0;
                ReturnObject.UserID = UserDetails.UserID;
                ReturnObject.SubscriptionType = UserDetails.SubscriptionType;
                ReturnObject.SubscriptionExpiry = UserDetails.SubscriptionExpiry;
                ReturnObject.ProfilePicture = UserDetails.ProfilePicture;

                Budgets? Budget = _db.Budgets.Where(b=> b.BudgetID == UserDetails.DefaultBudgetID.GetValueOrDefault()).FirstOrDefault();

                if(Budget != null)
                {
                    ReturnObject.DefaultBudgetType = Budget.BudgetType;
                }               

                return Ok(ReturnObject);
            }            
        }

        [HttpGet("getsalt/{Email}")]
        public IActionResult GetUserSalt(string Email)
        {
            var UserAccounts = _db.UserAccounts
                .Where(x => x.Email == Email);

            var UserDetails = UserAccounts.FirstOrDefault();

            if (UserDetails == null)
            {
                return BadRequest(new { ErrorMessage = "User not found" });
            }
            else
            {
                return Ok(new { salt = UserDetails.Salt });
            }            

        }

        [HttpGet("getuseraccountbudgets/{UserID}")]
        public IActionResult GetUserAccountBudgets(int UserID)
        {
            UserAccounts? User = _db.UserAccounts.Where(u=>u.UserID == UserID).Include(u=>u.Budgets).FirstOrDefault();

            if (User == null)
            {
                return NotFound(new { ErrorMessage = "Email Did not send" });
            }

            List<Budgets> Budgets = User.Budgets;

            List<Budgets> SharedBudget = _db.Budgets.Where(b => b.SharedUserID == UserID).ToList();

            if(SharedBudget != null)
            {
                Budgets.AddRange(SharedBudget);
            }

            return Ok(Budgets);
        }

        [HttpPost("uploaduserprofilepicture/{UserID}")]
        public async Task<IActionResult> UploadUserProfilePicture([FromRoute]int UserID)
        {
            UserAccounts? User = _db.UserAccounts.Where(u => u.UserID == UserID).FirstOrDefault();

            if (User == null)
            {
                return NotFound(new { ErrorMessage = "No Such User" });
            }

            if(HttpContext is null)
            {
                return BadRequest(new { ErrorMessage = "No File provided" });
            }

            if(HttpContext.Request.Form.Files.Count > 0)
            {
                foreach(var file in HttpContext.Request.Form.Files)
                {
                    string result = await _fs.Upload(file.OpenReadStream(), file.FileName, "dbugetprofileimage", UserID);
                    if(result == "UploadFailed")
                    {
                        return BadRequest(new { ErrorMessage = "File Failed to Upload" });
                    }
                    await _fs.SaveFileLocation(result, UserID, "ProfilePictureImage", "dbugetprofileimage");
                }
            }
            else
            {
                return BadRequest(new { ErrorMessage = "No File provided" });    
            }

            return Ok();
        }

        [HttpGet("downloaduserprofilepicture/{UserID}")]
        public async Task<IActionResult> DownloadUserProfilePicture([FromRoute] int UserID)
        {
            string FileLocation = await _fs.GetFileLocation(UserID, "ProfilePictureImage");
            if (string.IsNullOrEmpty(FileLocation))
            {
                return NotFound();
            }
            string MediaType = $"image/{Path.GetExtension(FileLocation).ToLower()}";

            Stream stream = await _fs.Download(FileLocation, "dbugetprofileimage", UserID);
            if (stream is null)
            {
                return NotFound();
            }            

            return File(stream, MediaType, System.IO.Path.GetFileName(FileLocation));

        }
    }
}
