using AutoMapper;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.DTOS;
using DailyBudgetAPI.Models;
using DailyBudgetAPI.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyBudgetAPI.Controllers
{
    [ApiController]
    [Route("api/v1/budgetsettings")]
    public class BudgetSettingsController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        private readonly ISecurityHelper _sh;

        private readonly IMapper _am;

        private readonly IProductTools _pt;

        public BudgetSettingsController(ApplicationDBContext db, ISecurityHelper sh, IMapper am, IProductTools pt)
        {
            _db = db;
            _sh = sh;
            _am = am;
            _pt = pt;
        }

        [HttpGet("getbudgetsettingsvalues/{BudgetId}")]
        public IActionResult GetBudgetSettingsValues([FromRoute] int BudgetId)
        {
            try
            {
                BudgetSettings? BS = _db.BudgetSettings?.Where(b => b.BudgetID == BudgetId).FirstOrDefault();
                if (BS != null)
                {
                    lut_CurrencySymbol Symbol = _db.lut_CurrencySymbols.Where(s => s.id == BS.CurrencySymbol).First();
                    lut_CurrencyDecimalSeparator DecimalSep = _db.lut_CurrencyDecimalSeparators.Where(d => d.id == BS.CurrencyDecimalSeparator).First();
                    lut_CurrencyGroupSeparator GroupSeparator = _db.lut_CurrencyGroupSeparators.Where(g => g.id == BS.CurrencyGroupSeparator).First();
                    lut_CurrencyDecimalDigits DecimalDigits = _db.lut_CurrencyDecimalDigits.Where(d => d.id == BS.CurrencyDecimalDigits).First();
                    lut_CurrencyPlacement CurrencyPositivePat = _db.lut_CurrencyPlacements.Where(c => c.id == BS.CurrencyPattern).First();
                    lut_DateSeperator DateSeperator = _db.lut_DateSeperators.Where(c => c.id == BS.DateSeperator).First();
                    lut_DateFormat DateFormat = _db.lut_DateFormats.Where(c => c.DateSeperatorID == BS.DateSeperator & c.ShortDatePatternID == BS.ShortDatePattern).First();
                    lut_BudgetTimeZone TimeZone = _db.lut_BudgetTimeZone.Where(b => b.TimeZoneID == BS.TimeZone).First();

                    BudgetSettingsDTO ReturnObject = new BudgetSettingsDTO();

                    ReturnObject.CurrencySymbol = Symbol.CurrencySymbol;
                    ReturnObject.CurrencyDecimalSeparator = DecimalSep.CurrencyDecimalSeparator;
                    ReturnObject.CurrencyGroupSeparator = GroupSeparator.CurrencyGroupSeparator;
                    ReturnObject.CurrencyDecimalDigits = Convert.ToInt32(DecimalDigits.CurrencyDecimalDigits);
                    ReturnObject.CurrencyPositivePattern = CurrencyPositivePat.CurrencyPositivePatternRef;
                    ReturnObject.ShortDatePattern = DateFormat.DateFormat;
                    ReturnObject.DateSeparator = DateSeperator.DateSeperator;
                    ReturnObject.TimeZoneName = TimeZone.TimeZoneName;
                    ReturnObject.TimeZoneUTCOffset = TimeZone.TimeZoneUTCOffset;

                    return Ok(ReturnObject);
                }
                else
                {
                    return NotFound(new { ErrorMessage = "Budget Settings Not Found" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }

        [HttpGet("getbudgetsettings/{BudgetId}")]
        public IActionResult GetBudgetSettings([FromRoute] int BudgetId)
        {
            try
            {
                BudgetSettings? BS = _db.BudgetSettings?.Where(b => b.BudgetID == BudgetId).FirstOrDefault();
                if (BS != null)
                {
                    return Ok(BS);
                }
                else
                {
                    return NotFound(new { ErrorMessage = "Budget Settings Not Found" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }

        }


        [HttpGet("getcurrencysymbols/{SearchQuery?}")]
        public IActionResult GetCurrencySymbols([FromRoute] string? SearchQuery)
        {
            try
            {
                List<lut_CurrencySymbol> Currencies = new List<lut_CurrencySymbol>();

                if (SearchQuery == null || string.IsNullOrWhiteSpace(SearchQuery))
                {
                    Currencies = _db.lut_CurrencySymbols.ToList();
                }
                else
                {
                    if (Int32.TryParse(SearchQuery, out int SymbolID))
                    {
                        Currencies = _db.lut_CurrencySymbols
                            .Where(t => t.id == SymbolID)
                            .ToList();
                    }
                    else
                    {
                        if (SearchQuery.Trim().Split(" ").Count() > 1)
                        {
                            string[] SearchQueries = SearchQuery.Trim().Split(" ");
                            List<lut_CurrencySymbol> TempList = new List<lut_CurrencySymbol>();

                            foreach (string s in SearchQueries)
                            {
                                string SearchString = "%" + s + "%" ?? "";

                                TempList = _db.lut_CurrencySymbols
                                    .Where(t => EF.Functions.Like(t.Name ?? "", SearchString) || EF.Functions.Like(t.Code ?? "", SearchString) || EF.Functions.Like(t.CurrencySymbol ?? "", SearchString))
                                    .ToList();

                                foreach (lut_CurrencySymbol temp in TempList)
                                {
                                    Currencies.Add(temp);
                                }
                            }
                        }
                        else
                        {
                            string SearchString = "%" + SearchQuery + "%" ?? "";

                            Currencies = _db.lut_CurrencySymbols
                                .Where(t => EF.Functions.Like(t.Name ?? "", SearchString) || EF.Functions.Like(t.Code ?? "", SearchString) || EF.Functions.Like(t.CurrencySymbol ?? "", SearchString))
                                .ToList();
                        }

                    }
                }

                if (Currencies == null || Currencies.Count() == 0)
                {
                    return NotFound(new { ErrorMessage = "No currencies found" });
                }
                else
                {
                    return Ok(Currencies);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getcurrencyplcements/{SearchQuery?}")]
        public IActionResult GetCurrencyPlacments([FromRoute] string? SearchQuery)
        {
            try
            {
                List<lut_CurrencyPlacement> Currencies = new List<lut_CurrencyPlacement>();

                if (SearchQuery == null || string.IsNullOrWhiteSpace(SearchQuery))
                {
                    Currencies = _db.lut_CurrencyPlacements.ToList();
                }
                else
                {
                    if (Int32.TryParse(SearchQuery, out int SymbolID))
                    {
                        Currencies = _db.lut_CurrencyPlacements
                            .Where(t => t.id == SymbolID)
                            .ToList();
                    }

                }

                if (Currencies == null || Currencies.Count() == 0)
                {
                    return NotFound(new { ErrorMessage = "No currencies found" });
                }
                else
                {
                    return Ok(Currencies);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getdateformatsbystring/{SearchQuery?}")]
        public IActionResult GetDateFormatsByString([FromRoute] string? SearchQuery)
        {
            try
            {
                List<lut_DateFormat> DateFormat = new List<lut_DateFormat>();

                if (SearchQuery == null || string.IsNullOrWhiteSpace(SearchQuery))
                {
                    DateFormat = _db.lut_DateFormats.ToList();
                }
                else
                {
                    if (SearchQuery.Trim().Split(" ").Count() > 1)
                    {
                        string[] SearchQueries = SearchQuery.Trim().Split(" ");
                        List<lut_DateFormat> TempList = new List<lut_DateFormat>();

                        foreach (string s in SearchQueries)
                        {
                            string SearchString = "%" + s + "%" ?? "";

                            TempList = _db.lut_DateFormats
                                .Where(t => EF.Functions.Like(t.DateFormat ?? "", SearchString))
                                .ToList();

                            foreach (lut_DateFormat temp in TempList)
                            {
                                DateFormat.Add(temp);
                            }
                        }
                    }
                    else
                    {
                        string SearchString = "%" + SearchQuery + "%" ?? "";

                        DateFormat = _db.lut_DateFormats
                            .Where(t => EF.Functions.Like(t.DateFormat ?? "", SearchString))
                            .ToList();
                    }

                }

                if (DateFormat == null || DateFormat.Count() == 0)
                {
                    return NotFound(new { ErrorMessage = "No currencies found" });
                }
                else
                {
                    return Ok(DateFormat);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getdateformatsbyid/{ShortDatePattern}/{Seperator}")]
        public IActionResult GetDateFormatsById([FromRoute] int ShortDatePattern, [FromRoute] int Seperator)
        {
            try
            {
                lut_DateFormat DateFormat = new lut_DateFormat();

                DateFormat = _db.lut_DateFormats
                            .Where(d => d.ShortDatePatternID == ShortDatePattern && d.DateSeperatorID == Seperator).First();

                if (DateFormat == null)
                {
                    return NotFound(new { ErrorMessage = "No currencies found" });
                }
                else
                {
                    return Ok(DateFormat);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getnumberformatsbyid/{CurrencyDecimalDigits}/{CurrencyDecimalSeparator}/{CurrencyGroupSeparator}")]
        public IActionResult GetNumberFormatsById([FromRoute] int CurrencyDecimalDigits, [FromRoute] int CurrencyDecimalSeparator, [FromRoute] int CurrencyGroupSeparator)
        {
            try
            {
                lut_NumberFormat DateFormat = new lut_NumberFormat();

                DateFormat = _db.lut_NumberFormats
                            .Where(d => d.CurrencyDecimalDigitsID == CurrencyDecimalDigits && d.CurrencyDecimalSeparatorID == CurrencyDecimalSeparator && d.CurrencyGroupSeparatorID == CurrencyGroupSeparator).First();

                if (DateFormat == null)
                {
                    return NotFound(new { ErrorMessage = "No settings found" });
                }
                else
                {
                    return Ok(DateFormat);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getnumberformats")]
        public IActionResult GetNumberFormats()
        {
            try
            {
                List<lut_NumberFormat> DateFormat = new List<lut_NumberFormat>();

                DateFormat = _db.lut_NumberFormats.ToList();

                if (DateFormat == null)
                {
                    return NotFound(new { ErrorMessage = "No settings found" });
                }
                else
                {
                    return Ok(DateFormat);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpPut("{BudgetId}")]
        public IActionResult UpdateBudgetSettings([FromRoute] int BudgetId, [FromBody] BudgetSettings BS)
        {
            if (BS == null)
            {
                return NotFound(new { ErrorMessage = "No settings found" });
            }

            if (BS.BudgetID !=  BudgetId) 
            {
                return BadRequest(new { ErrorMessage = "Budget Miss Match" });
            }

            try
            {
                _db.Attach(BS);
                _db.Update(BS);
                _db.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message});
            }
        }

        [HttpGet("getbudgettimezones/{SearchQuery?}")]
        public IActionResult GetBudgetTimeZone([FromRoute] string? SearchQuery)
        {
            try
            {

                List<lut_BudgetTimeZone> BudgetTimeZone = new List<lut_BudgetTimeZone>();


                if (SearchQuery == null || string.IsNullOrWhiteSpace(SearchQuery))
                {
                    BudgetTimeZone = _db.lut_BudgetTimeZone.ToList();
                }
                else
                {
                    if (SearchQuery.Trim().Split(" ").Count() > 1)
                    {
                        string[] SearchQueries = SearchQuery.Trim().Split(" ");
                        List<lut_BudgetTimeZone> TempList = new List<lut_BudgetTimeZone>();

                        foreach (string s in SearchQueries)
                        {
                            string SearchString = "%" + s + "%" ?? "";

                            TempList = _db.lut_BudgetTimeZone
                                .Where(t => EF.Functions.Like(t.TimeZoneName ?? "", SearchString))
                                .ToList();

                            foreach (lut_BudgetTimeZone temp in TempList)
                            {
                                BudgetTimeZone.Add(temp);
                            }
                        }
                    }
                    else
                    {
                        string SearchString = "%" + SearchQuery + "%" ?? "";

                        BudgetTimeZone = _db.lut_BudgetTimeZone
                            .Where(t => EF.Functions.Like(t.TimeZoneName ?? "", SearchString))
                            .ToList();
                    }

                }

                if (BudgetTimeZone == null || BudgetTimeZone.Count() == 0)
                {
                    return NotFound(new { ErrorMessage = "No BudgetTimeZone found" });
                }
                else
                {
                    return Ok(BudgetTimeZone);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("gettimezonebyid/{TimeZoneID}")]
        public IActionResult GetNumberFormatsById([FromRoute] int TimeZoneID)
        {
            try
            {
                lut_BudgetTimeZone TimeZones = new lut_BudgetTimeZone();

                TimeZones = _db.lut_BudgetTimeZone
                            .Where(d => d.TimeZoneID == TimeZoneID).First();

                if (TimeZones == null)
                {
                    return NotFound(new { ErrorMessage = "No Time zone found" });
                }
                else
                {
                    return Ok(TimeZones);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getshortdatepatternbyid/{ShortDatePatternID}")]
        public IActionResult GetShortDatePatternById([FromRoute] int ShortDatePatternID)
        {
            try
            {
                lut_ShortDatePattern ShortDatePattern = new lut_ShortDatePattern();

                ShortDatePattern = _db.lut_ShortDatePatterns
                            .Where(d => d.id == ShortDatePatternID).First();

                if (ShortDatePattern == null)
                {
                    return NotFound(new { ErrorMessage = "No Time zone found" });
                }
                else
                {
                    return Ok(ShortDatePattern);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getdateseperatorbyid/{DateSeperatorID}")]
        public IActionResult GetDateSeperatorById([FromRoute] int DateSeperatorID)
        {
            try
            {
                lut_DateSeperator DateSeperator = new lut_DateSeperator();

                DateSeperator = _db.lut_DateSeperators
                            .Where(d => d.id == DateSeperatorID).First();

                if (DateSeperator == null)
                {
                    return NotFound(new { ErrorMessage = "No Time zone found" });
                }
                else
                {
                    return Ok(DateSeperator);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getcurrencydecimaldigitsbyid/{CurrencyDecimalDigitsId}")]
        public IActionResult GetCurrencyDecimalDigitsById([FromRoute] int CurrencyDecimalDigitsId)
        {
            try
            {
                lut_CurrencyDecimalDigits DecimalDigits = new lut_CurrencyDecimalDigits();

                DecimalDigits = _db.lut_CurrencyDecimalDigits
                            .Where(d => d.id == CurrencyDecimalDigitsId).First();

                if (DecimalDigits == null)
                {
                    return NotFound(new { ErrorMessage = "No Decimal Digits  found" });
                }
                else
                {
                    return Ok(DecimalDigits);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getcurrencydecimalseparatorbyid/{CurrencyDecimalSeparatorId}")]
        public IActionResult GetCurrencyDecimalSeparatorById([FromRoute] int CurrencyDecimalSeparatorId)
        {
            try
            {
                lut_CurrencyDecimalSeparator DecimalSeparator = new lut_CurrencyDecimalSeparator();

                DecimalSeparator = _db.lut_CurrencyDecimalSeparators
                            .Where(d => d.id == CurrencyDecimalSeparatorId).First();

                if (DecimalSeparator == null)
                {
                    return NotFound(new { ErrorMessage = "No Decimal Separator found" });
                }
                else
                {
                    return Ok(DecimalSeparator);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpGet("getcurrencygroupseparatorbyid/{CurrencyGroupSeparatorId}")]
        public IActionResult GetCurrencyGroupSeparatorById([FromRoute] int CurrencyGroupSeparatorId)
        {
            try
            {
                lut_CurrencyGroupSeparator GroupSeparator = new lut_CurrencyGroupSeparator();

                GroupSeparator = _db.lut_CurrencyGroupSeparators
                            .Where(d => d.id == CurrencyGroupSeparatorId).First();

                if (GroupSeparator == null)
                {
                    return NotFound(new { ErrorMessage = "No Group Separator found" });
                }
                else
                {
                    return Ok(GroupSeparator);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }

        [HttpPatch("updatebudgetsettings/{BudgetID}")]
        public IActionResult UpdateBudgetSettings([FromRoute] int BudgetID, JsonPatchDocument<BudgetSettings> PatchDoc)
        {
            try
            {
                var BudgetSettings = _db.BudgetSettings
                    .Where(b => b.BudgetID == BudgetID).FirstOrDefault();

                if (BudgetSettings == null)
                {
                    return NotFound(new { ErrorMessage = "Object is Null" });
                }

                PatchDoc.ApplyTo(BudgetSettings, ModelState);

                if (!TryValidateModel(BudgetSettings))
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
    }
}
