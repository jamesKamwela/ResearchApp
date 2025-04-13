using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using ResearchApp.Models;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace ResearchApp.DataStorage
{

    public class ClientDatabaseService : IClientDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<ClientDatabaseService> _logger;
        private bool _isInitialized = false;

        public ClientDatabaseService(string databasePath, ILogger<ClientDatabaseService> logger)
        {

            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);
            _logger = logger;
            _ = InitializeDatabase();

        }

        private async Task InitializeDatabase()
        {
            if (!_isInitialized)
            {
                try
                {
                    await _database.CreateTableAsync<Client>();
                    await _database.CreateTableAsync<Job>();
                    _logger.LogInformation("Database tables created successfully");
                    _isInitialized = true;
                }
                catch (SQLiteException sqlEx)
                {
                    _logger.LogError(sqlEx, "Error creating database tables");
                    throw;
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, "General error during database initialization");
                    throw;
                }
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
        public async Task<Client> GetLastClientAsync()
        {
            try
            {
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
     

   
        public async Task<Client?> GetClientByDetailsAsync(string name, string phone, string address)
        {
            try
            {
                var normalizedName = name?.Trim().ToLowerInvariant();
                var normalizedPhone = phone?.Trim().ToLowerInvariant();
                var normalizedAddress = address?.Trim().ToLowerInvariant();

                var existingClient = await _database.Table<Client>()
                    .FirstOrDefaultAsync(c =>
                        c.Name.Trim().ToLowerInvariant() == normalizedName &&
                        c.Phone.Trim().ToLowerInvariant() == normalizedPhone &&
                        c.Address.Trim().ToLowerInvariant() == normalizedAddress);

                _logger.LogInformation(existingClient != null
                    ? $"Found existing client with ID: {existingClient.Id}"
                    : "No matching client found");

                return existingClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for client: {name}, {phone}, {address}");
                throw;
            }
        }

        


        public async Task<Client> SaveClientAsync(Client client)
        {
            try
            {
                // Use a transaction to ensure atomicity
                await _database.RunInTransactionAsync(async connection =>
                {
                    if (client.Id == 0)
                    {
                        await _database.InsertAsync(client);
                        _logger.LogInformation($"Inserted new client with ID: {client.Id}");
                    }
                    else
                    {
                        await _database.UpdateAsync(client);
                        _logger.LogInformation($"Updated existing client with ID: {client.Id}");
                    }
                    if (client.Jobs != null && client.Jobs.Count > 0)
                    {
                        foreach (var job in client.Jobs)
                        {
                            job.ClientId = client.Id;
                            if (job.Id == 0)
                                await _database.InsertAsync(job);
                            else
                                await _database.UpdateAsync(job);
                        }
                        _logger.LogInformation($"Saved {client.Jobs.Count} jobs for client {client.Id}");
                    }
                });

                return client;
           
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving client with ID: {client.Id}");
                throw;
            }
        }

        public async Task SaveJobsAsync(List<Job> jobs)
        {
            try
            {
                await _database.InsertAllAsync(jobs); // Saves all jobs in one operation
                _logger.LogInformation($"Saved {jobs.Count} jobs");
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
        public async Task<List<Job>> GetJobsByClientIdAsync(int clientId)
        {
            try
            {
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

        public async Task<int> DeleteClientAsync(Client client)
        {
            try
            {
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