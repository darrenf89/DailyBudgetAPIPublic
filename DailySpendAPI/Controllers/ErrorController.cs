using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using DailyBudgetAPI.DTOS;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.JsonPatch;
using AutoMapper;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/error")]
    public class ErrorController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        public ErrorController(ApplicationDBContext db, ISecurityHelper sh, IMapper am)
        {
            _db = db;
            _sh = sh;
            _am = am;
        }

        [HttpPost("adderrorlogentry")]
        public IActionResult AddErrorLogEntry([FromBody] ErrorLog NewLog)
        {
            _db.ErrorLog.Add(NewLog);
            _db.SaveChanges();

            if (NewLog.ErrorLogID != 0)
            { 
                ErrorLog ReturnLog = new();
                ReturnLog.ErrorLogID = NewLog.ErrorLogID;
                string ErrorLogReference = string.Concat(NewLog.ErrorMethod.Substring(0,2), NewLog.ErrorPage.Substring(0, 2), NewLog.ErrorLogID.ToString(), NewLog.WhenAdded.GetValueOrDefault().ToString("dd"), NewLog.WhenAdded.GetValueOrDefault().ToString("MM"), NewLog.WhenAdded.GetValueOrDefault().ToString("yy"));
                
                switch (NewLog.ErrorMessage)
                {
                    case "Connectivity":
                        ReturnLog.ErrorMessage = "You have no Internet Connection, unfortunately you need that. Please try again when you are back in civilised society";
                        break;
                    default:
                        ReturnLog.ErrorMessage = $"Something has gone wrong on the {NewLog.ErrorPage} Page, your error has been logged under {ErrorLogReference.ToUpper()}. We promise you we will look into it at some point, although if you want to contact us before that .... just don't!";
                        break;
                }

                return Ok(ReturnLog);
            }
            else
            {
                return BadRequest();
            }

        }

    }
}
