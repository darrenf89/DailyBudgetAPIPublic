using DailyBudgetAPI.Models;
using DailyBudgetAPI.Data;
using sib_api_v3_sdk.Api;
using sib_api_v3_sdk.Client;
using sib_api_v3_sdk.Model;
using Newtonsoft.Json.Linq;


namespace DailyBudgetAPI.Services
{
    public class EmailService : IEmailService
    {       

        private readonly ApplicationDBContext _db;
        private readonly IProductTools _pt;
        private readonly IConfiguration _config;
        private readonly string _brevoAPIKey;

        public EmailService(ApplicationDBContext db, IConfiguration config, IProductTools pt)
        {
            _db = db;
            _config = config;
            _pt = pt;

            string BrevoAPIKey = _config.GetValue<string>("Brevo:apikey") ?? "";
            _brevoAPIKey = BrevoAPIKey;
        }

        public string SendTransactionEmail(SendSmtpEmail SmtpEmail) 
        {
            try
            {
                string SenderName = "dBudget";
                string SenderEmail = "support@dbugeting.com";
                SendSmtpEmailSender Email = new SendSmtpEmailSender(SenderName, SenderEmail);
                SmtpEmail.Sender = Email;

                if (!Configuration.Default.ApiKey.ContainsKey("api-key"))
                {
                    Configuration.Default.ApiKey.Add("api-key", _brevoAPIKey);
                }

                TransactionalEmailsApi apiInstance = new TransactionalEmailsApi();
                CreateSmtpEmail result = apiInstance.SendTransacEmail(SmtpEmail);

                string EmailID = result.MessageId;
                string Output = result.ToJson();

                return "OK";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            
        }

        public SendSmtpEmail CreateEmailVerificationEmail(int UserID, OTP UserOTP)
        {
            UserAccounts User = _db.UserAccounts.Where(u => u.UserID == UserID).First();

            if (User != null && UserOTP != null)
            {
                string ToEmail = User.Email;
                string ToName = User.NickName ?? "";
                SendSmtpEmailTo smtpEmailTo = new SendSmtpEmailTo(ToEmail, ToName);

                List<SendSmtpEmailTo> To = new List<SendSmtpEmailTo>
                {
                    smtpEmailTo
                };
                
                string ReplyToName = "dBudget";
                string ReplyToEmail = "Support@dbudgeting.com";
                SendSmtpEmailReplyTo ReplyTo = new SendSmtpEmailReplyTo(ReplyToEmail, ReplyToName);

                long? TemplateId = 1;

                JObject Params = new JObject();
                Params.Add("nickname", User.NickName ?? "");
                Params.Add("otpcode", UserOTP.OTPCode);

                return new SendSmtpEmail(null, To, null, null, null, null, null, ReplyTo, null, null, TemplateId, Params, null, null, null, null);
            }
            else
            {
                throw new Exception("Unable to create Email");
            }

        }

        public SendSmtpEmail CreateResetPasswordEmail(int UserID, OTP UserOTP)
        {
            UserAccounts User = _db.UserAccounts.Where(u => u.UserID == UserID).First();

            if (User != null && UserOTP != null)
            {
                string ToEmail = User.Email;
                string ToName = User.NickName ?? "";
                SendSmtpEmailTo smtpEmailTo = new SendSmtpEmailTo(ToEmail, ToName);

                List<SendSmtpEmailTo> To = new List<SendSmtpEmailTo>
                {
                    smtpEmailTo
                };

                string ReplyToName = "dBudget";
                string ReplyToEmail = "Support@dbudgeting.com";
                SendSmtpEmailReplyTo ReplyTo = new SendSmtpEmailReplyTo(ReplyToEmail, ReplyToName);

                long? TemplateId = 2;

                JObject Params = new JObject();
                Params.Add("nickname", User.NickName ?? "");
                Params.Add("otpcode", UserOTP.OTPCode);

                return new SendSmtpEmail(null, To, null, null, null, null, null, ReplyTo, null, null, TemplateId, Params, null, null, null, null);
            }
            else
            {
                throw new Exception("Unable to create Email");
            }

        }

        public SendSmtpEmail CreateShareBudgetEmail(string UserEmail, OTP UserOTP)
        {
            UserAccounts User = _db.UserAccounts.Where(u => u.Email == UserEmail).First();

            if (User != null && UserOTP != null)
            {
                string ToEmail = UserEmail;
                string ToName = User.NickName ?? "";
                SendSmtpEmailTo smtpEmailTo = new SendSmtpEmailTo(ToEmail, ToName);

                List<SendSmtpEmailTo> To = new List<SendSmtpEmailTo>
                {
                    smtpEmailTo
                };

                string ReplyToName = "dBudget";
                string ReplyToEmail = "Support@dbudgeting.com";
                SendSmtpEmailReplyTo ReplyTo = new SendSmtpEmailReplyTo(ReplyToEmail, ReplyToName);

                long? TemplateId = 5;

                JObject Params = new JObject();
                Params.Add("nickname", User.NickName ?? "");
                Params.Add("otpcode", UserOTP.OTPCode);

                return new SendSmtpEmail(null, To, null, null, null, null, null, ReplyTo, null, null, TemplateId, Params, null, null, null, null);
            }
            else
            {
                throw new Exception("Unable to create Email");
            }

        }

        public SendSmtpEmail CreateOTPVerifiedEmail(int UserID, OTP UserOTP)
        {
            UserAccounts User = _db.UserAccounts.Where(u => u.UserID == UserID).First();

            if (User != null && UserOTP != null)
            {
                string ToEmail = User.Email;
                string ToName = User.NickName ?? "";
                SendSmtpEmailTo smtpEmailTo = new SendSmtpEmailTo(ToEmail, ToName);

                List<SendSmtpEmailTo> To = new List<SendSmtpEmailTo>
                {
                    smtpEmailTo
                };

                string ReplyToName = "dBudget";
                string ReplyToEmail = "Support@dbudgeting.com";
                SendSmtpEmailReplyTo ReplyTo = new SendSmtpEmailReplyTo(ReplyToEmail, ReplyToName);

                long? TemplateId = 4;

                string OTPType = "";
                if(UserOTP.OTPType == "ValidateEmail")
                {
                    OTPType = "Email verification";
                }
                else if (UserOTP.OTPType == "ResetPassword")
                {
                    OTPType = "Password Reset";
                }

                string date = _pt.GetBudgetLocalTime(DateTime.UtcNow, User.DefaultBudgetID.GetValueOrDefault()).ToString("dddd dd MMMM yyyy");
                string time = _pt.GetBudgetLocalTime(DateTime.UtcNow, User.DefaultBudgetID.GetValueOrDefault()).ToString("HH:mm:ss");

                JObject Params = new JObject();
                Params.Add("nickname", User.NickName ?? "");
                Params.Add("otptype", OTPType);
                Params.Add("date", date);
                Params.Add("time", time);

                return new SendSmtpEmail(null, To, null, null, null, null, null, ReplyTo, null, null, TemplateId, Params, null, null, null, null);
            }
            else
            {
                throw new Exception("Unable to create Email");
            }

        }

        public SendSmtpEmail CreateOTPVerifiedEmailShareBudget(ShareBudgetRequest ShareRequest, OTP UserOTP)
        {
            UserAccounts? UserOne = _db.UserAccounts.Where(u => u.Email == ShareRequest.SharedByUserEmail).FirstOrDefault();
            UserAccounts? UserTwo = _db.UserAccounts.Where(u => u.Email == ShareRequest.SharedWithUserEmail).FirstOrDefault();

            if (ShareRequest != null && UserOTP != null && UserOne != null && UserTwo != null)
            {

                string ToEmail = ShareRequest.SharedByUserEmail;
                string ToName = UserOne.NickName ?? "";
                SendSmtpEmailTo smtpEmailToOne = new SendSmtpEmailTo(ToEmail, ToName);

                string ToEmailTwo = ShareRequest.SharedWithUserEmail;
                string ToNameTwo = UserTwo.NickName ?? "";
                SendSmtpEmailTo smtpEmailToTwo = new SendSmtpEmailTo(ToEmailTwo, ToNameTwo);

                List<SendSmtpEmailTo> To = new List<SendSmtpEmailTo>
                {
                    smtpEmailToOne,
                    smtpEmailToTwo
                };

                string ReplyToName = "dBudget";
                string ReplyToEmail = "Support@dbudgeting.com";
                SendSmtpEmailReplyTo ReplyTo = new SendSmtpEmailReplyTo(ReplyToEmail, ReplyToName);

                long? TemplateId = 6;

                string date = _pt.GetBudgetLocalTime(DateTime.UtcNow, ShareRequest.SharedBudgetID).ToString("dddd dd MMMM yyyy");
                string time = _pt.GetBudgetLocalTime(DateTime.UtcNow, ShareRequest.SharedBudgetID).ToString("HH:mm:ss");

                string EmailOne = string.IsNullOrEmpty(ShareRequest.SharedByUserEmail) ? "You" : ShareRequest.SharedByUserEmail;
                string EmailTwo = string.IsNullOrEmpty(ShareRequest.SharedWithUserEmail) ? "Your Friend" : ShareRequest.SharedWithUserEmail;

                JObject Params = new JObject();
                Params.Add("useroneemail", UserOne.NickName);
                Params.Add("usertwoemail", UserTwo.NickName);
                Params.Add("date", date);
                Params.Add("time", time);

                return new SendSmtpEmail(null, To, null, null, null, null, null, ReplyTo, null, null, TemplateId, Params, null, null, null, null);
            }
            else
            {
                throw new Exception("Unable to create Email");
            }
        }

    }

}

