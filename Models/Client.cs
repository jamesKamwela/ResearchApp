using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
     public class Client
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    
        [Ignore]
        public List<Job> Jobs { get; set; } = new List<Job>();
        // Parameterless constructor required for SQLite
        public Client() { }
    }

  

}

