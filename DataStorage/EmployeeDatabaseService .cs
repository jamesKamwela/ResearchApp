using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using ResearchApp.Models;
using ResearchApp.ViewModels;
using System.Diagnostics;

namespace ResearchApp.DataStorage
{
    public class EmployeeDatabaseService : IEmployeeDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public EmployeeDatabaseService(string databasePath)
        {
            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);
            try
            {
                _database.CreateTableAsync<Employee>().Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating Employee table: {ex.Message}");
                throw;
            }
        }
        public async Task<Employee>SaveEmployeeAsync(Employee employee)
        {
            try
            {
                await _database.InsertAsync(employee);
                Debug.WriteLine("Employee saved successfully.");
                return employee;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving employee: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Employee>> GetEmployeesAsync(int skip = 0, int take = 20)
        {
            try
            {

                return await _database.Table<Employee>()
                           .OrderBy(e => e.Name). // Important for consistent paging
                           Skip(skip).Take(take)
                           .
                           ToListAsync();

                //return await _database.Table<Employee>().ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching employee: {ex.Message}");
                return new List<Employee>(); // Return an empty list in case of an error
            }
        }

        public async Task<Employee> GetEmployeeByIdAsync(int id)
        {
            try
            {
                return await _database.Table<Employee>().Where(c => c.Id == id).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching employee with ID {id}: {ex.Message}");
                return null; // Return null in case of an error
            }
        }

        public async Task<int> DeleteEmployeeAsync(Employee employee)
        {
            try
            {
                return await _database.DeleteAsync(employee);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting employee: {ex.Message}");
                return 0; // Return 0 to indicate failure
            }
        }
        public async Task ResetDatabaseAsync()
        {
            try
            {
                // Drop the existing tables
                await _database.DropTableAsync<Employee>();

                // Recreate the tables
                await _database.CreateTableAsync<Employee>();

                Debug.WriteLine("Database reset successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting database: {ex.Message}");
                throw;
            }
        }

        public async Task AddEmployeeAsync(Employee employee)
        {
            await _database.InsertAsync(employee);
        }
    }
}