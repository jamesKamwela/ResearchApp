using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    public class ClientDisplayModel : ContactDisplayModel
    {
       
        public string? Jobs { get; set; }

        // Parameterless constructor required for SQLite
    }
}
