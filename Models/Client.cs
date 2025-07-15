using ResearchApp.DataStorage;
using SQLite;
using SQLiteNetExtensions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Models
{
    [Table("Clients")]
    public class Client : IEntity, IAuditable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        private string _name;
        [Indexed]
        public string? Name
        {
            get => _name;
            set => _name = value?.Trim().ToLowerInvariant();
        }

        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<ClientWorkRecord> ClientWorkRecords { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        [Indexed]
        public string? Phone { get; set; }

        [Indexed]
        public string? Address { get; set; }
    
        [Ignore]
        public List<Job> Jobs { get; set; } = new List<Job>();
        // Parameterless constructor required for SQLite
        public Client() { }
    }

    public class ClientCompletionStats
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int CompletedJobsCount { get; set; }
        public decimal TotalEarnings { get; set; }
        public string FormattedTotalEarnings => $"{TotalEarnings:N2} ₺";
    }

}

