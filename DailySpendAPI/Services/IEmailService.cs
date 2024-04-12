using sib_api_v3_sdk.Api;
using sib_api_v3_sdk.Client;
using sib_api_v3_sdk.Model;
using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using DailyBudgetAPI.Models;

namespace DailyBudgetAPI.Services
{
    public interface IEmailService
    {
        public SendSmtpEmail CreateEmailVerificationEmail(int UserID, OTP UserOTP);
        public SendSmtpEmail CreateResetPasswordEmail(int UserID, OTP UserOTP);
        public SendSmtpEmail CreateOTPVerifiedEmail(int UserID, OTP UserOTP);
        public SendSmtpEmail CreateShareBudgetEmail(string UserEmail, OTP UserOTP);
        public SendSmtpEmail CreateOTPVerifiedEmailShareBudget(ShareBudgetRequest ShareRequest, OTP UserOTP);
        public string SendTransactionEmail(SendSmtpEmail SmtpEmail);
    }
}
