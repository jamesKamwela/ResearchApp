using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using ResearchApp.Models;
using ResearchApp.ViewModels;

using System.Diagnostics;
using System;
using ResearchApp.Exceptions;
using Microsoft.Extensions.Logging;
using Windows.Media.Protection.PlayReady;

namespace ResearchApp.DataStorage
{
    public class EmployeeDatabaseService : IEmployeeDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<EmployeeDatabaseService> _logger;
        private readonly IWorkRecordDatabaseService _workRecordService; // Add dependency
        private bool _isInitialized = false;

        public EmployeeDatabaseService(
            string databasePath,
            ILogger<EmployeeDatabaseService> logger,
            IWorkRecordDatabaseService workRecordService = null) // Make optional for now
        {
            _database = new SQLiteAsyncConnection(databasePath, Constants.Flags);
            _logger = logger;
            _workRecordService = workRecordService;
        }

        /*public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Create all tables at once
                await _database.CreateTableAsync<Employee>();
                await _database.CreateTableAsync<WorkRecord>();
                await _database.CreateTableAsync<EmployeeWorkRecord>();

                // Verify tables were created with improved checking
                await VerifyTablesExist();

                await CreateIndexesAsync();
                _isInitialized = true;
                _logger.LogInformation("Database initialized successfully with all tables");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database");
                throw new DatabaseInitializationException("Failed to initialize database", ex);
            }
        }*/

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Create tables
                await _database.CreateTableAsync<Employee>();
                await _database.CreateTableAsync<WorkRecord>();
                await _database.CreateTableAsync<EmployeeWorkRecord>();

                // Verify tables were created
                if (!await VerifyTableCreated<Employee>() ||
                    !await VerifyTableCreated<WorkRecord>() ||
                    !await VerifyTableCreated<EmployeeWorkRecord>())
                {
                    throw new DatabaseInitializationException("Failed to create one or more tables", null);
                }

