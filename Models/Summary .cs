using ResearchApp.DataStorage;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    [Table("Summeries")]
    public class Summary : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int SummaryId { get; set; }

        // Admin view fields
        public string ClientName { get; set; }
        public string JobName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AdminRevenue { get; set; }
        public int EmployeeCount { get; set; }
        public DateTime? CompletedDate { get; set; }

        // Employee view fields
        public string EmployeeName { get; set; }
        public int JobsCompleted { get; set; }
        public decimal JobEarnings { get; set; }
        public decimal TotalEarnings { get; set; }

        // Common fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
