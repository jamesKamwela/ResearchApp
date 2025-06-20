using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Exceptions;
using ResearchApp.Models;
using ResearchApp.StaticClasses;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ResearchApp.DataStorage
{
    public class SummeriesDatabaseService : ISummeriesDatabaseService,IDisposable
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<SummeriesDatabaseService> _logger;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        public SummeriesDatabaseService(string databasePath, ILogger<SummeriesDatabaseService> logger)
        {
            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);
            _logger = logger;
        }
        public const int DatabaseVersion = 1;



        
         public async Task InitializeAsync(){

            if (_isInitialized) return;


            await _initLock.WaitAsync();
            try
            {

                if (!_isInitialized)
                {
                    _logger.LogInformation("Initializing database tables...");

                    // Create tables directly using the types
                    await CreateTablesAsync();
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
                // Create WorkRecord first (if it has foreign keys referencing other tables)
                _logger.LogDebug("Creating WorkRecord table...");
                await _database.CreateTableAsync<WorkRecord>();

                // Create the unified Summary table
                _logger.LogDebug("Creating Summary table...");
                await _database.CreateTableAsync<Summary>();

                _logger.LogInformation("All tables created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create tables");
                throw;
            }
        }

        private async Task CreateIndexesAsync()
        {
            try
            {
                // Corrected to use "Summary" instead of "Summeries"
                // Admin view indexes
                await _database.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_summary_client_job ON Summary(ClientName, JobName)");
                await _database.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_summary_completion ON Summary(CompletedDate)");

                // Employee view indexes
                await _database.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_summary_employee_date ON Summary(EmployeeName, CompletedDate)");

                // Common filter indexes
                await _database.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_summary_created ON Summary(CreatedAt)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create indexes");
            }
        }
        
        public async Task<int> InsertAsync<T>(T entity) where T : new()
        {
            await EnsureInitializedAsync();
            if (entity is IAuditable auditable)
                auditable.CreatedAt = DateTime.UtcNow;

            return await _database.InsertAsync(entity);
        }

        public async Task<int> UpdateAsync<T>(T entity) where T : new()
        {
            await EnsureInitializedAsync();
            if (entity is IAuditable auditable)
                auditable.UpdatedAt = DateTime.UtcNow;

            return await _database.UpdateAsync(entity);
        }

        public async Task<int> SaveAsync<T>(T entity) where T : IEntity, new()
        {
            return entity.Id == 0 ? await InsertAsync(entity) : await UpdateAsync(entity);
        }

        public async Task<int> DeleteAsync<T>(T entity) where T : new()
        {
            await EnsureInitializedAsync();
            return await _database.DeleteAsync(entity);
        }

        public async Task<T> GetByIdAsync<T>(int id) where T : IEntity, new()
        {
            await EnsureInitializedAsync();
            return await _database.Table<T>().FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<T>> GetAllAsync<T>() where T : new()
        {
            await EnsureInitializedAsync();
            return await _database.Table<T>().ToListAsync();
        }
        public void Dispose()
        {
            _initLock?.Dispose();
            _database?.CloseAsync().GetAwaiter().GetResult();
        }

    }
}
