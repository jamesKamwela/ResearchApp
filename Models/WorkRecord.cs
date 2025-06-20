using ResearchApp.DataStorage;
using ResearchApp.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ResearchApp.Models
{
    [Table("WorkRecords")]
    public class WorkRecord: IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int ClientId { get; set; }

        [Indexed]
        public int JobId { get; set; }

        public string JobName { get; set; }  // New field to store job name

        public string ClientName { get; set; }

        public DateTime WorkDate { get; set; }
        public decimal QuantityCompleted { get; set; }
        public decimal CommissionRate { get; set; }

        // Calculated fields
        public decimal TotalAmount { get; set; }
        public decimal AdminCommission { get; set; }
        public decimal EmployeePool { get; set; }

        public decimal AmountPerEmployee { get; set; }

        // Comma-separated employee IDs
        public string EmployeeIds { get; set; }

        // Status flags
        public bool IsJobCompleted { get; set; }  // New field to track job completion
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedDate { get; set; }  // New field for when job was marked complete
        public DateTime? PaidDate { get; set; }

        // Navigation properties
        [Ignore]
        public Client Client { get; set; }

        [Ignore]
        public Job Job { get; set; }
        public bool IsPaymentProcessed { get; set; }

        [Ignore]
        public List<Employee> Employees { get; set; } = new();

        [Ignore]
        public List<EmployeePayment> EmployeePayments { get; set; } = new();
        public int EmployeeCount { get; set; }  // New field to track the number of employees
        [Ignore]
        public List<EmployeeWorkRecord> EmployeeWorkRecords { get; set; } = new();

        [Ignore] // This tells SQLite not to map these to database columns
        public string DisplayClientName
        {
            get
            {
                // First try the navigation property
                if (Client != null && !string.IsNullOrEmpty(Client.Name))
                    return Client.Name;

                // Then try the direct ClientName field
                if (!string.IsNullOrEmpty(ClientName))
                    return ClientName;

                // Final fallback
                return "Unknown";
            }
        }

        [Ignore]
        public string DisplayJobName
        {
            get
            {
                // First try the navigation property
                if (Job != null && !string.IsNullOrEmpty(Job.JobName))
                    return Job.JobName;

                // Then try the direct JobName field
                if (!string.IsNullOrEmpty(JobName))
                    return JobName;

                // Final fallback
                return "Unknown";
            }
        }
        public List<int> GetEmployeeIdList() =>
            string.IsNullOrEmpty(EmployeeIds)
                ? new List<int>()
                : EmployeeIds.Split(',').Select(int.Parse).ToList();

        // Helper method to mark job as completed
        public void MarkCompleted()
        {
            IsJobCompleted = true;
            CompletedDate = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        // Helper method to mark payment as processed
        public void MarkPaid()
        {
            IsPaid = true;
            PaidDate = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

    }

}
