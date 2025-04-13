using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResearchApp.Models;
using SQLite;

namespace ResearchApp.Models
{
    public abstract class Person
    {
        [PrimaryKey, AutoIncrement]
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string ?Address { get; set; }
    }
   
}
