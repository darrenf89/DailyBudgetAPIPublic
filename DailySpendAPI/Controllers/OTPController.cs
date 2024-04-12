using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;

using FirebaseAdmin.Messaging;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/otp")]
    public class OTPController : ControllerBase
    {

        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;
        private readonly IEmailService _es;

        public OTPController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt, IEmailService es)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
            _es = es;
        }

        [HttpGet("createnewotpcode/{UserID}/{OTPType}")]
        public IActionResult CreateNewOTPCode([FromRoute] int UserID, [FromRoute] string OTPType)
        {
            try 
            {
                if (UserID == 0)
                {
                    return BadRequest(new { ErrorMessage = "No userID provided" });
                }
                else
                {
                    //CHECK USER DOESNT HAVE MORE THAN 3 OTPs IN THE LAST 24 HOURS.
                    List<OTP> OTPList = _db.OTP.Where(o => o.UserAccountID == UserID && o.OTPExpiryTime > DateTime.UtcNow.AddHours(-24) && !o.IsValidated && o.OTPType == OTPType).ToList();

                    if(OTPList.Count <= 3)
                    {
                        OTP NewOTP = new OTP();
                        NewOTP.OTPCode = (BetterRandom.NextInt() % 100000000).ToString("00000000");
                        NewOTP.OTPExpiryTime = DateTime.UtcNow.AddMinutes(30);
                        NewOTP.UserAccountID = UserID;
                        NewOTP.IsValidated = false;
                        NewOTP.OTPType = OTPType;

                        _db.Add(NewOTP);
                        _db.SaveChanges();

                        
                        try
                        {
                            string status = "OK";
                            if(OTPType == "ValidateEmail")
                            {
                                status = _es.SendTransactionEmail(_es.CreateEmailVerificationEmail(UserID, NewOTP));
                            }
                            else if(OTPType == "ResetPassword")
                            {
                                status = _es.SendTransactionEmail(_es.CreateResetPasswordEmail(UserID, NewOTP));
                            }

                            if (status == "OK")
                            {
                                return Ok(NewOTP);
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
                    else
                    {
                        return NotFound();
                    }
                }
            }
            catch (Exception ex) 
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
            
        }

        [HttpPost("validateotpcodeemail")]
        public IActionResult ValidateOTPCodeEmail([FromBody] OTP UserOTP)
        {
            try
            {
                if (UserOTP == null || UserOTP.OTPCode == "")
                {
                    return BadRequest(new { ErrorMessage = "No OTP details provided" });
                }
                else
                {
                    OTP? CheckOTP = _db.OTP.Where(o => o.UserAccountID == UserOTP.UserAccountID && o.OTPType == UserOTP.OTPType).OrderByDescending(o => o.OTPID).FirstOrDefault();

                    if (CheckOTP == null)
                    {
                        return NotFound();
                    }
                    else
                    {
                        if(CheckOTP.OTPCode == UserOTP.OTPCode && CheckOTP.OTPExpiryTime >= UserOTP.OTPExpiryTime) 
                        {
                            CheckOTP.IsValidated = true;
                            _db.Update(CheckOTP);

                            UserAccounts User = _db.UserAccounts.Where(u => u.UserID == UserOTP.UserAccountID).First();
                            User.isEmailVerified = true;

                            _db.Update(User);                            
                            _db.SaveChanges();

                            string status = "";
                            status = _es.SendTransactionEmail(_es.CreateOTPVerifiedEmail(User.UserID, CheckOTP));

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
                            return NotFound();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpPost("validateotpcodesharebudget/{ShareBudgetRequestID}")]
        public IActionResult ValidateOTPCodeShareBudget([FromRoute] int ShareBudgetRequestID, [FromBody] OTP UserOTP)
        {
            try
            {
                if (UserOTP == null || UserOTP.OTPCode == "")
                {
                    return BadRequest(new { ErrorMessage = "No OTP details provided" });
                }
                else
                {
                    OTP? CheckOTP = _db.OTP.Where(o => o.UserAccountID == UserOTP.UserAccountID && o.OTPType == UserOTP.OTPType).OrderByDescending(o => o.OTPID).FirstOrDefault();

                    if (CheckOTP == null)
                    {
                        return NotFound();
                    }
                    else
                    {
                        if (CheckOTP.OTPCode == UserOTP.OTPCode && CheckOTP.OTPExpiryTime >= UserOTP.OTPExpiryTime)
                        {
                            CheckOTP.IsValidated = true;
                            _db.Update(CheckOTP);

                            Budgets? budget;
                            ShareBudgetRequest? ShareRequest = _db.ShareBudgetRequest.Where(u => u.SharedBudgetRequestID == ShareBudgetRequestID).FirstOrDefault();
                            if(ShareRequest != null)
                            {
                                ShareRequest.IsVerified = true;
                                budget = _db.Budgets?.Where(b => b.BudgetID == ShareRequest.SharedBudgetID).FirstOrDefault();
                                budget.IsSharedValidated = true;

                                _db.Update(budget);
                                _db.Update(ShareRequest);

                            }
                            else
                            {
                                return BadRequest(new { ErrorMessage = "No budget share request" });
                            }

                            _db.SaveChanges();

                            string status = "";
                            status = _es.SendTransactionEmail(_es.CreateOTPVerifiedEmailShareBudget(ShareRequest, CheckOTP));

                            //SEND PUSH NOTIFICATION BACK TO PERSON WHO SHARED
                            UserAccounts? User = _db.UserAccounts?.Where(u => u.Budgets.Contains(budget)).FirstOrDefault();
                            if (User != null)
                            {                                    
                                FirebaseDevices? Device = _db.FirebaseDevices.Where(u => u.UserAccountID == User.UserID && u.LoginExpiryDate >= DateTime.UtcNow).OrderByDescending(u => u.LoginExpiryDate).FirstOrDefault();

                                if (Device != null)
                                {
                                    Dictionary<string, string> AndroidData = new()
                                    {
                                        { "NavigationType", "BudgetShared" }
                                    };

                                    Message Message = new Message
                                    {
                                        Token = Device.FirebaseToken,
                                        Notification = new Notification
                                        {
                                            Title = "BUDGET SHARED!!!",
                                            Body = $"You are successfully budgetting with {ShareRequest.SharedWithUserEmail}, let's go!"
                                        },
                                        Data = AndroidData

                                    };

                                    _pt.SendPushNotification(Message);
                                }
                                
                            }

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
                            return NotFound();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getuseridfromemail/{UserEmail}")]
        public IActionResult GetUserIdFromEmail([FromRoute] string UserEmail)
        {
            try
            {
                if (UserEmail == "")
                {
                    return BadRequest(new { ErrorMessage = "No Email provided" });
                }
                else
                {
                    UserAccounts? User = _db.UserAccounts.Where(u => u.Email == UserEmail).First();

                    if (User == null) 
                    {
                        return NotFound(new { ErrorMessage = "User Not Found" });
                    }
                    else
                    {
                        return Ok(new {UserID = User.UserID});
                    }                    
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

    }
}
