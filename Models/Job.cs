using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
      public class Job
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? JobName { get; set; }
        public decimal Amount { get; set; }
        public string? Unit { get; set; }

        [Indexed]
        public int ClientId { get; set; } // Foreign key to link Job to Client


    }
}
