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
    public interface IEntity
    {
        int Id { get; set; }
    }

    public interface IAuditable
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }

    public class WorkRecordDatabaseService : IWorkRecordDatabaseService, IDisposable
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<WorkRecordDatabaseService> _logger;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        public WorkRecordDatabaseService(string databasePath, ILogger<WorkRecordDatabaseService> logger)
        {
            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);
            _logger = logger;
        }

        #region Initialization and Core Methods

        public async Task InitializeAsync()
        {
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

        private async Task CreateTablesAsync()
        {
            try
            {
                // Create tables in the correct order (respecting foreign key dependencies)
                _logger.LogDebug("Creating Client table...");
                await _database.CreateTableAsync<Client>();

                _logger.LogDebug("Creating Employee table...");
                await _database.CreateTableAsync<Employee>();

                _logger.LogDebug("Creating Job table...");
                await _database.CreateTableAsync<Job>();

                _logger.LogDebug("Creating WorkRecord table...");
                await _database.CreateTableAsync<WorkRecord>();

                _logger.LogDebug("Creating EmployeeWorkRecord table...");
                await _database.CreateTableAsync<EmployeeWorkRecord>();

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
                // Get the actual table names from the database with better error handling
                var tables = await _database.QueryAsync<dynamic>("SELECT name FROM sqlite_master WHERE type='table'");
                var tableNames = new List<string>();

                if (tables?.Any() == true)
                {
                    foreach (var table in tables)
                    {
                        if (table?.name != null)
                        {
                            var tableName = table.name.ToString();
                            if (!string.IsNullOrWhiteSpace(tableName))
                            {
                                tableNames.Add(tableName);
                            }
                        }
                    }
                }

                _logger.LogInformation($"Available tables: {string.Join(", ", tableNames)}");

                // Create indexes with proper table names (check both singular and plural)
                var indexCommands = new List<string>();

                // Client indexes
                var clientTable = FindTableName(tableNames, "Client");
                if (!string.IsNullOrEmpty(clientTable))
                {
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_client_name ON [{clientTable}](Name)");
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_client_phone ON [{clientTable}](Phone)");
                }

                // Employee indexes
                var employeeTable = FindTableName(tableNames, "Employee", excludePatterns: new[] { "Payment", "Work" });
                if (!string.IsNullOrEmpty(employeeTable))
                {
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_employee_name ON [{employeeTable}](Name)");
                }

                // Job indexes
                var jobTable = FindTableName(tableNames, "Job", excludePatterns: new[] { "Work" });
                if (!string.IsNullOrEmpty(jobTable))
                {
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_job_name ON [{jobTable}](JobName)");
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_job_clientid ON [{jobTable}](ClientId)");
                }

                // WorkRecord indexes
                var workRecordTable = FindTableName(tableNames, "WorkRecord", excludePatterns: new[] { "Employee" });
                if (!string.IsNullOrEmpty(workRecordTable))
                {
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_workrecord_clientid ON [{workRecordTable}](ClientId)");
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_workrecord_jobid ON [{workRecordTable}](JobId)");
                    indexCommands.Add($"CREATE INDEX IF NOT EXISTS idx_workrecord_date ON [{workRecordTable}](WorkDate)");
                }

                // Execute index commands
                foreach (var command in indexCommands)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            await _database.ExecuteAsync(command);
                            _logger.LogDebug($"Created index: {command}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to create index with command: {command}");
                        // Continue with other indexes even if one fails
                    }
                }

                _logger.LogInformation($"Index creation completed. Created {indexCommands.Count} indexes.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index creation failed");
                // Don't throw here - indexes are not critical for basic functionality
            }
        }

        private string FindTableName(List<string> tableNames, string baseTableName, string[] excludePatterns = null)
        {
            if (tableNames == null || !tableNames.Any())
                return null;

            // First try exact match
            var exactMatch = tableNames.FirstOrDefault(t =>
                string.Equals(t, baseTableName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exactMatch))
            {
                if (excludePatterns == null || !excludePatterns.Any(pattern =>
                    exactMatch.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    return exactMatch;
                }
            }

            // Try plural form
            var pluralMatch = tableNames.FirstOrDefault(t =>
                string.Equals(t, baseTableName + "s", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(pluralMatch))
            {
                if (excludePatterns == null || !excludePatterns.Any(pattern =>
                    pluralMatch.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    return pluralMatch;
                }
            }

            // Try case-insensitive contains match
            var containsMatch = tableNames.FirstOrDefault(t =>
                t.Contains(baseTableName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(containsMatch))
            {
                if (excludePatterns == null || !excludePatterns.Any(pattern =>
                    containsMatch.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    return containsMatch;
                }
            }

            return null;
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        #endregion

        #region Generic CRUD Operations

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

        public async Task<int> UpdateWorkRecord(WorkRecord record)
        {
            // Validation
            if (record == null)
            {
                _logger.LogError("Attempted to update a null work record");
                throw new ArgumentNullException(nameof(record));
            }

            if (record.Id <= 0)
            {
                _logger.LogError($"Attempted to update work record with invalid ID: {record.Id}");
                throw new ArgumentException("Work record must have a valid ID to be updated");
            }

            await EnsureInitializedAsync();

            try
            {
                // Business logic for status changes
                if (record.IsJobCompleted && record.CompletedDate == null)
                {
                    record.CompletedDate = DateTime.UtcNow;
                    _logger.LogDebug($"Set CompletedDate for work record {record.Id}");
                }

                if (record.IsPaid && record.PaidDate == null)
                {
                    record.PaidDate = DateTime.UtcNow;
                    _logger.LogDebug($"Set PaidDate for work record {record.Id}");
                }

                // Update timestamp
                record.UpdatedAt = DateTime.UtcNow;

                // Perform update
                var result = await UpdateAsync(record);

                if (result == 1)
                {
                    _logger.LogInformation($"Successfully updated work record ID: {record.Id}");
                }
                else
                {
                    _logger.LogWarning($"No rows affected when updating work record ID: {record.Id}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating work record ID: {record.Id}");
                throw;
            }
        }

        public async Task<int> DeleteWorkRecordAsync(WorkRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            return await DeleteAsync(record);
        }

        #endregion

        #region Generic Query Methods

        public async Task<List<T>> GetFilteredAsync<T>(
            Expression<Func<T, bool>> predicate = null,
            Func<AsyncTableQuery<T>, AsyncTableQuery<T>> queryModifier = null)
            where T : new()
        {
            await EnsureInitializedAsync();
            var query = _database.Table<T>();

            if (predicate != null)
                query = query.Where(predicate);

            if (queryModifier != null)
                query = queryModifier(query);

            return await query.ToListAsync();
        }

        public async Task<int> GetCountAsync<T>(
            Expression<Func<T, bool>> predicate = null)
            where T : new()
        {
            await EnsureInitializedAsync();
            var query = _database.Table<T>();

            return predicate != null
                ? await query.CountAsync(predicate)
                : await query.CountAsync();
        }

        public async Task ResetDatabaseAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                _logger.LogInformation("Beginning database reset operation...");

                // First, get all existing table names
                var existingTables = await _database.QueryAsync<dynamic>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'");
                var tableNames = existingTables.Select(t => t.name.ToString()).ToList();

                _logger.LogInformation($"Found existing tables: {string.Join(", ", tableNames)}");

                // Drop all existing tables
                foreach (var tableName in tableNames)
                {
                    try
                    {
                        await _database.ExecuteAsync($"DROP TABLE IF EXISTS {tableName}");
                        _logger.LogDebug($"Dropped table: {tableName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to drop table: {tableName}");
                    }
                }

                // Mark as uninitialized to force recreation
                _isInitialized = false;

                // Recreate all tables
                await CreateTablesAsync();
                await CreateIndexesAsync();

                _isInitialized = true;
                _logger.LogInformation("Database reset completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database reset failed");
                _isInitialized = false;
                throw new DatabaseOperationException("Failed to reset database", ex);
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task DropTableIfExistsAsync<T>() where T : new()
        {
            try
            {
                var tableName = typeof(T).Name;
                await _database.ExecuteAsync($"DROP TABLE IF EXISTS {tableName}");
                _logger.LogDebug($"Dropped table: {tableName}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error dropping table {typeof(T).Name}");
                throw;
            }
        }

        #endregion

        #region WorkRecord Specific Methods

        public async Task<List<EmployeeWorkRecord>> GetWorkRecordEmployeesAsync(int workRecordId)
        {
            if (workRecordId <= 0)
            {
                _logger.LogWarning("Invalid workRecordId: {WorkRecordId}", workRecordId);
                return new List<EmployeeWorkRecord>();
            }

            await EnsureInitializedAsync();

            try
            {
                return await _database.Table<EmployeeWorkRecord>()
                    .Where(ew => ew.WorkRecordId == workRecordId)
                    .OrderBy(ew => ew.EmployeeId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employees for work record {WorkRecordId}", workRecordId);
                return new List<EmployeeWorkRecord>();
            }
        }

        public async Task<List<WorkRecord>> GetWorkRecordsForEmployeeAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            await EnsureInitializedAsync();

            if (employeeId <= 0)
            {
                _logger.LogWarning("Invalid employeeId: {EmployeeId}", employeeId);
                return new List<WorkRecord>();
            }

            try
            {
                // Get all EmployeeWorkRecord entries for this employee
                var joinRecords = await _database.Table<EmployeeWorkRecord>()
                    .Where(ew => ew.EmployeeId == employeeId)
                    .ToListAsync();

                if (!joinRecords.Any()) return new List<WorkRecord>();

                // Get the corresponding WorkRecords within date range
                var workRecordIds = joinRecords.Select(j => j.WorkRecordId).ToList();

                return await _database.Table<WorkRecord>()
                    .Where(wr => workRecordIds.Contains(wr.Id) &&
                                 wr.WorkDate >= startDate &&
                                 wr.WorkDate <= endDate)
                    .OrderByDescending(wr => wr.WorkDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work records for employee {EmployeeId} from {StartDate} to {EndDate}", employeeId, startDate, endDate);
                return new List<WorkRecord>();
            }
        }

        public async Task<List<WorkRecord>> GetWorkRecordsAsync(
            int? clientId = null,
            int? jobId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            bool? isCompleted = null,
            bool? isPaid = null,
            bool? isPaymentProcessed = null)
        {
            await EnsureInitializedAsync();

            var query = _database.Table<WorkRecord>();

            if (clientId.HasValue)
                query = query.Where(w => w.ClientId == clientId.Value);

            if (jobId.HasValue)
                query = query.Where(w => w.JobId == jobId.Value);

            if (startDate.HasValue)
                query = query.Where(w => w.WorkDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(w => w.WorkDate <= endDate.Value);

            if (isCompleted.HasValue)
                query = query.Where(w => w.IsJobCompleted == isCompleted.Value);

            if (isPaid.HasValue)
                query = query.Where(w => w.IsPaid == isPaid.Value);

            if (isPaymentProcessed.HasValue)
                query = query.Where(w => w.IsPaymentProcessed == isPaymentProcessed.Value);

            return await query.ToListAsync();
        }

        public async Task<List<WorkRecord>> GetWorkRecordsByPeriodAsync(
            string period = "Current Week",
            bool? isCompleted = null,
            bool? isPaymentProcessed = null,
            DateTime? customStartDate = null,
            DateTime? customEndDate = null)
        {
            var (startDate, endDate) = GetDateRange(period);

            return await GetWorkRecordsAsync(
                startDate: customStartDate ?? startDate,
                endDate: customEndDate ?? endDate,
                isCompleted: isCompleted,
                isPaymentProcessed: isPaymentProcessed
            );
        }

        public async Task<WorkRecord> GetWorkRecordWithDetailsAsync(int id)
        {
            var record = await GetByIdAsync<WorkRecord>(id);
            if (record != null)
            {
                await LoadRelatedEntities(record);
            }
            return record;
        }

        public async Task<int> SaveWorkRecordAsync(WorkRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            if (record.IsJobCompleted && record.CompletedDate == null)
                record.CompletedDate = DateTime.UtcNow;

            if (record.IsPaid && record.PaidDate == null)
                record.PaidDate = DateTime.UtcNow;

            return await SaveAsync(record);
        }

        public async Task<decimal> GetCompletedJobsTotalAccumulatedRevenueAsync(
            string period = "Current Week",
            DateTime? customStartDate = null,
            DateTime? customEndDate = null)
        {
            var (startDate, endDate) = GetDateRange(period);
            startDate = customStartDate ?? startDate;
            endDate = customEndDate ?? endDate;

            try
            {
                string sql = @"
                    SELECT SUM(TotalAmount) as Total
                    FROM WorkRecords 
                    WHERE IsJobCompleted = 1 
                    AND CompletedDate >= ? AND CompletedDate <= ?
                    AND IsPaymentProcessed = 0";

                var result = await _database.ExecuteScalarAsync<decimal?>(sql, startDate, endDate);
                return result ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total accumulated revenue");
                return 0; // Return 0 instead of throwing
            }
        }

        public async Task<int> GetUnprocessedCompletedJobsCountAsync(string period = "Current Week",
            DateTime? customStartDate = null, DateTime? customEndDate = null)
        {
            await EnsureInitializedAsync();

            try
            {
                var (startDate, endDate) = GetDateRange(period);
                startDate = customStartDate ?? startDate;
                endDate = customEndDate ?? endDate;

                // Count unprocessed completed jobs
                return await _database.Table<WorkRecord>()
                    .Where(r => r.IsJobCompleted &&
                               !r.IsPaymentProcessed &&
                               r.CompletedDate >= startDate &&
                               r.CompletedDate <= endDate)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting unprocessed completed jobs");
                return 0; // Return 0 instead of throwing
            }
        }

        #endregion

        #region EmployeePayment Methods

        public async Task<List<EmployeePayment>> GetEmployeePaymentsAsync(
            int? employeeId = null,
            bool? isPaid = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            await EnsureInitializedAsync();

            var query = _database.Table<EmployeePayment>();

            if (employeeId.HasValue)
                query = query.Where(p => p.EmployeeId == employeeId.Value);

            if (isPaid.HasValue)
                query = query.Where(p => p.IsPaid == isPaid.Value);

            if (startDate.HasValue)
                query = query.Where(p => p.CompletionDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(p => p.CompletionDate <= endDate.Value);

            return await query.OrderByDescending(p => p.CompletionDate).ToListAsync();
        }

        public async Task<int> SaveEmployeePaymentAsync(EmployeePayment payment)
        {
            if (payment == null)
                throw new ArgumentNullException(nameof(payment));

            return await SaveAsync(payment);
        }

        public async Task<decimal> GetEmployeeEarningsTotalAsync(
            int employeeId,
            string period = "Current Week",
            DateTime? customStartDate = null,
            DateTime? customEndDate = null)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Invalid employee ID", nameof(employeeId));

            var (startDate, endDate) = GetDateRange(period);
            startDate = customStartDate ?? startDate;
            endDate = customEndDate ?? endDate;

            try
            {
                string sql = @"
                    SELECT SUM(AmountEarned) 
                    FROM EmployeePayment 
                    WHERE EmployeeId = ?
                    AND CompletionDate >= ?
                    AND CompletionDate <= ?";

                var result = await _database.ExecuteScalarAsync<decimal?>(sql, employeeId, startDate, endDate);
                return result ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating employee earnings");
                return 0; // Return 0 instead of throwing
            }
        }

        #endregion

        #region Helper Methods

        private async Task LoadRelatedEntities(WorkRecord record)
        {
            record.Client = await GetByIdAsync<Client>(record.ClientId);
            record.Job = await GetByIdAsync<Job>(record.JobId);

            if (!string.IsNullOrEmpty(record.EmployeeIds))
            {
                var employeeIds = record.EmployeeIds.Split(',').Select(int.Parse);
                record.Employees = await GetFilteredAsync<Employee>(e => employeeIds.Contains(e.Id));
            }
        }

        private async Task LoadRelatedEntities(List<WorkRecord> records)
        {
            if (!records.Any()) return;

            var clientIds = records.Select(r => r.ClientId).Distinct();
            var clients = await GetFilteredAsync<Client>(c => clientIds.Contains(c.Id));
            var clientsDict = clients.ToDictionary(c => c.Id);

            var jobIds = records.Select(r => r.JobId).Distinct();
            var jobs = await GetFilteredAsync<Job>(j => jobIds.Contains(j.Id));
            var jobsDict = jobs.ToDictionary(j => j.Id);

            var allEmployeeIds = records
                .Where(r => !string.IsNullOrEmpty(r.EmployeeIds))
                .SelectMany(r => r.EmployeeIds.Split(',').Select(int.Parse))
                .Distinct();
            var employees = await GetFilteredAsync<Employee>(e => allEmployeeIds.Contains(e.Id));
            var employeesDict = employees.ToDictionary(e => e.Id);

            foreach (var record in records)
            {
                if (clientsDict.TryGetValue(record.ClientId, out var client))
                    record.Client = client;

                if (jobsDict.TryGetValue(record.JobId, out var job))
                    record.Job = job;

                if (!string.IsNullOrEmpty(record.EmployeeIds))
                {
                    record.Employees = record.EmployeeIds.Split(',')
                        .Select(int.Parse)
                        .Where(employeesDict.ContainsKey)
                        .Select(id => employeesDict[id])
                        .ToList();
                }
            }
        }

        public (DateTime startDate, DateTime endDate) GetDateRange(string period)
        {
            DateTime startDate;
            DateTime endDate = DateTime.Today.EndOfDay();

            switch (period.ToLower())
            {
                case "current week":
                    startDate = DateTime.Today.StartOfWeek(DayOfWeek.Monday);
                    endDate = startDate.AddDays(6).EndOfDay();
                    break;
                case "last week":
                    startDate = DateTime.Today.AddDays(-7).StartOfWeek(DayOfWeek.Monday);
                    endDate = startDate.AddDays(6).EndOfDay();
                    break;
                case "last two weeks":
                    startDate = DateTime.Today.AddDays(-14);
                    break;
                case "last month":
                    startDate = DateTime.Today.AddMonths(-1);
                    break;
                case "last 3 months":
                    startDate = DateTime.Today.AddMonths(-3);
                    break;
                case "last 6 months":
                    startDate = DateTime.Today.AddMonths(-6);
                    break;
                case "last year":
                    startDate = DateTime.Today.AddYears(-1);
                    break;
                default:
                    startDate = DateTime.Today.StartOfWeek(DayOfWeek.Monday);
                    endDate = startDate.AddDays(6).EndOfDay();
                    break;
            }

            return (startDate, endDate);
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            _initLock?.Dispose();
            _database?.CloseAsync().GetAwaiter().GetResult();
        }

        #endregion
    }
}