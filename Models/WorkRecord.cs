using ResearchApp.DataStorage;
using ResearchApp.Models;
using SQLite;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    [Table("WorkRecords")]

    public class WorkRecord : IEntity, IAuditable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [ForeignKey(typeof(Client))]
        public int ClientId { get; set; }

        [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead | CascadeOperation.CascadeInsert)]
        public Client Client { get; set; }

        [ForeignKey(typeof(Job))]
        public int JobId { get; set; }

        public string JobName { get; set; }
        public string ClientName { get; set; }
        public DateTime WorkDate { get; set; }
        public DateTime EntryDate { get; set; }
        public string Unit { get; set; } 
        public decimal QuantityCompleted { get; set; }
        public decimal CommissionRate { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal AdminCommission { get; set; }
        public decimal EmployeePool { get; set; }
        public decimal AmountPerEmployee { get; set; }

        // Status flags
        public bool IsJobCompleted { get; set; }
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public bool IsPaymentProcessed { get; set; }
        public int EmployeeCount { get; set; }

      
        [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
        public Job Job { get; set; }



        // Enhanced Navigation Properties with AddedDateTicks support
        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<EmployeeWorkRecord> EmployeeWorkRecords { get; set; } = new();

        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<EmployeeWorkRecord> ClientWorkRecords { get; set; } = new();



        /// <summary>
        /// Gets employees ordered by when they were added to this work record (most recent first)
        /// </summary>
        [Ignore]
        public List<Employee> Employees
        {
            get
            {
                return EmployeeWorkRecords?
                    .OrderByDescending(ewr => ewr.AddedDateTicks)
                    .Select(ewr => ewr.Employee)
                    .Where(emp => emp != null)
                    .ToList() ?? new List<Employee>();
            }
        }

      



        /// <summary>
        /// Gets the date when the first employee was added to this work record
        /// </summary>


        // Existing display properties
        [Ignore]
        public string DisplayClientName
        {
            get
            {
                if (Client != null && !string.IsNullOrEmpty(Client.Name))
                    return Client.Name;
                if (!string.IsNullOrEmpty(ClientName))
                    return ClientName;
                return "Unknown";
            }
        }

        public string FormattedQuantity => $"{QuantityCompleted:N0} {Unit}";

        [Ignore]
        public string DisplayJobName
        {
            get
            {
                if (Job != null && !string.IsNullOrEmpty(Job.JobName))
                    return Job.JobName;
                if (!string.IsNullOrEmpty(JobName))
                    return JobName;
                return "Unknown";
            }
        }

        // Helper methods
        public void MarkCompleted()
        {
            IsJobCompleted = true;
            CompletedDate = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkPaid()
        {
            IsPaid = true;
            PaidDate = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}