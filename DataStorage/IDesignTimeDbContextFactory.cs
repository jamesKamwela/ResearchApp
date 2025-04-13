using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.EFDatabase
{
    public class ContactsDatabaseFactory : IDesignTimeDbContextFactory<ContactsDatabase>
    {
        public ContactsDatabase CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ContactsDatabase>();
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Contacts.db");
            optionsBuilder.UseSqlite($"Filename={dbPath}");

            return new ContactsDatabase();
        }
    }
}
