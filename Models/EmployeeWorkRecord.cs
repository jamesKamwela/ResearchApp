using ResearchApp.DataStorage;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SQLiteNetExtensions.Attributes;


using System.Threading.Tasks;

namespace ResearchApp.Models
{

    /// <summary>
    /// Represents the many-to-many relationship between Employee and WorkRecord
    /// Stores when an employee was added to a work record using AddedDateTicks
    /// </summary>
    [Table("EmployeeWorkRecords")]
    public class EmployeeWorkRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [ForeignKey(typeof(Employee)), Indexed]
        public int EmployeeId { get; set; }

        [ForeignKey(typeof(WorkRecord)), Indexed]
        public int WorkRecordId { get; set; }

        /// <summary>
        /// Stores the date/time when employee was added to work record as binary ticks
        /// This is the actual database field - use AddedDate property for manipulation
        /// </summary>

        /// <summary>
        /// Helper property for easier date manipulation - not stored in database
        /// Use this property to get/set the actual DateTime value
        /// </summary>
      
        [Column("AddedDateTicks")]
        public long AddedDateTicks { get; set; }

        [Ignore]
        public DateTime AddedDate
        {
            get => new DateTime(AddedDateTicks, DateTimeKind.Utc); // Raw ticks → DateTime
            set => AddedDateTicks = value.Ticks; // DateTime → raw ticks
        }
        // Navigation Properties
        [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
        public Employee Employee { get; set; }

        [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
        public WorkRecord WorkRecord { get; set; }

        // Helper Methods
        /// <summary>
        /// Sets the AddedDate to current UTC time
        /// </summary>
        public void SetAddedDateToNow()
        {
            AddedDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Checks if this record was added within the specified number of days
        /// </summary>
        public bool WasAddedWithinDays(int days)
        {
            return AddedDate >= DateTime.UtcNow.AddDays(-days);
        }

        /// <summary>
        /// Gets a formatted string representation of when the employee was added
        /// </summary>
        public string GetFormattedAddedDate()
        {
            return AddedDate.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}