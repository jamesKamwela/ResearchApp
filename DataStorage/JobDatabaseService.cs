using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using ResearchApp.Models;
using System.Diagnostics;

namespace ResearchApp.DataStorage
{
    public class JobDatabaseService: IJobDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public JobDatabaseService(string databasePath)
        {
            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);
            try
            {
                _database.CreateTableAsync<Job>().Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating Job table: {ex.Message}");
                throw;
            }
        }

        public async Task SaveJobAsync(Job job)
        {
            try
            {
                await _database.InsertAsync(job);
                Debug.WriteLine("Job saved successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving job: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Job>> GetJobsAsync(int clientId)
        {
            try
            {
                return await _database.Table<Job>().Where(j => j.ClientId == clientId).ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching jobs for client ID {clientId}: {ex.Message}");
                return new List<Job>(); // Return an empty list in case of an error
            }
        }

        public async Task<int> DeleteJobAsync(Job job)
        {
            try
            {
                return await _database.DeleteAsync(job);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting job: {ex.Message}");
                return 0; // Return 0 to indicate failure
            }
        }
    }
}