using Microsoft.Extensions.Logging;
using ResearchApp.Exceptions;
using ResearchApp.Models;
using ResearchApp.StaticClasses;
using ResearchApp.ViewModels;
using SQLite;
using SQLiteNetExtensions.Extensions;       
using SQLiteNetExtensionsAsync.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ResearchApp.DataStorage
{
    public class EmployeeDatabaseService : IEmployeeDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<EmployeeDatabaseService> _logger;
        private readonly IWorkRecordDatabaseService _workRecordService;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        public EmployeeDatabaseService(
            string databasePath,
            ILogger<EmployeeDatabaseService> logger,
            IWorkRecordDatabaseService workRecordService = null)
        {
            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);
            _logger = logger;
            _workRecordService = workRecordService;
        }
        public SQLiteConnection GetConnection()
        {
            return _database.GetConnection();
        }
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogInformation("Initializing database tables...");

                    await CreateTablesAsync();
                    await CreateIndexesAsync();
                    await VerifyTablesExist();

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
                // Create tables with proper error handling
                await _database.CreateTableAsync<Employee>();
                _logger.LogDebug("Employee table created successfully");

                await _database.CreateTableAsync<WorkRecord>();
                _logger.LogDebug("WorkRecords table created successfully");

                await _database.CreateTableAsync<EmployeeWorkRecord>();
                _logger.LogDebug("EmployeeWorkRecords table created successfully");
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "SQLite error creating tables in database");
                throw new DatabaseInitializationException("Failed to create database tables", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during table creation");
                throw new DatabaseInitializationException("Unexpected error during table creation", ex);
            }
        }

        private async Task VerifyTablesExist()
        {
            var requiredTables = new Dictionary<Type, string>
            {
                { typeof(Employee), "Employees" },
                { typeof(WorkRecord), "WorkRecords" },
                { typeof(EmployeeWorkRecord), "EmployeeWorkRecords" }
            };

            foreach (var table in requiredTables)
            {
                bool tableExists = await CheckTableExists(table.Key, table.Value);
                if (!tableExists)
                {
                    throw new DatabaseInitializationException($"Required table for {table.Key.Name} was not created successfully", null);
                }
                _logger.LogDebug($"Verified table exists for {table.Key.Name}");
            }
        }

        private async Task<bool> CheckTableExists(Type entityType, string expectedTableName)
        {
            try
            {
                // Get the actual table name from SQLite-Net mapping
                var mapping = _database.GetConnection().GetMapping(entityType);
                string actualTableName = mapping.TableName;

                var tableInfo = await _database.GetTableInfoAsync(actualTableName);
                if (tableInfo?.Any() == true)
                {
                    _logger.LogDebug($"Table {actualTableName} exists for {entityType.Name}");
                    return true;
                }

                // Fallback: Check sqlite_master directly
                var tables = await _database.QueryAsync<TableInfo>(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name = ?",
                    actualTableName);

                return tables?.Any() == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if table exists for type: {EntityType}", entityType.Name);
                return false;
            }
        }

        private async Task CreateIndexesAsync()
        {
            try
            {
                var indexCommands = new List<string>();

                // Get actual table names from mappings
                var employeeWorkRecordMapping = _database.GetConnection().GetMapping(typeof(EmployeeWorkRecord));
                var workRecordMapping = _database.GetConnection().GetMapping(typeof(WorkRecord));

              

                // EmployeeWorkRecord indexes for foreign keys and common queries
                // Note: Changed AddedDate to AddedDateTicks since that's the actual column name
                indexCommands.AddRange(new[]
                {
            $"CREATE INDEX IF NOT EXISTS idx_employeeworkrecord_employeeid ON [{employeeWorkRecordMapping.TableName}](EmployeeId)",
            $"CREATE INDEX IF NOT EXISTS idx_employeeworkrecord_workrecordid ON [{employeeWorkRecordMapping.TableName}](WorkRecordId)",
            $"CREATE INDEX IF NOT EXISTS idx_employeeworkrecord_addeddateticks ON [{employeeWorkRecordMapping.TableName}](AddedDateTicks)"
        });

                // WorkRecord indexes (assuming common fields)
                indexCommands.AddRange(new[]
                {
            $"CREATE INDEX IF NOT EXISTS idx_workrecord_createdat ON [{workRecordMapping.TableName}](CreatedAt)",
            $"CREATE INDEX IF NOT EXISTS idx_workrecord_updatedat ON [{workRecordMapping.TableName}](UpdatedAt)"

        });

                // Execute index commands
                int successCount = 0;
                foreach (var command in indexCommands)
                {
                    try
                    {
                        await _database.ExecuteAsync(command);
                        successCount++;
                        _logger.LogDebug($"Created index: {command}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to create index with command: {command}");
                    }
                }

                _logger.LogInformation($"Index creation completed. Created {successCount}/{indexCommands.Count} indexes.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during index creation process");
            }
        }

        private class TableInfo
        {
            public string Name { get; set; }
        }

        #region Employee CRUD Operations

        public async Task<Employee> SaveEmployeeAsync(Employee employee)
        {
            await InitializeAsync();
            if (employee == null)
            {
                _logger?.LogWarning("Save failed: Employee object is null");
                return null;
            }

            try
            {
                // Set audit fields
                if (employee.Id == 0)
                {
                    employee.CreatedAt = DateTime.Now;
                    employee.UpdatedAt = null;
                    await _database.InsertAsync(employee);
                    _logger?.LogInformation("Employee '{Name}' with ID {EmployeeId} added successfully", employee.Name, employee.Id);
                }
                else
                {
                    employee.UpdatedAt = DateTime.Now;
                    await _database.UpdateAsync(employee);
                    _logger?.LogInformation("Employee '{Name}' with ID {EmployeeId} updated successfully", employee.Name, employee.Id);
                }
                return employee;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Save failed - constraint violation for employee '{Name}' ID {EmployeeId}", employee.Name, employee.Id);
                throw new DataConstraintException("Save failed - possible duplicate phone number or other constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving employee '{Name}' with ID {EmployeeId}", employee.Name, employee.Id);
                throw new DatabaseOperationException($"Failed to save employee '{employee.Name}' with ID {employee.Id}", ex);
            }
        }

        public async Task AddEmployeeAsync(Employee employee)
        {
            await InitializeAsync();
            if (employee == null)
            {
                _logger?.LogWarning("Add failed: Employee object is null");
                return;
            }

            try
            {
                employee.CreatedAt = DateTime.Now;
                employee.UpdatedAt = null;
                employee.Id = 0; // Ensure it's treated as new

                await _database.InsertAsync(employee);
                _logger?.LogInformation("Employee '{Name}' with ID {EmployeeId} added successfully", employee.Name, employee.Id);
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Add failed - constraint violation for employee '{Name}'", employee.Name);
                throw new DataConstraintException("Add failed - possible duplicate phone number or other constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error adding employee '{Name}'", employee.Name);
                throw new DatabaseOperationException($"Failed to add employee '{employee.Name}'", ex);
            }
        }

        public async Task<bool> UpdateEmployeeAsync(Employee employee)
        {
            await InitializeAsync();

            if (employee == null)
            {
                _logger?.LogWarning("Update failed: Employee object is null");
                return false;
            }

            try
            {
                employee.UpdatedAt = DateTime.Now;
                int rowsAffected = await _database.UpdateAsync(employee);

                if (rowsAffected == 1)
                {
                    _logger?.LogInformation("Employee '{Name}' with ID {EmployeeId} updated successfully", employee.Name, employee.Id);
                    return true;
                }

                // Check if employee exists
                var existingEmployee = await _database.Table<Employee>()
                    .Where(e => e.Id == employee.Id)
                    .CountAsync() > 0;

                _logger?.LogWarning(existingEmployee ?
                    $"Update affected {rowsAffected} rows (expected 1) for employee '{employee.Name}' ID {employee.Id}" :
                    $"Employee '{employee.Name}' with ID {employee.Id} not found");

                return false;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Update failed - constraint violation for employee '{Name}' ID {EmployeeId}", employee.Name, employee.Id);
                throw new DataConstraintException("Update failed - possible duplicate phone number or other constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating employee '{Name}' with ID {EmployeeId}", employee.Name, employee.Id);
                throw new DatabaseOperationException($"Failed to update employee '{employee.Name}' with ID {employee.Id}", ex);
            }
        }
        public async Task<List<T>> GetAllAsync<T>(bool loadChildren = false) where T : IEntity,new()
        {
            await InitializeAsync();
            try
            {
                if (loadChildren)
                {
                    return await _database.GetAllWithChildrenAsync<T>(recursive: true);
                }
                else
                {
                    return await _database.Table<T>().ToListAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving all entities of type {typeof(T).Name}");
                throw new DatabaseOperationException($"Failed to retrieve all {typeof(T).Name} entities", ex);
            }
        }
        public async Task<int> DeleteEmployeeAsync(Employee employee)
        {
            await InitializeAsync();
            if (employee == null)
            {
                _logger?.LogWarning("Delete failed: Employee object is null");
                return 0;
            }

            try
            {
                int rowsAffected = await _database.DeleteAsync(employee);
                if (rowsAffected > 0)
                {
                    _logger?.LogInformation("Employee '{Name}' with ID {EmployeeId} deleted successfully", employee.Name, employee.Id);
                }
                else
                {
                    _logger?.LogWarning("No employee found with ID {EmployeeId} to delete", employee.Id);
                }
                return rowsAffected;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Delete failed - constraint violation for employee '{Name}' ID {EmployeeId}", employee.Name, employee.Id);
                throw new DataConstraintException("Delete failed - possible foreign key constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting employee '{Name}' with ID {EmployeeId}", employee.Name, employee.Id);
                throw new DatabaseOperationException($"Failed to delete employee '{employee.Name}' with ID {employee.Id}", ex);
            }
        }

        #endregion

        #region Employee Queries

        public async Task<Employee> GetEmployeeByIdAsync(int id)
        {
            await InitializeAsync();
            try
            {
                return await _database.Table<Employee>()
                                      .Where(e => e.Id == id)
                                      .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee with ID {EmployeeId}", id);
                return null;
            }
        }

        public async Task<List<Employee>> GetEmployeesAsync(int skip = 0, int take = 20)
        {
            await InitializeAsync();
            try
            {
                return await _database.Table<Employee>()
                           .OrderBy(e => e.Name)
                           .Skip(skip).Take(take)
                           .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employees (skip: {Skip}, take: {Take})", skip, take);
                return new List<Employee>();
            }
        }
        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            await InitializeAsync();
            try
            {
                return await _database.Table<Employee>()
                           .OrderBy(e => e.Name)
                           .ToListAsync();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all employees");
                return new List<Employee>();
            }
        }

        public async Task<int> GetTotalEmployeeCountAsync()
        {
            await InitializeAsync();

            try
            {
                return await _database.Table<Employee>().CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching total employee count");
                return 0;
            }
        }

        public class EmployeeRecords
        {
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; }
            public List<int> WorkRecordIds { get; set; } = new List<int>();
        }

        public async Task<List<EmployeeRecords>> GetEmployeeWorkRecordMappingsStructuredAsync(string period = "Current Week")
        {
            await InitializeAsync();
            try
            {
                // Get date range based on period parameter
                var (startDate, endDate) = GetDateRange(period);
                var startDateTicks = startDate.Ticks; 
                var endDateTicks = endDate.Ticks;
            

                var relationships = await _database.Table<EmployeeWorkRecord>()
                    .Where(ewr => ewr.AddedDateTicks >= startDateTicks && ewr.AddedDateTicks <= endDateTicks)
                    .ToListAsync();

                var employeeIds = relationships.Select(ewr => ewr.EmployeeId).Distinct().ToList();
                var employees = await _database.Table<Employee>()
                    .Where(e => employeeIds.Contains(e.Id))
                    .ToListAsync();

                var result = relationships
                    .GroupBy(ewr => ewr.EmployeeId)
                    .Select(g => new EmployeeRecords
                    {
                        EmployeeId = g.Key,
                        EmployeeName = employees.FirstOrDefault(e => e.Id == g.Key)?.Name ?? "Unknown",
                        WorkRecordIds = g.Select(ewr => ewr.WorkRecordId).ToList()
                    })
                    .ToList();

                _logger.LogInformation($"Retrieved structured work records for {result.Count} employees for period: {period}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving structured employee work records");
                throw new DatabaseOperationException("Failed to retrieve structured employee work records", ex);
            }
        }
        public async Task<List<int>> GetEmployeeWorkRecordsIdsAsync(int employeeId, string period = "Current Week")
        {
            if (employeeId <= 0)
            {
                _logger.LogWarning("Invalid employee ID: {EmployeeId}", employeeId);
                return new List<int>();
            }
            await InitializeAsync();
            try
            {
                // Get date range based on period parameter
                var (startDate, endDate) = GetDateRange(period);
                var startDateTicks = startDate.Ticks;
                var endDateTicks = endDate.Ticks;

                // Get EmployeeWorkRecord entities for the specified employee and period
                var employeeWorkRecords = await _database.Table<EmployeeWorkRecord>()
                    .Where(ewr => ewr.EmployeeId == employeeId &&
                                 ewr.AddedDateTicks >= startDateTicks &&
                                 ewr.AddedDateTicks <= endDateTicks)
                    .ToListAsync();

                // Extract the WorkRecord IDs (not EmployeeWorkRecord IDs)
                var workRecordIds = employeeWorkRecords.Select(ewr => ewr.WorkRecordId).ToList();

                _logger.LogDebug("Retrieved {Count} work record IDs for Employee {EmployeeId} in period {Period}",
                                 workRecordIds.Count, employeeId, period);

                return workRecordIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work record IDs for Employee {EmployeeId} in period {Period}",
                                 employeeId, period);
                return new List<int>();
            }
        }
        public async Task<List<Employee>> GetEmployeesPerPeriodAsync(bool includeDetails = false, CancellationToken cancellationToken = default, 
           string period = "Current Week"){
            await InitializeAsync();
            if (_database == null) throw new InvalidOperationException("Database not initialized");
            try
            {
                var (startDate, endDate) = GetDateRange(period);
                var startDateTicks = startDate.Ticks;
                var endDateTicks = endDate.Ticks;
                Debug.WriteLine($"Fetching active employees between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}");
                // Single query to get distinct employee IDs
                var activeEmployeeIds = await _database.Table<EmployeeWorkRecord>()
                    .Where(ewr => ewr.AddedDateTicks >= startDateTicks && ewr.AddedDateTicks <= endDateTicks)
                    .ToListAsync();

                var distinctEmployeeIds = activeEmployeeIds
                    .Select(ewr => ewr.EmployeeId)
                    .Distinct()
                    .ToList();
                
                Debug.WriteLine($"Found {activeEmployeeIds.Count} active employees");
                if (activeEmployeeIds.Count == 0)
                    return new List<Employee>();

                // Fetch employees (with or without details)
                return includeDetails
                    ? await _database.GetAllWithChildrenAsync<Employee>(
                        e => distinctEmployeeIds.Contains(e.Id),
                        recursive: true,
                        cancellationToken: cancellationToken)
                    : await _database.Table<Employee>()
                    .Where(e => distinctEmployeeIds.Contains(e.Id))
                    .OrderBy(e => e.Name)
                    .ToListAsync();

            
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "Error fetching active employees for period: {Period}", period);
        throw new DatabaseOperationException("Failed to retrieve active employees", ex);
    }
}
        #endregion

        #region EmployeeWorkRecord Operations
        public async Task<int> SaveEmployeeWorkRecordAsync(EmployeeWorkRecord record, SQLiteConnection transaction = null) { 
        
            await InitializeAsync();
            if (record == null)
            {
                _logger.LogWarning("Save failed: EmployeeWorkRecord object is null");
                return 0;
            }

            try
            {
                if (transaction != null)
                {
                    if (record.Id == 0)
                    {
                        if (record.AddedDate == default)
                            record.AddedDateTicks = DateTime.Now.ToBinary();

                        var result = transaction.Insert(record);
                        _logger.LogDebug($"EmployeeWorkRecord inserted within existing transaction with ID: {record.Id}");
                        return result;
                    }
                    else
                    {
                        return transaction.Update(record);
                    }
                }
                // Otherwise use the TransactionCoordinator
                var coordinator = new TransactionCoordinator(_database, _logger as ILogger<TransactionCoordinator>);
                return await coordinator.ExecuteInTransactionAsync(async () =>
                {
                    if (record.Id == 0)
                    {
                        if (record.AddedDate == default)
                            record.AddedDateTicks = DateTime.Now.ToBinary();

                        var result = await _database.InsertAsync(record);
                        _logger.LogDebug($"EmployeeWorkRecord inserted with new transaction with ID: {record.Id}");
                        return result;
                    }
                    else
                    {
                        return await _database.UpdateAsync(record);
                    }
                });
            
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger.LogError(ex, "Save failed - constraint violation for employee work record ID {RecordId}", record.Id);
                throw new DataConstraintException("Save failed - possible foreign key constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving employee work record with ID {RecordId}", record.Id);
                throw new DatabaseOperationException($"Failed to save employee work record with ID {record.Id}", ex);
            }
        }


        public async Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                _logger.LogWarning("Invalid employee ID: {EmployeeId}", employeeId);
                return new List<EmployeeWorkRecord>();
            }

            await InitializeAsync();

            try
            {
                var records = await _database.Table<EmployeeWorkRecord>()
                                              .Where(ew => ew.EmployeeId == employeeId)
                                              .OrderByDescending(ew => ew.AddedDateTicks)
                                              .ToListAsync();

                _logger.LogDebug("Retrieved {Count} work records for Employee {EmployeeId}",
                                 records.Count, employeeId);

                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work records for Employee {EmployeeId}", employeeId);
                return new List<EmployeeWorkRecord>();
            }
        }

        public async Task<List<EmployeeWorkRecord>> GetAllEmployeeWorkRecordsAsync()
        {
            await InitializeAsync();
            try
            {
                return await _database.Table<EmployeeWorkRecord>()
                                     .OrderByDescending(ew => ew.AddedDate)
                                     .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all employee work records");
                return new List<EmployeeWorkRecord>();
            }
        }
         
        public async Task<List<WorkRecord>> GetWorkRecordsForEmployeeAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                _logger.LogWarning("Invalid employee ID: {EmployeeId}", employeeId);
                return new List<WorkRecord>();
            }

            await InitializeAsync();

            try
            {
                // Get employee work records first
                var employeeWorkRecords = await _database.Table<EmployeeWorkRecord>()
                                                          .Where(ew => ew.EmployeeId == employeeId)
                                                          .ToListAsync();

                var workRecordIds = employeeWorkRecords.Select(ew => ew.WorkRecordId).Distinct().ToList();

                if (!workRecordIds.Any())
                {
                    return new List<WorkRecord>();
                }

   
                // Get work records by IDs
                var workRecords = await _database.Table<WorkRecord>()
                                                 .Where(wr => workRecordIds.Contains(wr.Id))
                                                 .OrderByDescending(wr => wr.CreatedAt)
                                                 .ToListAsync();

                return workRecords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work records for Employee {EmployeeId}", employeeId);
                return new List<WorkRecord>();
            }
        }

        #endregion

        #region Utility Methods

        public async Task<bool> VerifyTableCreated<T>()
        {
            try
            {
                var mapping = _database.GetConnection().GetMapping(typeof(T));
                var tableInfo = await _database.GetTableInfoAsync(mapping.TableName);
                return tableInfo?.Any() == true;
            }
            catch
            {
                return false;
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

        #region Disposal

        public void Dispose()
        {
            _database?.CloseAsync()?.Wait();
            _initLock?.Dispose();
        }

        #endregion
    }
}