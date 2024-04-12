using System.Drawing.Printing;
using System.Security.Policy;
using DailyBudgetAPI.Data;
using DailyBudgetAPI.Models;

namespace DailyBudgetAPI.Services
{
    public interface ISecurityHelper
    {
        public string GenerateSalt(int nSalt);
        public bool CheckUniqueEmail(string Email);
        public string GenerateHashedPassword(string NonHasdedPassword, string Salt, int nIterations, int nHash);
        public bool CheckPassword(string Password, string Email);
        public UserAccounts CreateUserSecurityDetails(UserAccounts obj);
        public bool CheckEmailVerified(string Email);
        public string GenerateEmailToken(UserAccounts obj);
        public bool ValidateEmailToken(string token);
        public string GetClaim(string token, string claimType);

    }
}
