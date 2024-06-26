﻿using System.ComponentModel.DataAnnotations;

namespace DailyBudgetAPI.Models
{
    public class IncomeEvents
    {
        [Key]
        public int IncomeEventID { get; set; }
        public decimal IncomeAmount { get; set; }
        public string IncomeName { get; set; } = "";
        public DateTime IncomeActiveDate { get; set; } = DateTime.UtcNow;
        public DateTime DateOfIncomeEvent { get; set; } = DateTime.UtcNow;
        public bool isRecurringIncome { get; set; }
        public string? RecurringIncomeType { get; set; } 
        public int? RecurringIncomeValue { get; set; } 
        public string? RecurringIncomeDuration { get; set; }
        public bool isClosed { get; set; } = true;
        public bool? isInstantActive { get; set; }
        public bool? isIncomeAddedToBalance { get; set; } = false;
    }
}
