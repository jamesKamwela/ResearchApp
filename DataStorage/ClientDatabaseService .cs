using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using ResearchApp.Models;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ResearchApp.Exceptions;
namespace ResearchApp.DataStorage
{

    public class ClientDatabaseService : IClientDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<ClientDatabaseService> _logger;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);



        public ClientDatabaseService(string databasePath, ILogger<ClientDatabaseService> logger)
        {

            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);

            _logger = logger;

        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            await _initLock.WaitAsync();

            try
            {
                if (!_isInitialized)
                {
                    // Create tables (this will create single-column indexes from [Indexed] attributes)
                    await _database.CreateTableAsync<Client>();
                    await _database.CreateTableAsync<Job>();

                    await _database.ExecuteAsync(
      "CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_client ON Clients (Name, Phone, Address)");

                    _logger.LogInformation("Database tables and indexes created successfully");

                    _isInitialized = true;
                }
            }
            finally
            {
                _initLock.Release();
            }
        }
        public async Task ResetDatabaseAsync()
        {
            try
            {
                // Drop the existing tables
                await _database.DropTableAsync<Client>();
                await _database.DropTableAsync<Job>();

                // Recreate the tables
                await _database.CreateTableAsync<Client>();
                await _database.CreateTableAsync<Job>();

                _logger.LogInformation("Database reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting database");
                throw;
            }
        
        }




        public async Task<bool> UpdateClientAsync(Client client)
        {
            if (client == null)
            {
                _logger?.LogWarning("Update failed: Client object is null");
                return false;
            }

            try
            {
                // Direct update attempt without separate existence check
                int rowsAffected = await _database.UpdateAsync(client);

                if (rowsAffected == 1)
                {
                    _logger?.LogInformation("Client with ID {ClientId} updated successfully", client.Id);
                    return true;
                }

                // If no rows affected, verify if client exists
                var exists = await _database.Table<Client>()
                    .Where(c => c.Id == client.Id)
                    .CountAsync() > 0;

                _logger?.LogWarning(exists ?
                    $"Update affected {rowsAffected} rows (expected 1) for client ID {client.Id}" :
                    $"Client with ID {client.Id} not found");

                return false;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger.LogError(ex, "Constraint violation updating client ID {ClientId}", client.Id);
                throw new DataConstraintException(
                    ex.Message.Contains("Phone")
                        ? "Phone number must be unique"
                        : "Database constraint violated",
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client ID {ClientId}", client.Id);
                throw new DatabaseOperationException("Failed to update client", ex);
            }
        }
        public async Task<int> GetTotalClientCountAsync()
        {
            try
            {
                await InitializeAsync();

                return await _database.Table<Client>().CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total client count");
                Debug.WriteLine($"Error getting client count: {ex.Message}");
                return 0; // Return 0 if there's an error
            }
        }
        public async Task<Job> GetJobByIdAsync(int jobId)
        {
            try
            {
                await InitializeAsync();
                var job = await _database.Table<Job>()
                    .FirstOrDefaultAsync(j => j.Id == jobId);
                if (job != null)
                {
                    job.ClientId = job.ClientId; // Ensure ClientId is set
                }
                return job;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching job with ID: {jobId}");
                throw;
            }
        }
        public async Task<Client> GetLastClientAsync()
        {
            try
            {
                await InitializeAsync();
                // Fetch the last client based on the highest ID
                var lastClient = await _database.Table<Client>()
                                                .OrderByDescending(c => c.Id)
                                                .FirstOrDefaultAsync();

                if (lastClient != null)
                {
                    // Fetch the jobs for the last client
                    lastClient.Jobs = await _database.Table<Job>()
                                                     .Where(j => j.ClientId == lastClient.Id)
                                                     .ToListAsync();
                }

                _logger.LogInformation($"Retrieved last client: {lastClient?.Id ?? -1}");
                return lastClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last client");
                throw;
            }
        }
        public async Task<int> DeleteJobAsync(Job job)
        {
            try
            {
                await InitializeAsync();

                // First verify the job exists
                var existingJob = await _database.Table<Job>()
                    .FirstOrDefaultAsync(j => j.Id == job.Id);

                if (existingJob == null)
                {
                    _logger.LogWarning($"No job found with ID: {job.Id}");
                    return 0;
                }

                // Delete the job
                var result = await _database.DeleteAsync(job);

                if (result > 0)
                {
                    _logger.LogInformation($"Successfully deleted job with ID: {job.Id}");
                }
                else
                {
                    _logger.LogWarning($"No rows affected when deleting job with ID: {job.Id}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting job with ID: {job.Id}");
                throw;
            }
        }



        public async Task<Client?> GetClientByDetailsAsync(string name, string phone, string address)
        {
            try
            {
                await InitializeAsync();

                var normalizedName = name?.Trim().ToLowerInvariant();
                var normalizedPhone = NormalizePhone(phone);
                var normalizedAddress = address?.Trim().ToLowerInvariant();

                var existingClient = await _database.Table<Client>()
                    .Where(c => c.Name != null && c.Phone != null && c.Address != null)
                    .FirstOrDefaultAsync(c =>
                        c.Name.Trim().ToLowerInvariant() == normalizedName &&
                        NormalizePhone(c.Phone) == normalizedPhone &&
                        c.Address.Trim().ToLowerInvariant() == normalizedAddress);

                return existingClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for client: {name}, {phone}, {address}");
                throw;
            }
        }

        private string NormalizePhone(string phone)
        {
            return new string(phone?.Where(char.IsDigit).ToArray()) ?? string.Empty;
        }




        public async Task<Client> SaveClientAsync(Client client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            try
            {
                await InitializeAsync();

                client.Name = client.Name?.Trim().ToLowerInvariant();
                await _database.RunInTransactionAsync(async conn =>
                {
                    // Save the client first
                    if (client.Id == 0)
                    {
                        conn.Insert(client);
                        _logger.LogInformation($"Inserted new client with ID: {client.Id}");
                    }
                    else
                    {
                        conn.Update(client);
                        _logger.LogInformation($"Updated existing client with ID: {client.Id}");
                    }

                    // Then handle jobs if they exist
                    if (client.Jobs != null && client.Jobs.Count > 0)
                    {

                        foreach (var job in client.Jobs)
                        {
                            // Ensure the job is linked to the client
                            job.ClientId = client.Id;

                            if (job.Id == 0)
                            {
                                conn.Insert(job);
                            }
                            else
                            {
                                conn.Update(job);
                            }
                        }
                        _logger.LogInformation($"Saved {client.Jobs.Count} jobs for client {client.Id}");
                    }
                });

                return client;
            }
            
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger.LogWarning("Duplicate client detected");
                return await _database.Table<Client>()
                    .FirstOrDefaultAsync(c =>
                        c.Name == client.Name &&
                        c.Phone == client.Phone &&
                        c.Address == client.Address);
            }
        }


        public async Task SaveJobsAsync(List<Job> jobs)
        {
            try
            {
                if (jobs == null || jobs.Count == 0)
                {
                    _logger.LogWarning("Null/empty job list received");
                    return; // Early exit for no-ops
                }
                var invalidJobs = jobs.Where(j =>
                      j.ClientId <= 0 ||
                      string.IsNullOrWhiteSpace(j.JobName) ||
                      j.Amount <= 0
                  ).ToList();

                if (invalidJobs.Any())
                {
                    _logger.LogError($"Invalid jobs detected: {invalidJobs.Count}");
                    throw new ArgumentException("Jobs violate database constraints");
                }
                await _database.RunInTransactionAsync(async connection =>
                {
                    await _database.InsertAllAsync(jobs);
                    _logger.LogInformation($"Saved {jobs.Count} jobs for client {jobs[0].ClientId}");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving jobs");
                throw;
            }
        }



        public async Task<List<Client>> GetClientsAsync()
        {
            try
            {
                await InitializeAsync();

                var clients = await _database.Table<Client>().ToListAsync();

                // Fetch and assign jobs for each client
                foreach (var client in clients)
                {
                    client.Jobs = await _database.Table<Job>().Where(j => j.ClientId == client.Id).ToListAsync();
                }

                return clients;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching clients: {ex.Message}");
                return new List<Client>(); // Return an empty list in case of an error
            }
        }
          public async Task<Client> GetClientByIdAsync(int id)
        {
            try
            {
                await InitializeAsync();

                var client = await _database.Table<Client>()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (client != null)
                {
                    client.Jobs = await _database.Table<Job>()
                        .Where(j => j.ClientId == id)
                        .ToListAsync();
                }

                _logger.LogInformation(client != null
                    ? $"Retrieved client with ID: {id}"
                    : $"Client with ID: {id} not found");

                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching client with ID: {id}");
                throw;
            }
        }
        public async Task<Client> GetClientMinusJobsWithId(int clientId)
        {
            try
            {
                await InitializeAsync();
                var client = await _database.Table<Client>()
                    .FirstOrDefaultAsync(c => c.Id == clientId);
                if (client != null)
                {
                    client.Jobs = new List<Job>(); // Clear jobs
                }
                _logger.LogInformation(client != null
                    ? $"Retrieved client with ID: {clientId} without jobs"
                    : $"Client with ID: {clientId} not found");
                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching client with ID: {clientId}");
                throw;
            }
        }
        public async Task<List<Job>> GetJobsByClientIdAsync(int clientId)
        {
            try
            {
                await InitializeAsync();

                // Fetch all jobs for the specified client ID
                var jobs = await _database.Table<Job>()
                                          .Where(j => j.ClientId == clientId)
                                          .ToListAsync();

                Debug.WriteLine($"Fetched {jobs.Count} jobs for client ID {clientId}.");
                return jobs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching jobs for client ID {clientId}: {ex.Message}");
                return new List<Job>(); // Return an empty list in case of an error
            }
        }
        public async Task<List<Client>> GetClientsWithoutJobsAsync(int skip = 0, int take = 10)
        {
            try
            {
                await InitializeAsync();

                var clients = await _database.Table<Client>()
                    .OrderBy(c => c.Name)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {clients.Count} clients (without jobs)");
                return clients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching clients without jobs");
                throw;
            }
        }
        public async Task<bool> UpdateJobAsync(Job job)
        {
            if (job == null)
            {
                _logger?.LogWarning("Update failed: Job object is null");
                return false;
            }

            try
            {
                await InitializeAsync();

                // First verify the job exists
                var existingJob = await _database.Table<Job>()
                    .FirstOrDefaultAsync(j => j.Id == job.Id);

                if (existingJob == null)
                {
                    _logger?.LogWarning($"Job with ID {job.Id} not found");
                    return false;
                }

                // Perform the update
                int rowsAffected = await _database.UpdateAsync(job);

                if (rowsAffected == 1)
                {
                    _logger?.LogInformation("Job with ID {JobId} updated successfully", job.Id);
                    return true;
                }

                _logger?.LogWarning($"Update affected {rowsAffected} rows (expected 1) for job ID {job.Id}");
                return false;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger.LogError(ex, "Constraint violation updating job ID {JobId}", job.Id);
                throw new DataConstraintException("Database constraint violated", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating job ID {JobId}", job.Id);
                throw new DatabaseOperationException("Failed to update job", ex);
            }
        }

        public async Task<int> DeleteClientAsync(Client client)
        {
            try
            {
                await InitializeAsync();

                await _database.Table<Job>().DeleteAsync(j => j.ClientId == client.Id);
                var result = await _database.DeleteAsync(client);

                _logger.LogInformation($"Deleted client with ID: {client.Id} and associated jobs");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting client with ID: {client.Id}");
                throw;
            }
        }
       
    }

}