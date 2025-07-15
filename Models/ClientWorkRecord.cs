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

    [Table("ClientWorkRecords")]
    public class ClientWorkRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [ForeignKey(typeof(Client)), Indexed]
        public int ClientId { get; set; }

        [ForeignKey(typeof(WorkRecord)), Indexed]
        public int WorkRecordId { get; set; }

        [Column("AddedDateTicks")]
        public long AddedDateTicks { get; set; }

        [Indexed]
        public bool IsPaid { get;set; }

        [Ignore]
        public DateTime AddedDate
        {
            get => new DateTime(AddedDateTicks, DateTimeKind.Utc); // Raw ticks → DateTime
            set => AddedDateTicks = value.Ticks; // DateTime → raw ticks
        }
        // Navigation Properties

        [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
        public WorkRecord WorkRecord { get; set; }

        [ManyToOne(CascadeOperations = CascadeOperation.CascadeRead)]
        public Client Client { get; set; }

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
