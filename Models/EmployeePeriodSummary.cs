using CommunityToolkit.Mvvm.Input;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    public partial class EmployeePeriodSummary
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; }
        public int JobCount { get; set; }
        public decimal Earnings { get; set; }
        public string Period { get; set; }

        [Ignore]
        public int Row { get; set; }

        [Ignore]
        public Employee Employee { get; set; }
        public string DisplayEarnings => $"{Earnings:N2} TL";
        public string DisplayNameWithId => $"{Name} (ID:#{EmployeeId})";
        public string EmployeeName => Name;
        public string FormattedDisplay => $"{Row + 1,2}. {Name,-30} | Jobs: {JobCount,3} |  Earnings: {Earnings:N2} TL";

      

        [RelayCommand]
        private async Task ViewDetails()
        {
            // Navigation command using the full Employee object
        }
    }
}
