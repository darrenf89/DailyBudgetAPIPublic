using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing.Text;
using System.Security.Cryptography;

namespace DailyBudgetAPI.DTOS
{
    public class UserAccountsDTO
    {
        public int UserID { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? NickName { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool isEmailVerified { get; set; } = false;
        public int DefaultBudgetID { get; set; }
        public string? DefaultBudgetType { get; set; }
        public string Salt { get; set; } = string.Empty;
        public bool isDPAPermissions { get; set; }
        public bool isAgreedToTerms { get; set; }
        public string? FireBaseToken { get; set; }
        public string? SubscriptionType { get; set; }
        public DateTime? SubscriptionExpiry { get; set; }
        public string ProfilePicture { get; set; }


    }
}
