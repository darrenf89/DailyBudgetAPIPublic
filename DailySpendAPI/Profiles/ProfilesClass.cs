using AutoMapper;
using DailyBudgetAPI.DTOS;
using DailyBudgetAPI.Models;

namespace DailyBudgetAPI.Profiles
{
    public class ProfilesClass : Profile
    {
        public ProfilesClass()
        {
            CreateMap<UserAccounts, UserAccountsDTO>();
            CreateMap<UserAccountsDTO, UserAccounts>();
        }
    }
}
