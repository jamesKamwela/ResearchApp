using ResearchApp.DataStorage;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    [Table("EmployeePayments")]
    public class EmployeePayment : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int EmployeeId { get; set; }

        [Indexed]
        public int WorkRecordId { get; set; }

        public string EmployeeName { get; set; }
        public string JobName { get; set; }
        public string ClientName { get; set; }
        public decimal AmountEarned { get; set; }

        public DateTime CreatedAt { get; set; } 
        public DateTime UpdatedAt { get; set; } 
        public DateTime CompletionDate { get; set; }
        public bool IsPaid { get; set; } = false;
        public DateTime? PaidDate { get; set; }

        [Ignore]
        public Employee Employee { get; set; }

        [Ignore]
        public WorkRecord WorkRecord { get; set; }
    }
}
