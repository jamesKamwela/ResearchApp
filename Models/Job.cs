using ResearchApp.DataStorage;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    [Table("Jobs")]
    public class Job : IEntity, IAuditable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public string? JobName { get; set; }
        [Indexed]
        public decimal Amount { get; set; }
        [Indexed]
        public string? Unit { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [Indexed]
        public int ClientId { get; set; }
        [Ignore]
        public List<WorkRecord> WorkRecords { get; set; } = new();

      
    
      public Job()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }
    }
}