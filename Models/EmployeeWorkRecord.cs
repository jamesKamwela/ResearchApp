using ResearchApp.DataStorage;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    // EmployeeWorkRecord.cs
    [Table("EmployeeWorkRecords")]
    public class EmployeeWorkRecord : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int EmployeeId { get; set; }

        [Indexed]
        public int WorkRecordId { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.UtcNow;

        // Navigation properties (optional but useful)
        [Ignore]
        public Employee Employee { get; set; }

        [Ignore]
        public WorkRecord WorkRecord { get; set; }
    }
}
