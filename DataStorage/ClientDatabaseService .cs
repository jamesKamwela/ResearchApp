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
using System.Linq.Expressions;
using SQLiteNetExtensions.Extensions;       // For synchronous operations
using SQLiteNetExtensionsAsync.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;


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
            _database = new SQLiteAsyncConnection(databasePath ?? throw new ArgumentNullException(nameof(databasePath)), Constants.Flags);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Initialization and Core Methods

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogDebug("Database already initialized");
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogInformation("Initializing database tables...");

                    await CreateTablesAsync();
                    await MigrateSchema();
                    await ValidateSchema();
                    await CreateIndexesAsync();

                    _isInitialized = true;
                    _logger.LogInformation("Database initialized successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        private async Task CreateTablesAsync()
        {
            try
            {
                _logger.LogInformation("Creating database tables");
                await _database.CreateTableAsync<Client>();
                await _database.CreateTableAsync<Job>();
                _logger.LogInformation("Database tables created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database tables");
                throw;
            }
        }

        private async Task CreateIndexesAsync()
        {
            try
            {
                var indexCommands = new List<string>
                {
                    "CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_client ON Clients (Name, Phone, Address)",
                    "CREATE INDEX IF NOT EXISTS idx_client_created ON Clients (CreatedAt)",
                    "CREATE INDEX IF NOT EXISTS idx_client_updated ON Clients (UpdatedAt)",
                    "CREATE INDEX IF NOT EXISTS idx_job_clientid ON Jobs (ClientId)",
                    "CREATE INDEX IF NOT EXISTS idx_job_name ON Jobs (JobName)",
                    "CREATE INDEX IF NOT EXISTS idx_job_amount ON Jobs (Amount)"
                };

                int successCount = 0;
                foreach (var command in indexCommands)
                {
                    try
                    {
                        await _database.ExecuteAsync(command);
                        _logger.LogDebug($"Created index: {command}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to create index: {command}");
                    }
                }
                _logger.LogInformation($"Created {successCount}/{indexCommands.Count} indexes successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index creation failed");
                throw;
            }
        }

        private async Task MigrateSchema()
        {
            var currentVersion = await GetSchemaVersion();
            var targetVersion = 1;

            if (currentVersion < targetVersion)
            {
                _logger.LogInformation($"Migrating database from version {currentVersion} to {targetVersion}");
                await SetSchemaVersion(targetVersion);
            }
            else
            {
                _logger.LogDebug($"No migration needed (current version: {currentVersion}, target version: {targetVersion})");
            }
        }

        private async Task<int> GetSchemaVersion()
        {
            try
            {
                _logger.LogDebug("Checking schema version");
                await _database.ExecuteAsync("CREATE TABLE IF NOT EXISTS SchemaVersion (Version INTEGER)");
                var version = await _database.ExecuteScalarAsync<int>("SELECT Version FROM SchemaVersion LIMIT 1");
                _logger.LogDebug($"Current schema version: {version}");
                return version;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting schema version, assuming version 0");
                return 0;
            }
        }

        private async Task SetSchemaVersion(int version)
        {
            _logger.LogDebug($"Setting schema version to {version}");
            await _database.ExecuteAsync("DELETE FROM SchemaVersion");
            await _database.ExecuteAsync("INSERT INTO SchemaVersion (Version) VALUES (?)", version);
        }

        private async Task ValidateSchema()
        {
            try
            {
                _logger.LogDebug("Starting schema validation");

                var expectedSchemas = new Dictionary<string, Dictionary<string, Type>>
                {
                    {
                        "Clients",
                        new Dictionary<string, Type>
                        {
                            { "Id", typeof(int) },
                            { "Name", typeof(string) },
                            { "Phone", typeof(string) },
                            { "Address", typeof(string) },
                            { "CreatedAt", typeof(DateTime) },
                            { "UpdatedAt", typeof(DateTime?) }
                        }
                    },
                    {
                        "Jobs",
                        new Dictionary<string, Type>
                        {
                            { "Id", typeof(int) },
                            { "JobName", typeof(string) },
                            { "Amount", typeof(decimal) },
                            { "Unit", typeof(string) },
                            { "CreatedAt", typeof(DateTime) },
                            { "UpdatedAt", typeof(DateTime?) },
                            { "ClientId", typeof(int) }
                        }
                    }
                };

                bool validationSuccessful = true;
                var validationErrors = new List<string>();

                foreach (var tableSchema in expectedSchemas)
                {
                    string tableName = tableSchema.Key;
                    var expectedColumns = tableSchema.Value;

                    try
                    {
                        _logger.LogDebug($"Validating table: {tableName}");
                        var tableInfo = await _database.GetTableInfoAsync(tableName);

                        if (tableInfo == null || tableInfo.Count == 0)
                        {
                            validationErrors.Add($"Table '{tableName}' not found or has no columns");
                            validationSuccessful = false;
                            continue;
                        }

                        var missingColumns = expectedColumns.Keys
                            .Where(expected => !tableInfo.Any(c => c.Name.Equals(expected, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        if (missingColumns.Any())
                        {
                            validationErrors.Add($"Table '{tableName}' missing columns: {string.Join(", ", missingColumns)}");
                            validationSuccessful = false;
                        }

                        _logger.LogDebug($"Table '{tableName}' validation completed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error validating table {tableName}");
                        validationErrors.Add($"Error validating table {tableName}: {ex.Message}");
                        validationSuccessful = false;
                    }
                }

                if (!validationSuccessful)
                {
                    var errorMessage = $"Schema validation failed with {validationErrors.Count} error(s):\n" +
                                      string.Join("\n", validationErrors.Select((e, i) => $"{i + 1}. {e}"));
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                _logger.LogInformation("Schema validation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schema validation failed");
                throw;
            }
        }

        public async Task ResetDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Resetting database...");

                // Drop existing tables
                await _database.DropTableAsync<Client>();
                await _database.DropTableAsync<Job>();

                // Reset initialization flag
                _isInitialized = false;

                // Reinitialize
                await InitializeAsync();

                _logger.LogInformation("Database reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting database");
                throw;
            }
        }

        #endregion

        #region Client Operations

        public async Task<Client> SaveClientAsync(Client client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            try
            {
                await EnsureInitializedAsync();

                client.Name = client.Name?.Trim().ToLowerInvariant();
                client.UpdatedAt = DateTime.Now;
                if (client.Id == 0)
                    client.CreatedAt = DateTime.Now;

                await _database.RunInTransactionAsync(async conn =>
                {
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

                    if (client.Jobs?.Count > 0)
                    {
                        foreach (var job in client.Jobs)
                        {
                            job.ClientId = client.Id;
                            job.UpdatedAt = DateTime.Now;
                            if (job.Id == 0)
                            {
                                job.CreatedAt = DateTime.Now;
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
                _logger.LogWarning("Duplicate client detected, returning existing client");
                return await GetClientByDetailsAsync(client.Name, client.Phone, client.Address);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving client");
                throw new DatabaseOperationException("Failed to save client", ex);
            }
        }

        public async Task<bool> UpdateClientAsync(Client client)
        {
            if (client == null)
            {
                _logger.LogWarning("Update failed: Client object is null");
                return false;
            }

            try
            {
                await EnsureInitializedAsync();

                client.UpdatedAt = DateTime.Now;
                int rowsAffected = await _database.UpdateAsync(client);

                if (rowsAffected == 1)
                {
                    _logger.LogInformation("Client with ID {ClientId} updated successfully", client.Id);
                    return true;
                }

                var exists = await ClientExistsAsync(client.Id);
                _logger.LogWarning(exists ?
                    $"Update affected {rowsAffected} rows (expected 1) for client ID {client.Id}" :
                    $"Client with ID {client.Id} not found");

                return false;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger.LogError(ex, "Constraint violation updating client ID {ClientId}", client.Id);
                throw new DataConstraintException("Database constraint violated", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client ID {ClientId}", client.Id);
                throw new DatabaseOperationException("Failed to update client", ex);
            }
        }

        public async Task<int> DeleteClientAsync(Client client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            try
            {
                await EnsureInitializedAsync();

                await _database.RunInTransactionAsync(async conn =>
                {
                    // Delete associated jobs first
                    var jobCount = await _database.Table<Job>().Where(j => j.ClientId == client.Id).CountAsync();
                    await _database.Table<Job>().DeleteAsync(j => j.ClientId == client.Id);

                    // Delete the client
                    await _database.DeleteAsync(client);

                    _logger.LogInformation($"Deleted client with ID: {client.Id} and {jobCount} associated jobs");
                });

                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting client with ID: {client.Id}");
                throw;
            }
        }

        public async Task<List<Client>> GetAllClientsMinusJobsAsync()
        
        {
            try
            {
                await EnsureInitializedAsync();
                var clients = await _database.Table<Client>().ToListAsync();
                foreach (var client in clients)
                {
                    client.Jobs = new List<Job>(); // Clear jobs
                }
                _logger.LogDebug($"Retrieved {clients.Count} clients without jobs");
                return clients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all clients without jobs");
                return new List<Client>();
            }


        }
        public async Task<Client?> GetClientByIdAsync(int id)
        {
            try
            {
                await EnsureInitializedAsync();

                var client = await _database.Table<Client>()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (client != null)
                {
                    client.Jobs = await GetJobsByClientIdAsync(client.Id);
                }

                _logger.LogDebug(client != null
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
        public async Task<Client?> GetClientByDetailsAsync(string name, string phone, string address)
        {
            try
            {
                await EnsureInitializedAsync();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address))
                {
                    _logger.LogWarning("GetClientByDetailsAsync called with null/empty parameters");
                    return null;
                }

                var normalizedName = name.Trim().ToLowerInvariant();
                var normalizedPhone = NormalizePhone(phone);
                var normalizedAddress = address.Trim().ToLowerInvariant();

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

        public async Task<List<Client>> GetClientsAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                var clients = await _database.Table<Client>().ToListAsync();

                foreach (var client in clients)
                {
                    client.Jobs = await GetJobsByClientIdAsync(client.Id);
                }

                _logger.LogDebug($"Retrieved {clients.Count} clients with jobs");
                return clients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching clients");
                return new List<Client>();
            }
        }

        public async Task<List<Client>> GetClientsWithoutJobsAsync(int skip = 0, int take = 10)
        {
            try
            {
                await EnsureInitializedAsync();

                var clients = await _database.Table<Client>()
                    .OrderBy(c => c.Name)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                _logger.LogDebug($"Retrieved {clients.Count} clients (without jobs)");
                return clients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching clients without jobs");
                throw;
            }
        }

        public async Task<Client?> GetLastClientAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                var lastClient = await _database.Table<Client>()
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                if (lastClient != null)
                {
                    lastClient.Jobs = await GetJobsByClientIdAsync(lastClient.Id);
                }

                _logger.LogDebug($"Retrieved last client: {lastClient?.Id ?? -1}");
                return lastClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last client");
                throw;
            }
        }

        public async Task<int> GetTotalClientCountAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                return await _database.Table<Client>().CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total client count");
                return 0;
            }
        }

        #endregion

        #region Job Operations

        public async Task InsertJobAsync(Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            try
            {
                await EnsureInitializedAsync();

                job.CreatedAt = DateTime.Now;
                job.UpdatedAt = DateTime.Now;

                await _database.InsertAsync(job);
                _logger.LogInformation($"Inserted job with ID: {job.Id} for client: {job.ClientId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting job");
                throw;
            }
        }

        public async Task SaveJobsAsync(List<Job> jobs)
        {
            if (jobs == null || jobs.Count == 0)
            {
                _logger.LogWarning("Null/empty job list received");
                return;
            }

            try
            {
                await EnsureInitializedAsync();

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

                var now = DateTime.Now;
                foreach (var job in jobs)
                {
                    if (job.Id == 0)
                        job.CreatedAt = now;
                    job.UpdatedAt = now;
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

        public async Task<bool> UpdateJobAsync(Job job)
        {
            if (job == null)
            {
                _logger.LogWarning("Update failed: Job object is null");
                return false;
            }

            try
            {
                await EnsureInitializedAsync();

                job.UpdatedAt = DateTime.Now;
                int rowsAffected = await _database.UpdateAsync(job);

                if (rowsAffected == 1)
                {
                    _logger.LogInformation("Job with ID {JobId} updated successfully", job.Id);
                    return true;
                }

                var exists = await JobExistsAsync(job.Id);
                _logger.LogWarning(exists ?
                    $"Update affected {rowsAffected} rows (expected 1) for job ID {job.Id}" :
                    $"Job with ID {job.Id} not found");

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

        public async Task<int> DeleteJobAsync(Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            try
            {
                await EnsureInitializedAsync();

                var existingJob = await GetJobByIdAsync(job.Id);
                if (existingJob == null)
                {
                    _logger.LogWarning($"No job found with ID: {job.Id}");
                    return 0;
                }

                var result = await _database.DeleteAsync(job);

                if (result > 0)
                {
                    _logger.LogInformation($"Successfully deleted job with ID: {job.Id}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting job with ID: {job.Id}");
                throw;
            }
        }

        public async Task<Job?> GetJobByIdAsync(int jobId)
        {
            try
            {
                await EnsureInitializedAsync();

                var job = await _database.Table<Job>()
                    .FirstOrDefaultAsync(j => j.Id == jobId);

                _logger.LogDebug(job != null
                    ? $"Retrieved job with ID: {jobId}"
                    : $"Job with ID: {jobId} not found");

                return job;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching job with ID: {jobId}");
                throw;
            }
        }

        public async Task<List<Job>> GetJobsByClientIdAsync(int clientId)
        {
            try
            {
                await EnsureInitializedAsync();

                var jobs = await _database.Table<Job>()
                    .Where(j => j.ClientId == clientId)
                    .ToListAsync();

                _logger.LogDebug($"Fetched {jobs.Count} jobs for client ID {clientId}");
                return jobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching jobs for client ID {clientId}");
                return new List<Job>();
            }
        }

        #endregion

        #region Helper Methods

        private async Task<bool> ClientExistsAsync(int clientId)
        {
            try
            {
                return await _database.Table<Client>()
                    .Where(c => c.Id == clientId)
                    .CountAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if client exists: {clientId}");
                return false;
            }
        }

        private async Task<bool> JobExistsAsync(int jobId)
        {
            try
            {
                return await _database.Table<Job>()
                    .Where(j => j.Id == jobId)
                    .CountAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if job exists: {jobId}");
                return false;
            }
        }

        private string NormalizePhone(string phone)
        {
            return new string(phone?.Where(char.IsDigit).ToArray()) ?? string.Empty;
        }

        #endregion

        #region IDisposable (if needed)

        public void Dispose()
        {
            _database?.CloseAsync()?.Wait();
            _initLock?.Dispose();
        }

        #endregion
    }
}