                await CreateIndexesAsync();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");
                throw;
            }
        }

        private async Task VerifyTablesExist()
        {
            var requiredTables = new List<string> { "Employee", "WorkRecord", "EmployeeWorkRecord" };

            foreach (var tableName in requiredTables)
            {
                bool tableExists = await CheckTableExists(tableName);

                if (!tableExists)
                {
                    throw new DatabaseInitializationException($"Required table '{tableName}' was not created successfully", null);
                }

                _logger.LogDebug($"Verified table exists: {tableName}");
            }
        }

        private async Task<bool> CheckTableExists(string tableName)
        {
            try
            {
                // First try the exact table name
                var tableInfo = await _database.GetTableInfoAsync(tableName);
                if (tableInfo?.Any() == true)
                {
                    return true;
                }

                // Try alternative naming conventions
                var alternativeNames = new[]
                {
            tableName.ToLower(),
            tableName.ToLower() + "s",
            tableName.ToUpper(),
            tableName.ToUpper() + "S"
        };

                foreach (var name in alternativeNames)
                {
                    try
                    {
                        var info = await _database.GetTableInfoAsync(name);
                        if (info?.Any() == true)
                        {
                            _logger.LogInformation($"Found {tableName} table with name: {name}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to check table info for alternative name: {TableName}", name);
                        continue;
                    }
                }

                // Final check using sqlite_master query
                var tables = await _database.QueryAsync<dynamic>(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE ?",
                    $"%{tableName.ToLower()}%");

                if (tables?.Any() == true)
                {
                    var foundTable = tables.FirstOrDefault();
                    if (foundTable?.name != null)
                    {
                        _logger.LogInformation($"Found {tableName} table via sqlite_master: {foundTable.name}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if table exists: {TableName}", tableName);
                return false;
            }
        }
        private async Task CreateIndexesAsync()
        {
            var successfulIndexes = new List<string>();
            var failedIndexes = new List<string>();

            try
            {
                var employeeTableInfo = await _database.GetTableInfoAsync("Employee");
                if (employeeTableInfo == null || !employeeTableInfo.Any())
                {
                    _logger.LogWarning("Employee table does not exist - skipping index creation");
                    return;
                }

                /* // Get the actual table name that SQLite is using
                 var tables = await _database.QueryAsync<dynamic>("SELECT name FROM sqlite_master WHERE type='table' AND name LIKE '%mployee%'");
                    string tableName = "Employee"; // Default
                if (tables?.Any() == true)
                {
                    tableName = tables.First().name;
                    _logger.LogInformation($"Using table name: {tableName} for index creation");
                }

                var indexDefinitions = new Dictionary<string, string>
        {
            { "idx_employee_name", $"CREATE INDEX IF NOT EXISTS idx_employee_name ON {tableName}(Name)" },
            { "idx_employee_phone", $"CREATE INDEX IF NOT EXISTS idx_employee_phone ON {tableName}(Phone)" } // ✅ Fixed: Changed from PhoneNumber to Phone
        };

                foreach (var indexDef in indexDefinitions)
                {
                    string indexName = indexDef.Key;
                    string command = indexDef.Value;

                    try
                    {
                        await _database.ExecuteAsync(command);
                        successfulIndexes.Add(indexName);
                        _logger.LogInformation($"✅ Successfully created index: {indexName}");
                    }
                    catch (Exception ex)
                    {
                        failedIndexes.Add(indexName);
                        _logger.LogError(ex, $"❌ Failed to create index: {indexName} - Command: {command}");
                    }
                }

                // Summary logging
                if (successfulIndexes.Any())
                {
                    _logger.LogInformation($"Successfully created {successfulIndexes.Count} indexes: [{string.Join(", ", successfulIndexes)}]");
                }

                if (failedIndexes.Any())
                {
                    _logger.LogWarning($"Failed to create {failedIndexes.Count} indexes: [{string.Join(", ", failedIndexes)}]");
                }
                else
                {
                    _logger.LogInformation("All employee database indexes created successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during index creation process");
                // Don't throw - the service can work without indexes
            }*/
                // Get the actual table name from the mapping
                var mapping = _database.GetConnection().GetMapping(typeof(Employee));
                string tableName = mapping.TableName;
                var indexCommands = new[]{
                    $"CREATE INDEX IF NOT EXISTS idx_employee_name ON {tableName}(Name)",
                    $"CREATE INDEX IF NOT EXISTS idx_employee_phone ON {tableName}(Phone)"};
                foreach (var command in indexCommands)
                {
                    try
                    {
                        await _database.ExecuteAsync(command);
                        _logger.LogInformation($"Successfully created index: {command}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to create index with command: {command}");
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during index creation process");
                // Don't throw - the service can work without indexes
            }
        }
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
                if (employee.Id == 0)
                {
                    await _database.InsertAsync(employee);
                    _logger?.LogInformation("Employee with ID {EmployeeId} added successfully", employee.Id);
                }
                else
                {
                    await _database.UpdateAsync(employee);
                    _logger?.LogInformation("Employee with ID {EmployeeId} updated successfully", employee.Id);
                }
                return employee;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Save failed - constraint violation for employee ID {EmployeeId}", employee.Id);
                throw new DataConstraintException("Save failed - possible duplicate phone number or other constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving employee with ID {EmployeeId}", employee.Id);
                throw new DatabaseOperationException($"Failed to save employee with ID {employee.Id}", ex);
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



        // EmployeeDatabaseService.cs

        public async Task<int> SaveEmployeeWorkRecordAsync(EmployeeWorkRecord record)
        {
            await InitializeAsync();
            try
            {
                if (record.Id == 0)
                {
                    return await _database.InsertAsync(record);
                }
                else
                {
                    await _database.UpdateAsync(record);
                    return record.Id;
                }
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
                                              .OrderByDescending(ew => ew.AddedDate) // Use AddedDate instead of Date
                                              .ToListAsync();

                _logger.LogDebug("Retrieved {Count} work records for Employee {EmployeeId}",
                                 records.Count, employeeId);

                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work records for Employee {EmployeeId}", employeeId);
                return Enumerable.Empty<EmployeeWorkRecord>().ToList(); // More explicit empty list
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
                // Replace LINQ query syntax with explicit query execution
                var employeeWorkRecords = await _database.Table<EmployeeWorkRecord>()
                                                          .Where(ew => ew.EmployeeId == employeeId)
                                                          .ToListAsync();

                var workRecordIds = employeeWorkRecords.Select(ew => ew.WorkRecordId).Distinct().ToList();

                if (!workRecordIds.Any())
                {
                    return new List<WorkRecord>();
                }

                var workRecords = await _database.Table<WorkRecord>()
                                                 .Where(wr => workRecordIds.Contains(wr.Id))
                                                 .OrderByDescending(wr => wr.CreatedAt) // Assuming there's a Date field
                                                 .ToListAsync();

                return workRecords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work records for Employee {EmployeeId}", employeeId);
                return new List<WorkRecord>();
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
                _logger.LogError(ex, "Error fetching employees");
                return new List<Employee>();
            }
        }

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
                    _logger?.LogInformation("Employee with ID {EmployeeId} deleted successfully", employee.Id);
                }
                else
                {
                    _logger?.LogWarning("No employee found with ID {EmployeeId} to delete", employee.Id);
                }
                return rowsAffected;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Delete failed - constraint violation for employee ID {EmployeeId}", employee.Id);
                throw new DataConstraintException("Delete failed - possible foreign key constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting employee with ID {EmployeeId}", employee.Id);
                throw new DatabaseOperationException($"Failed to delete employee with ID {employee.Id}", ex);
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
                int rowsAffected = await _database.UpdateAsync(employee);

                if (rowsAffected == 1)
                {
                    _logger?.LogInformation("Employee with ID {EmployeeId} updated successfully", employee.Id);
                    return true;
                }

                // Check if employee exists
                var existingEmployee = await _database.Table<Employee>()
                    .Where(e => e.Id == employee.Id)
                    .CountAsync() > 0;

                _logger?.LogWarning(existingEmployee ?
                    $"Update affected {rowsAffected} rows (expected 1) for employee ID {employee.Id}" :
                    $"Employee with ID {employee.Id} not found");

                return false;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Update failed - constraint violation for employee ID {EmployeeId}", employee.Id);
                throw new DataConstraintException("Update failed - possible duplicate phone number or other constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating employee with ID {EmployeeId}", employee.Id);
                throw new DatabaseOperationException($"Failed to update employee with ID {employee.Id}", ex);
            }
        }

        public async Task<List<Employee>> GetActiveEmployeesAsync(DateTime? referenceDate = null)
        {
            await InitializeAsync();

            // If we don't have access to WorkRecord service, return all employees
            if (_workRecordService == null)
            {
                _logger.LogWarning("WorkRecord service not available, returning all employees as active");
                return await GetAllEmployeesAsync();
            }

            var cutoffDate = (referenceDate ?? DateTime.Now).AddDays(-14);

            try
            {
                // Use the work record service to get active employee data
                var workRecords = await _workRecordService.GetWorkRecordsAsync(
                    startDate: cutoffDate,
                    endDate: DateTime.Now);

                if (!workRecords.Any())
                    return new List<Employee>();

                // Get unique employee IDs from work records
                var activeEmployeeIds = workRecords
                    .Where(w => !string.IsNullOrEmpty(w.EmployeeIds))
                    .SelectMany(w => w.GetEmployeeIdList())
                    .Distinct()
                    .ToList();

                if (!activeEmployeeIds.Any())
                    return new List<Employee>();

                // Get employees by IDs
                var employees = new List<Employee>();
                foreach (var id in activeEmployeeIds)
                {
                    var employee = await GetEmployeeByIdAsync(id);
                    if (employee != null)
                        employees.Add(employee);
                }

                return employees.OrderBy(e => e.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active employees");
                throw;
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
        public async Task ResetDatabaseAsync()
        {
            await InitializeAsync();
            try
            {
                await _database.DropTableAsync<Employee>();
                await _database.CreateTableAsync<Employee>();
                await CreateIndexesAsync();
                _isInitialized = false; // Reset initialization flag
                await InitializeAsync(); // Reinitialize
                _logger.LogInformation("Employee database reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset employee database");
                throw new DatabaseOperationException("Failed to reset employee database", ex);
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
                await _database.InsertAsync(employee);
                _logger?.LogInformation("Employee with ID {EmployeeId} added successfully", employee.Id);
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger?.LogError(ex, "Add failed - constraint violation for employee ID {EmployeeId}", employee.Id);
                throw new DataConstraintException("Add failed - possible duplicate phone number or other constraint violation.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error adding employee with ID {EmployeeId}", employee.Id);
                throw new DatabaseOperationException($"Failed to add employee with ID {employee.Id}", ex);
            }
        }
    }
}