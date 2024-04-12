using DailyBudgetAPI.Models;
using DailyBudgetAPI.Data;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Azure.Core.GeoJson;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace DailyBudgetAPI.Services
{
    public class SecurityHelper: ISecurityHelper
    {
        private readonly ApplicationDBContext _db;

        private readonly IConfiguration _config;

        public SecurityHelper(ApplicationDBContext db, IConfiguration config)
        {
            _db = db;
            //var UserAccount = _db.UserAccounts.ToList();
            _config = config;
        }

        public UserAccounts CreateUserSecurityDetails(UserAccounts obj)
        {
            int nHash = Convert.ToInt32(_config.GetValue<string>("MySettings:nHash"));
            int nIteraitons = Convert.ToInt32(_config.GetValue<string>("MySettings:nIterations"));

            Random rnd = new();
            int number = rnd.Next(2000);
            string Salt = GenerateSalt(number);

            string HashedPassword = GenerateHashedPassword(obj.Password, Salt, nIteraitons, nHash);

            obj.Password = HashedPassword;
            obj.Salt = Salt;

            return obj;
        }

        public string GenerateSalt(int nSalt)
        {
            Byte[] saltBytes = new Byte[nSalt];
            RandomNumberGenerator.Create().GetNonZeroBytes(saltBytes);

            return Convert.ToBase64String(saltBytes);

        }

        public string GenerateHashedPassword(string NonHasdedPassword, string Salt, int nIterations, int nHash)
        {

            Byte[] saltBytes = Convert.FromBase64String(Salt);

            Rfc2898DeriveBytes obj = new(NonHasdedPassword, saltBytes, nIterations);

            using (obj)
            {
                return Convert.ToBase64String(obj.GetBytes(nHash));
            }

        }

        public bool CheckUniqueEmail(string Email)
        {
            var UserEmail = _db.UserAccounts
                .Where(x => x.Email == Email);

            if (UserEmail.Any())
            {
                return false;
            }
            else
            {
                return true;
            }

        }

        public bool CheckEmailVerified(string Email)
        {
            try
            {
                var Users = _db.UserAccounts
                    .Where(x => x.Email == Email);

                var User = Users.First();

                if (User.isEmailVerified == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (ArgumentNullException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public bool CheckPassword(string Password, string Email)
        {


            try
            {
                var UserAccounts = _db.UserAccounts
                    .Where(x => x.Email == Email);

                int nHash = Convert.ToInt32(_config.GetValue<string>("MySettings:nHash"));
                int nIteraitons = Convert.ToInt32(_config.GetValue<string>("MySettings:nIterations"));

                var UserDetails = UserAccounts.First();
                string Salt = UserDetails.Salt;
                string HashedPassword = GenerateHashedPassword(Password, Salt, nIteraitons, nHash);

                if (UserDetails.Password == HashedPassword)
                {
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (ArgumentNullException)
            {
                return false;
            }
            catch(InvalidOperationException)
            {
                return false;
            }

        }

        public string GenerateEmailToken(UserAccounts obj)
        {

            var TokenHandler = new JwtSecurityTokenHandler();
            var mySecurityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_config.GetValue<string>("MySettings:mySecret")));

            var TokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, obj.Email),
                    }),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = _config.GetValue<string>("MySettings:myIssuer"),
                Audience = _config.GetValue<string>("MySettings:myAudience"),
                SigningCredentials = new SigningCredentials(mySecurityKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var Token = TokenHandler.CreateToken(TokenDescriptor);

            return TokenHandler.WriteToken(Token);
        }

        public bool ValidateEmailToken(string Token)
        {
            var TokenHandler = new JwtSecurityTokenHandler();
            var mySecurityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_config.GetValue<string>("MySettings:mySecret")));
            try
            {
                TokenHandler.ValidateToken(Token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _config.GetValue<string>("MySettings:myIssuer"),
                    ValidAudience = _config.GetValue<string>("MySettings:myAudience"),
                    IssuerSigningKey = mySecurityKey
                }, out SecurityToken validatedToken);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public string GetClaim(string token, string claimType)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

            var stringClaimValue = securityToken.Claims.First(claim => claim.Type == claimType).Value;
            return stringClaimValue;
        }

    }
}
