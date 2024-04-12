using AutoMapper;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.DTOS;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/firebasedevices")]
    public class FirebaseDevicesController : Controller
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public FirebaseDevicesController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpPost("registernewfirebasedevice")]
        public IActionResult RegisterNewFirebaseDevice([FromBody] FirebaseDevices NewDevice)
        {
            try
            {
                FirebaseDevices? CheckForDevice = _db.FirebaseDevices.Where(f => f.DeviceModel == NewDevice.DeviceModel && f.DeviceName == NewDevice.DeviceName).FirstOrDefault();

                if (CheckForDevice == null)
                {
                    _db.Add(NewDevice);
                    _db.SaveChanges();

                    return Ok(NewDevice);
                }
                else
                {
                    CheckForDevice.FirebaseToken = NewDevice.FirebaseToken;
                    CheckForDevice.LoginExpiryDate = NewDevice.LoginExpiryDate;
                    CheckForDevice.UserAccountID = NewDevice.UserAccountID;

                    return Ok(CheckForDevice);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpPost("updatedeviceuserdetails")]
        public IActionResult UpdateDeviceUserDetails([FromBody] FirebaseDevices UserDevice)
        {
            try
            {
                FirebaseDevices? CurrentDevice = _db.FirebaseDevices.Where(f => f.FirebaseDeviceID == UserDevice.FirebaseDeviceID).First();

                if (CurrentDevice != null)
                {
                    CurrentDevice.UserAccountID = UserDevice.UserAccountID;
                    CurrentDevice.LoginExpiryDate = UserDevice.LoginExpiryDate;
                    _db.SaveChanges();

                    return Ok(CurrentDevice);
                }
                else
                {

                    return BadRequest(new { ErrorMessage = "No Firebase Device Registered" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }
    }
}
