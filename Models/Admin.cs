using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    [Table("Admins")]
    public class Admin
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal TotalCommissionEarned { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Configuration properties
        public decimal DefaultCommissionPercentage { get; set; } = 25m;

        [Ignore]
        public List<WorkRecord> CommissionRecords { get; set; } = new();
    }
}
