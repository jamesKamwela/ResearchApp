using SQLite;

namespace ResearchApp.Models
{
    public class Employee 
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    
}
}