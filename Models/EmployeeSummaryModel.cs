using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    public class EmployeeSummaryModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }

        // Performance metrics
        public int TotalJobsCompleted { get; set; }
        public decimal TotalAmountEarned { get; set; }
        public decimal TotalUnpaidAmount { get; set; }
        public int UnpaidJobsCount { get; set; }

        // Date tracking
        public DateTime? LastJobCompletedDate { get; set; }
        public DateTime? FirstJobDate { get; set; }

        // Calculated properties
        public decimal AveragePaymentPerJob => TotalJobsCompleted > 0 ? TotalAmountEarned / TotalJobsCompleted : 0;
        public bool HasUnpaidJobs => UnpaidJobsCount > 0;
        public decimal PendingPayments => TotalUnpaidAmount;

        // Status
        public bool IsActive => TotalJobsCompleted > 0;
    }
}
