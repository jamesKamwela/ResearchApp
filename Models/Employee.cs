using ResearchApp.DataStorage;
using SQLite;
using SQLiteNetExtensions.Attributes;

namespace ResearchApp.Models
{
    [Table("Employees")]
    public class Employee : IEntity, IAuditable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string? Name { get; set; }

        [Indexed]
        public string? Phone { get; set; }

        [Indexed]
        public string? Address { get; set; }

        [Indexed]
        public decimal TotalEarnings { get; set; }

        public decimal PaidEarnings { get; set; }

        public decimal UnpaidEarnings => TotalEarnings - PaidEarnings;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [Indexed]
        public bool IsActive { get; set; }

        // Enhanced Navigation Properties with AddedDateTicks support
        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<EmployeeWorkRecord> EmployeeWorkRecords { get; set; } = new();

        /// <summary>
        /// Gets work records ordered by when the employee was added (most recent first)
        /// </summary>
        [Ignore]
        public List<WorkRecord> WorkRecords
        {
            get
            {
                return EmployeeWorkRecords?
                    .OrderByDescending(ewr => ewr.AddedDateTicks)
                    .Select(ewr => ewr.WorkRecord)
                    .Where(wr => wr != null)
                    .ToList() ?? new List<WorkRecord>();
            }
        }
        /// <summary>
        /// Gets work records added within the specified date range
        /// </summary>
        public List<WorkRecord> GetWorkRecordsByAddedDateRange(DateTime startDate, DateTime endDate)
        {
            var startTicks = startDate.ToBinary();
            var endTicks = endDate.ToBinary();

            return EmployeeWorkRecords?
                .Where(ewr => ewr.AddedDateTicks >= startTicks && ewr.AddedDateTicks <= endTicks)
                .OrderByDescending(ewr => ewr.AddedDateTicks)
                .Select(ewr => ewr.WorkRecord)
                .Where(wr => wr != null)
                .ToList() ?? new List<WorkRecord>();
        }

        /// <summary>
        /// Gets the most recent work records based on when employee was added
        /// </summary>
        public List<WorkRecord> GetRecentWorkRecords(int count = 10)
        {
            return EmployeeWorkRecords?
                .OrderByDescending(ewr => ewr.AddedDateTicks)
                .Take(count)
                .Select(ewr => ewr.WorkRecord)
                .Where(wr => wr != null)
                .ToList() ?? new List<WorkRecord>();
        }

        /// <summary>
        /// Gets work records where employee was added in the last N days
        /// </summary>
        public List<WorkRecord> GetWorkRecordsAddedInLastDays(int days)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var cutoffTicks = cutoffDate.ToBinary();

            return EmployeeWorkRecords?
                .Where(ewr => ewr.AddedDateTicks >= cutoffTicks)
                .OrderByDescending(ewr => ewr.AddedDateTicks)
                .Select(ewr => ewr.WorkRecord)
                .Where(wr => wr != null)
                .ToList() ?? new List<WorkRecord>();
        }

        /// <summary>
        /// Gets the total count of work records for this employee
        /// </summary>
        [Ignore]
        public int WorkRecordCount => EmployeeWorkRecords?.Count ?? 0;

        /// <summary>
        /// Gets the date when this employee was first added to any work record
        /// </summary>
        [Ignore]
        public DateTime? FirstWorkRecordAddedDate
        {
            get
            {
                var firstRecord = EmployeeWorkRecords?
                    .OrderBy(ewr => ewr.AddedDateTicks)
                    .FirstOrDefault();
                return firstRecord?.AddedDate;
            }
        }

        /// <summary>
        /// Gets the date when this employee was last added to a work record
        /// </summary>
        [Ignore]
        public DateTime? LastWorkRecordAddedDate
        {
            get
            {
                var lastRecord = EmployeeWorkRecords?
                    .OrderByDescending(ewr => ewr.AddedDateTicks)
                    .FirstOrDefault();
                return lastRecord?.AddedDate;
            }
        }
    }
}