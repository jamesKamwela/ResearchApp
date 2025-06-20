using ResearchApp.DataStorage;
using SQLite;

namespace ResearchApp.Models
{
    public class Employee : IEntity, IAuditable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public decimal TotalEarnings { get; set; }
        public decimal PaidEarnings { get; set; }
        public decimal UnpaidEarnings => TotalEarnings - PaidEarnings;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        [Ignore]
        public bool IsActive { get; set; }

        [Ignore]
        public List<EmployeeWorkRecord> EmployeeWorkRecords { get; set; } = new();

        [Ignore]
        public List<WorkRecord> WorkRecords { get; set; } = new();
    }
}
     