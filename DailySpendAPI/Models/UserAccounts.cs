using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing.Text;
using System.Security.Cryptography;

namespace DailyBudgetAPI.Models
{
    public class UserAccounts
    {
        [Key]
        public int UserID { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? NickName { get; set; }
        public string Password { get; set; } = string.Empty;
        [Required]
        public string Salt { get; set; } = string.Empty;
        [Required]
        public DateTime AccountCreated { get; set; } = DateTime.UtcNow;
        [Required]
        public DateTime? LastLoggedOn { get; set; }
        [Required]
        public bool isDPAPermissions { get; set; } = false;
        public bool isAgreedToTerms { get; set; } = false;
        public bool isEmailVerified { get; set; } = false;
        public int? DefaultBudgetID { get; set; }
        public string? SubscriptionType { get; set; }
        public DateTime SubscriptionExpiry { get; set; }
        public List<Budgets> Budgets { get; set; } = new List<Budgets>();
        public string ProfilePicture { get; set; }

    }
}
