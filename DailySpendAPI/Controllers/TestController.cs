using AutoMapper;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/test")]
    public class TestController : Controller
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        private readonly IEmailService _es;

        public TestController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt, IEmailService es)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
            _es = es;
        }

        [HttpGet("TestMe")]
        public IActionResult Test()
        {
            _pt.RecalculateAfterTransactionUpdate(49,0);
            return Ok();
        }

        [HttpGet("testnotification/{UserID}")]
        public IActionResult TestNotification([FromRoute] int UserID)
        {
            FirebaseDevices? Device = _db.FirebaseDevices.Where(u => u.UserAccountID == UserID && u.LoginExpiryDate >= DateTime.UtcNow).OrderByDescending(u => u.LoginExpiryDate).FirstOrDefault();

            if (Device != null)
            {
                Dictionary<string, string> AndroidData = new Dictionary<string, string>
                {
                    { "NavigationType", "Test" }
                };

                Message Message = new Message
                {
                    Token = Device.FirebaseToken,
                    Notification = new Notification
                    {
                        Title = "Test Title",
                        Body = $"Test Body"
                    },
                    Data = AndroidData
                };

                string status = _pt.SendPushNotification(Message).Result;
            }

            return Ok();

        }

    }
}
