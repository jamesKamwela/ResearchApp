using Microsoft.Extensions.Logging;
using ResearchApp.Exceptions;
using ResearchApp.Models;
using ResearchApp.StaticClasses;
using SQLite;
using SQLiteNetExtensions.Extensions;       // For synchronous operations
using SQLiteNetExtensionsAsync.Extensions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

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

                    // Create tables directly using the types
                    await CreateTablesAsync();
                    await VerifyTablesExist();
                    // await MigrateSchema();
                    //await ValidateSchema();
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

                //_logger.LogDebug("Creating ClientEmployeeRecord table");
                //await _database.CreateTableAsync<ClientWorkRecord>();
                Debug.WriteLine("Creating ClientWorkRecord table");
                try
                {
                    var result = await _database.CreateTableAsync<ClientWorkRecord>();
                    Debug.WriteLine($"CreateTable result for ClientWorkRecord: {result}");
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create ClientWorkRecords table");
                    throw;
                }
                // DEBUGGING: List all tables after creation
                var tables = await _database.QueryAsync<TableInfo>(
                    "SELECT name FROM sqlite_master WHERE type='table'");
                _logger.LogInformation("Existing tables: " +
                    string.Join(", ", tables.Select(t => t.name)));


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
                var tableInfo = await _database.QueryAsync<TableInfo>(
                  "SELECT name FROM sqlite_master WHERE type='table'");

                var tableNames = tableInfo.Select(t => t.name).ToList();

                if (!tableNames.Contains("ClientWorkRecords"))
                {
                    _logger.LogWarning("ClientWorkRecords table does not exist, skipping indexes");
                    return;
                }
                var indexCommands = new List<string>
        {
            // Clients indexes
            "CREATE INDEX IF NOT EXISTS idx_client_name ON Clients(Name)",
            "CREATE INDEX IF NOT EXISTS idx_client_phone ON Clients(Phone)",
            // Employees indexes
            "CREATE INDEX IF NOT EXISTS idx_employee_name ON Employees(Name)",
            // Jobs indexes
            "CREATE INDEX IF NOT EXISTS idx_job_name ON Jobs(JobName)",
            "CREATE INDEX IF NOT EXISTS idx_job_clientid ON Jobs(ClientId)",

            // WorkRecords Employee indexes
            "CREATE INDEX IF NOT EXISTS idx_workrecord_clientid ON WorkRecords(ClientId)",
            "CREATE INDEX IF NOT EXISTS idx_workrecord_jobid ON WorkRecords(JobId)",
            "CREATE INDEX IF NOT EXISTS idx_workrecord_date ON WorkRecords(WorkDate)",
            "CREATE INDEX IF NOT EXISTS idx_workrecord_dates ON WorkRecords(WorkDate, CompletedDate, PaidDate)",

            //ClientWorkRecords  indexes
            "CREATE INDEX IF NOT EXISTS idx_clientworkrecord_clientid ON ClientWorkRecords(ClientId)",
            "CREATE INDEX IF NOT EXISTS idx_clientworkrecord_workrecordid ON ClientWorkRecords(WorkRecordId)",
            "CREATE INDEX IF NOT EXISTS idx_clientworkrecord_addeddateticks ON ClientWorkRecords(AddedDateTicks)",
            "CREATE INDEX IF NOT EXISTS idx_clientworkrecord_ispaid ON ClientWorkRecords(IsPaid)",



            // EmployeeWorkRecords indexes
            "CREATE INDEX IF NOT EXISTS idx_employeeworkrecord_employeeid ON EmployeeWorkRecords(EmployeeId)",
            "CREATE INDEX IF NOT EXISTS idx_employeeworkrecord_workrecordid ON EmployeeWorkRecords(WorkRecordId)",
            "CREATE INDEX IF NOT EXISTS idx_employeeworkrecord_addeddateticks ON EmployeeWorkRecords(AddedDateTicks)",


            // Auditable indexes
            "CREATE INDEX IF NOT EXISTS idx_workrecord_created ON WorkRecords(CreatedAt)",
            "CREATE INDEX IF NOT EXISTS idx_workrecord_updated ON WorkRecords(UpdatedAt)",
            "CREATE INDEX IF NOT EXISTS idx_employee_created ON Employees(CreatedAt)",

        };

                // Execute all index commands
                foreach (var command in indexCommands)
                {
                    try
                    {
                        await _database.ExecuteAsync(command);
                        _logger.LogDebug($"Created index: {command}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to create index: {command}");
                    }
                }

                _logger.LogInformation($"Created {indexCommands.Count} indexes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index creation failed");
                throw;
            }
        }
        private async Task VerifyTablesExist()
        {
            var expectedTables = new[] {
        "Clients", "Employees", "Jobs",
        "WorkRecords", "EmployeeWorkRecords",
        "ClientWorkRecords" 
    };

            var existingTables = (await _database.QueryAsync<TableInfo>(
                "SELECT name FROM sqlite_master WHERE type='table'"))
                .Select(t => t.name)
                .ToList();

            var missingTables = expectedTables
                .Where(et => !existingTables.Any(t =>
                    t.Equals(et, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missingTables.Any())
            {
                throw new InvalidOperationException(
                    $"Missing tables in database: {string.Join(", ", missingTables)}. " +
                    $"Existing tables: {string.Join(", ", existingTables)}");
            }
        }
        private async Task ValidateSchema()
        {
            await EnsureInitializedAsync();

            var expectedColumns = new Dictionary<Type, List<string>>
    {
        {
            typeof(Employee),
            new List<string> {"Id", "Name", "Phone", "Address","TotalEarnings", "PaidEarnings","CreatedAt", "UpdatedAt"}
        },
        {
            typeof(Client),
            new List<string> {"Id", "Name", "Phone", "Address","CreatedAt", "UpdatedAt" }
        },
        {
            typeof(Job),
            new List<string> {"Id", "JobName", "ClientId", "Description","DefaultCommissionRate", "CreatedAt", "UpdatedAt"}
        },
        {
            typeof(WorkRecord),
            new List<string> {
                "Id", "ClientId", "JobId", "JobName", "ClientName", "WorkDate",
                "QuantityCompleted", "CommissionRate", "TotalAmount",
                "AdminCommission", "EmployeePool", "AmountPerEmployee",
                "IsJobCompleted", "IsPaid", "CreatedAt", "UpdatedAt",
                "CompletedDate", "PaidDate", "IsPaymentProcessed", "EmployeeCount"
            }
        },
        {
            typeof(EmployeeWorkRecord),
            new List<string> {
                "Id", "EmployeeId", "WorkRecordId", "AddedDateTicks"
            }
        },
                {
                   typeof(ClientWorkRecord),
                   new List<string> {
                          "Id", "ClientId", "WorkRecordId", "AddedDateTicks", "IsPaid"

                }
                },
    };

            foreach (var entity in expectedColumns)
            {
                try
                {
                    // Option 1: Use async query to get table info instead of GetConnection()
                    var tableName = GetTableName(entity.Key);

                    // Get actual columns from database using async query
                    var actualColumns = (await _database.QueryAsync<TableInfo>(
                        $"PRAGMA table_info({tableName})"))
                        .Select(c => c.name)
                        .ToList();

                    // Check for missing columns
                    var missingColumns = entity.Value.Except(actualColumns).ToList();
                    if (missingColumns.Any())
                    {
                        throw new DatabaseValidationException(
                            $"Missing columns in {tableName}: {string.Join(", ", missingColumns)}");
                    }

                    // Check for unexpected columns
                    var unexpectedColumns = actualColumns.Except(entity.Value)
                        .Where(c => !c.Equals("_id", StringComparison.OrdinalIgnoreCase)) // Ignore SQLite internal columns
                        .ToList();

                    if (unexpectedColumns.Any())
                    {
                        _logger.LogWarning($"Unexpected columns in {tableName}: {string.Join(", ", unexpectedColumns)}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Schema validation failed for {EntityType}", entity.Key.Name);
                }
            }
        }

        // Helper method to get table name without using GetConnection()
        private string GetTableName(Type entityType)
        {
            // You can either:
            // 1. Use reflection to get the table name from SQLite attributes
            var tableAttribute = entityType.GetCustomAttributes(typeof(SQLite.TableAttribute), false).FirstOrDefault() as SQLite.TableAttribute;
            if (tableAttribute != null)
            {
                return tableAttribute.Name;
            }

            // 2. Or use a simple mapping/convention
            return entityType.Name switch
            {
                nameof(Employee) => "Employees",
                nameof(Client) => "Clients",
                nameof(Job) => "Jobs",
                nameof(WorkRecord) => "WorkRecords",
                nameof(EmployeeWorkRecord) => "EmployeeWorkRecords",
                nameof(ClientWorkRecord) => "ClientWorkRecords",
                _ => entityType.Name + "s" // Default pluralization
            };
        }
        // Helper class for PRAGMA table_info results
        private class TableInfo
        {
            public string name { get; set; }
            public string type { get; set; }
            public int notnull { get; set; }
            public string dflt_value { get; set; }
            public int pk { get; set; }
        }
        // Custom exception for schema validation

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
            if (entity == null)
            {
                _logger.LogWarning("Attempted to insert null entity");
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                await EnsureInitializedAsync();

                if (entity is IAuditable auditable)
                {
                    auditable.CreatedAt = DateTime.UtcNow;
                    _logger.LogDebug("Set CreatedAt timestamp for new entity");
                }

                if (entity is WorkRecord workRecord)
                {
                    _logger.LogDebug("Inserting WorkRecord with ID {WorkRecordId}", workRecord.Id);
                    await _database.InsertWithChildrenAsync(workRecord, recursive: true);
                    _logger.LogInformation("Inserted WorkRecord with ID {WorkRecordId}", workRecord.Id);
                    return entity is IEntity identifiable ? identifiable.Id : 0;
                }
                else
                {
                    _logger.LogDebug("Inserting entity of type {EntityType}", typeof(T).Name);
                    return await _database.InsertAsync(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting entity of type {EntityType}", typeof(T).Name);
                throw new DatabaseOperationException("Failed to insert entity", ex);
            }
        }  
        public async Task<int> UpdateAsync<T>(T entity) where T : new()
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to update null entity");
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                await EnsureInitializedAsync();

                if (entity is IAuditable auditable)
                {
                    auditable.UpdatedAt = DateTime.UtcNow;
                    _logger.LogDebug("Set UpdatedAt timestamp for entity");
                }

                if (HasRelationships<T>())
                {
                    _logger.LogDebug("Updating entity with relationships of type {EntityType}", typeof(T).Name);
                    await _database.UpdateWithChildrenAsync(entity);
                    return 1;
                }

                _logger.LogDebug("Updating simple entity of type {EntityType}", typeof(T).Name);
                return await _database.UpdateAsync(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity of type {EntityType}", typeof(T).Name);
                throw new DatabaseOperationException("Failed to update entity", ex);
            }
        }
        private bool HasRelationships<T>()
        {
            return typeof(T) == typeof(WorkRecord) ||
                   typeof(T) == typeof(Employee) ||
                   typeof(T) == typeof(EmployeeWorkRecord);
        }
        public async Task<int> SaveAsync<T>(T entity) where T : IEntity, new()
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to save null entity");
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                await EnsureInitializedAsync();
                _logger.LogInformation("Saving entity of type {EntityType} with ID {EntityId}", typeof(T).Name, entity.Id);

                return entity.Id == 0 ? await InsertAsync(entity) : await UpdateAsync(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving entity of type {EntityType}", typeof(T).Name);
                throw new DatabaseOperationException("Failed to save entity", ex);
            }
        }
        public async Task<int> DeleteAsync<T>(T entity) where T : new()
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to delete null entity");
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                await EnsureInitializedAsync();

                var entityId = (entity as IEntity)?.Id ?? 0;
                _logger.LogInformation("Deleting entity of type {EntityType} with ID {EntityId}", typeof(T).Name, entityId);

                await _database.DeleteAsync(entity, recursive: true);

                _logger.LogInformation("Deleted entity of type {EntityType} with ID {EntityId}", typeof(T).Name, entityId);
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity of type {EntityType}", typeof(T).Name);
                throw new DatabaseOperationException("Failed to delete entity", ex);
            }
        }
       
        public async Task<T> GetByIdAsync<T>(int id) where T : IEntity, new()
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to get entity with invalid ID: {Id}", id);
                return default;
            }

            try
            {
                await EnsureInitializedAsync();
                _logger.LogDebug("Getting entity of type {EntityType} with ID {Id}", typeof(T).Name, id);

                return await _database.Table<T>().FirstOrDefaultAsync(x => x.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        public async Task<T> GetByIdWithChildrenAsync<T>(int id, bool recursive = true) where T : IEntity, new()
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to get entity with children using invalid ID: {Id}", id);
                return default;
            }

            await EnsureInitializedAsync();
            try
            {
                _logger.LogDebug("Getting entity of type {EntityType} with ID {Id} and children (recursive: {Recursive})",
                    typeof(T).Name, id, recursive);

                return await _database.GetWithChildrenAsync<T>(id, recursive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading {typeof(T).Name} with ID {id} and children");
                throw;
            }
        }

        public async Task<List<T>> GetAllAsync<T>(bool loadChildren = false) where T : new()
        {
            try
            {
                await EnsureInitializedAsync();

                if (loadChildren)
                {
                    _logger.LogInformation("Retrieving all entities of type {EntityType} with children", typeof(T).Name);
                    return await _database.GetAllWithChildrenAsync<T>(recursive: true);
                }
                else
                {
                    _logger.LogInformation("Retrieving all entities of type {EntityType} without children", typeof(T).Name);
                    return await _database.Table<T>().ToListAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all entities of type {EntityType}", typeof(T).Name);
                throw new DatabaseOperationException("Failed to retrieve all entities", ex);
            }
        }

        #endregion
        #region Bulk Operations

        public async Task<List<T>> GetByIdsAsync<T>(List<int> ids) where T : IEntity, new()
        {
            if (ids == null || !ids.Any())
            {
                _logger.LogWarning("Empty or null IDs list provided");
                return new List<T>();
            }

            try
            {
                await EnsureInitializedAsync();

                // Use SQLite-NET's built-in parameter binding instead of dictionary
                var placeholders = string.Join(",", ids.Select(id => "?"));
                var query = $"SELECT * FROM {GetTableName(typeof(T))} WHERE Id IN ({placeholders})";

                _logger.LogDebug($"Fetching {typeof(T).Name} entities with IDs: {string.Join(",", ids)}");

                // Convert List<int> to object[] for SQLite parameter binding
                var parameters = ids.Cast<object>().ToArray();

                return await _database.QueryAsync<T>(query, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching {typeof(T).Name} entities by IDs");
                throw new DatabaseOperationException($"Failed to fetch {typeof(T).Name} entities by IDs", ex);
            }
        }

        public async Task<List<WorkRecord>> GetWorkRecordsByIdsAsync(List<int> workRecordIds, bool loadChildren = false, bool? completedOnly = null,
        string period = "Current Week", DateTime? customStartDate = null, DateTime? customEndDate = null)
        {
            Debug.WriteLine($"GetWorkRecordsByIdsAsync called with {workRecordIds?.Count ?? 0} IDs");

            if (workRecordIds == null || !workRecordIds.Any())
            {
                Debug.WriteLine("No work record IDs provided");
                return new List<WorkRecord>();
            }

            try
            {
                await EnsureInitializedAsync();
                Debug.WriteLine($"First ID in list: {workRecordIds.First()}");

                // Get date range based on period or custom dates
                var (startDate, endDate) = GetDateRange(period);
                startDate = customStartDate ?? startDate;
                endDate = customEndDate ?? endDate;
                Debug.WriteLine($"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                if (loadChildren)
                {
                    Debug.WriteLine("Loading records with children...");

                    // More efficient query for single ID case
                    if (workRecordIds.Count == 1)
                    {
                        Debug.WriteLine("Using single ID optimization");
                        var result = new List<WorkRecord>();

                        try
                        {
                            var singleRecord = await _database.GetWithChildrenAsync<WorkRecord>(workRecordIds.First());

                            if (singleRecord != null &&
                                singleRecord.WorkDate >= startDate &&
                                singleRecord.WorkDate <= endDate &&
                                (!completedOnly.HasValue || singleRecord.IsJobCompleted == completedOnly.Value))
                            {
                                result.Add(singleRecord);
                            }
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("Sequence contains no elements"))
                        {
                            Debug.WriteLine($"Work record with ID {workRecordIds.First()} not found in database");
                            // Return empty list - record doesn't exist
                        }

                        Debug.WriteLine($"Returning {result.Count} records after filtering");
                        return result;
                    }

                    // Get all records with children in a single query
                    var allRecords = await _database.GetAllWithChildrenAsync<WorkRecord>();
                    Debug.WriteLine($"Retrieved {allRecords.Count} records with children");

                    // Apply all filters in one go
                    var filteredRecords = allRecords.Where(wr =>
                        workRecordIds.Contains(wr.Id) &&
                        wr.WorkDate >= startDate &&
                        wr.WorkDate <= endDate &&
                        (!completedOnly.HasValue || wr.IsJobCompleted == completedOnly.Value)
                    ).ToList();

                    Debug.WriteLine($"Filtered to {filteredRecords.Count} matching records");
                    return filteredRecords;
                }
                else
                {
                    Debug.WriteLine("Loading records without children...");

                    // Build parameterized query
                    var placeholders = string.Join(",", workRecordIds.Select(id => "?"));
                    var query = $"SELECT * FROM WorkRecords WHERE Id IN ({placeholders}) AND WorkDate >= ? AND WorkDate <= ?";

                    // Convert parameters to object array
                    var parameterList = workRecordIds.Cast<object>().ToList();
                    parameterList.Add(startDate);
                    parameterList.Add(endDate);

                    // Add completion filter if specified
                    if (completedOnly.HasValue)
                    {
                        query += " AND IsJobCompleted = ?";
                        parameterList.Add(completedOnly.Value);
                    }

                    Debug.WriteLine($"Executing query: {query}");
                    Debug.WriteLine($"Parameters: {string.Join(", ", parameterList)}");

                    var results = await _database.QueryAsync<WorkRecord>(query, parameterList.ToArray());
                    Debug.WriteLine($"Found {results.Count} matching records");
                    return results;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetWorkRecordsByIdsAsync: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new DatabaseOperationException("Failed to fetch work records by IDs with filters", ex);
            }
        }

        #endregion
        #region WorkRecord Specific Methods 
        public async Task<bool> UpdateWorkRecord(WorkRecord record)
        {
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
                _logger.LogDebug("Updating work record ID: {WorkRecordId}", record.Id);

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

                record.UpdatedAt = DateTime.UtcNow;

                var result = await UpdateAsync(record);

                if (result == 1)
                {
                    _logger.LogInformation($"Successfully updated work record ID: {record.Id}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"No rows affected when updating work record ID: {record.Id}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating work record ID: {record.Id}");
                throw;
            }
        }
        /*public async Task<int> SaveWorkRecordWithEmployeesInTransactionAsync(WorkRecord workRecord, List<EmployeeWorkRecord> employeeWorkRecords)
        {
            if (workRecord == null)
            {
                _logger.LogError("Attempted to save null work record");
                throw new ArgumentNullException(nameof(workRecord));
            }
            if (employeeWorkRecords == null)
            {
                _logger.LogError("Attempted to save null employee work records list");
                throw new ArgumentNullException(nameof(employeeWorkRecords));
            }
            await EnsureInitializedAsync();
            try
            {
                // Get the synchronous connection for the transaction
                var connection = _database.GetConnection();
                return await Task.Run(() =>
                {
                    int resultId = 0; // Variable to capture the return value

                    connection.RunInTransaction(() =>
                    {
                        _logger.LogDebug("Starting transaction to save work record with {EmployeeCount} employees",
                            employeeWorkRecords.Count);

                        // Save the work record (insert or update)
                        if (workRecord.Id == 0)
                        {
                            workRecord.CreatedAt = DateTime.UtcNow;
                            connection.Insert(workRecord);
                            _logger.LogDebug("Inserted new work record with ID {WorkRecordId}", workRecord.Id);
                        }
                        else
                        {
                            workRecord.UpdatedAt = DateTime.UtcNow;
                            connection.Update(workRecord);
                            _logger.LogDebug("Updated existing work record with ID {WorkRecordId}", workRecord.Id);
                        }

                        // Save all employee work records
                        foreach (var ewr in employeeWorkRecords)
                        {
                            ewr.WorkRecordId = workRecord.Id;
                            // Set timestamp if not already set
                            if (ewr.AddedDateTicks == 0)
                            {
                                ewr.AddedDateTicks = DateTime.UtcNow.ToBinary();
                            }
                            connection.Insert(ewr);
                        }

                        _logger.LogInformation("Successfully saved work record with ID {WorkRecordId} and {EmployeeCount} employee records",
                            workRecord.Id, employeeWorkRecords.Count);

                        resultId = workRecord.Id; // Capture the ID to return
                    });

                    return resultId; // Return the captured value
                });
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger.LogError(ex, "Constraint violation saving work record with employees");
                throw new DataConstraintException("Failed to save work record - possible duplicate or constraint violation", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving work record with employees in transaction");
                throw new DatabaseOperationException("Failed to save work record with employees", ex);
            }
        }*/
        public async Task<bool> DeleteWorkRecordByIdAsync(int workRecordId)
        {
            if (workRecordId <= 0)
            {
                _logger.LogError("Attempted to delete work record with invalid ID: {WorkRecordId}", workRecordId);
                throw new ArgumentException("Work record ID must be greater than zero");
            }
            await EnsureInitializedAsync();
            try
            {
                var record = await GetByIdAsync<WorkRecord>(workRecordId);
                if (record == null)
                {
                    _logger.LogWarning("No work record found with ID: {WorkRecordId}", workRecordId);
                    return false;
                }
                _logger.LogInformation("Deleting work record with ID {WorkRecordId}", workRecordId);
                var result = await DeleteAsync(record);
                if (result > 0)
                {
                    _logger.LogInformation("Successfully deleted work record with ID {WorkRecordId}", workRecordId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Delete operation did not affect any rows for work record ID {WorkRecordId}", workRecordId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting work record with ID {WorkRecordId}", workRecordId);
                throw new DatabaseOperationException("Failed to delete work record", ex);
            }
        }
        public async Task<int> SaveWorkRecordWithEmployeesInTransactionAsync(WorkRecord workRecord,List<EmployeeWorkRecord> employeeWorkRecords,
            ClientWorkRecord clientWorkRecord = null)
        {
            if (workRecord == null)
            {
                _logger.LogError("Attempted to save null work record");
                throw new ArgumentNullException(nameof(workRecord));
            }
            if (employeeWorkRecords == null)
            {
                _logger.LogError("Attempted to save null employee work records list");
                throw new ArgumentNullException(nameof(employeeWorkRecords));
            }

            await EnsureInitializedAsync();

            try
            {
                // Get the synchronous connection for the transaction
                var connection = _database.GetConnection();
                return await Task.Run(() =>
                {
                    int resultId = 0; // Variable to capture the return value

                    connection.RunInTransaction(() =>
                    {
                       
                       
                        _logger.LogDebug("Starting transaction to save work record with {EmployeeCount} employees",
                            employeeWorkRecords.Count);

                        // Save the work record (insert or update)
                        if (workRecord.Id == 0)
                        {
                            workRecord.CreatedAt = DateTime.UtcNow;
                            connection.Insert(workRecord);
                            _logger.LogDebug("Inserted new work record with ID {WorkRecordId}", workRecord.Id);
                        }
                        else
                        {
                            workRecord.UpdatedAt = DateTime.UtcNow;
                            connection.Update(workRecord);
                            _logger.LogDebug("Updated existing work record with ID {WorkRecordId}", workRecord.Id);
                        }

                        // Save all employee work records
                        foreach (var ewr in employeeWorkRecords)
                        {
                            ewr.WorkRecordId = workRecord.Id;
                            // Set timestamp if not already set
                            if (ewr.AddedDateTicks == 0)
                            {
                                ewr.AddedDateTicks = DateTime.UtcNow.ToBinary();
                            }
                            connection.Insert(ewr);
                        }

                        // Save client work record if provided
                        if (clientWorkRecord != null)
                        {
                            clientWorkRecord.WorkRecordId = workRecord.Id;
                            // Set timestamp if not already set
                            if (clientWorkRecord.AddedDateTicks == 0)
                            {
                                clientWorkRecord.AddedDateTicks = DateTime.UtcNow.ToBinary();
                            }
                            connection.Insert(clientWorkRecord);
                            _logger.LogDebug("Inserted client work record for client ID {ClientId}", clientWorkRecord.ClientId);
                        }

                        _logger.LogInformation(
                            "Successfully saved work record with ID {WorkRecordId}, {EmployeeCount} employee records, and client record",
                            workRecord.Id, employeeWorkRecords.Count);

                        resultId = workRecord.Id; // Capture the ID to return
                    });

                    return resultId; // Return the captured value
                });
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                _logger.LogError(ex, "Constraint violation saving work record with employees and client");
                throw new DataConstraintException(
                    "Failed to save work record - possible duplicate or constraint violation", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving work record with employees and client in transaction");
                throw new DatabaseOperationException(
                    "Failed to save work record with employees and client", ex);
            }
        }

        public async Task<List<Employee>> GetEmployeesByWorkRecordIdAsync(int workRecordId)
        {
            await EnsureInitializedAsync();

            if (workRecordId <= 0)
            {
                _logger.LogWarning("Invalid workRecordId: {WorkRecordId}", workRecordId);
                return new List<Employee>();
            }

            try
            {
                _logger.LogDebug("Getting employees for work record {WorkRecordId}", workRecordId);

                // Efficient single query with joins
                var query = @"
            SELECT e.* 
            FROM Employees e
            JOIN EmployeeWorkRecords ewr ON e.Id = ewr.EmployeeId
            WHERE ewr.WorkRecordId = ?
            ORDER BY ewr.AddedDateTicks DESC";

                var employees = await _database.QueryAsync<Employee>(query, workRecordId);

                _logger.LogDebug("Found {Count} employees for work record {WorkRecordId}",
                    employees.Count, workRecordId);

                return employees ?? new List<Employee>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees for work record {WorkRecordId}", workRecordId);
                return new List<Employee>();
            }
        }
        #endregion

        #region Generic Query Methods


        public async Task<List<T>> GetFilteredAsync<T>(
           Expression<Func<T, bool>> predicate = null,
           Func<AsyncTableQuery<T>, AsyncTableQuery<T>> queryModifier = null)
           where T : new()
        {
            await EnsureInitializedAsync();

            try
            {
                var query = _database.Table<T>();
                _logger.LogDebug("Building query for type {EntityType}", typeof(T).Name);

                if (predicate != null)
                {
                    query = query.Where(predicate);
                    _logger.LogDebug("Applied predicate filter to query");
                }

                if (queryModifier != null)
                {
                    query = queryModifier(query);
                    _logger.LogDebug("Applied custom query modifier");
                }

                var results = await query.ToListAsync();
                _logger.LogDebug("Retrieved {Count} records of type {EntityType}", results.Count, typeof(T).Name);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing filtered query for type {EntityType}", typeof(T).Name);
                throw;
            }
        }
        public async Task<int> GetCountAsync<T>(Expression<Func<T, bool>> predicate = null) where T : new()
        {
            await EnsureInitializedAsync();

            try
            {
                var query = _database.Table<T>();
                _logger.LogDebug("Building count query for type {EntityType}", typeof(T).Name);

                if (predicate != null)
                {
                    _logger.LogDebug("Applying predicate to count query");
                    return await query.CountAsync(predicate);
                }

                _logger.LogDebug("Executing simple count query");
                return await query.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting count for type {EntityType}", typeof(T).Name);
                throw;
            }
        }


        #endregion

        #region client work record specific methods

        public async Task<List<ClientWorkRecord>> GetAllClientWorkRecordsAsync(bool isComplete, bool isPaid, string period = "Current Week")
        {
            await EnsureInitializedAsync();
            try
            {
                var (startDate, endDate) = GetDateRange(period);
                // Build the base query with parameters
                var query = new StringBuilder(@"
                      SELECT cw.Id 
                      FROM ClientWorkRecords cw
                      JOIN WorkRecords wr ON cw.WorkRecordId = wr.Id
                      WHERE cw.IsPaid = ?
                      AND wr.IsJobCompleted = ?
                      AND wr.WorkDate >= ?
                      AND wr.WorkDate <= ?");
                var parameters = new List<object> { isPaid, isComplete, startDate, endDate };
                // Add sorting
                query.Append(" ORDER BY wr.WorkDate DESC, cw.AddedDateTicks DESC");
                // Get sorted IDs
                var sortedIds = await _database.QueryAsync<ClientWorkRecord>(query.ToString(), parameters.ToArray());
                // Then get full records with children
                return await _database.GetAllWithChildrenAsync<ClientWorkRecord>(
                    filter: cw => sortedIds.Select(x => x.Id).Contains(cw.Id),
                    recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all client work records");
                return new List<ClientWorkRecord>();
            }
        }
        public async Task<List<ClientWorkRecord>> GetClientWorkRecordsAsync(int? clientId, bool isPaid, bool isComplete, string period = "Current Week")
        {
            await EnsureInitializedAsync();
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // Option 1: Single query approach (recommended)
                var query = new StringBuilder(@"
            SELECT cw.* 
            FROM ClientWorkRecords cw
            JOIN WorkRecords wr ON cw.WorkRecordId = wr.Id
            WHERE cw.IsPaid = ?
            AND wr.IsJobCompleted = ?
            AND wr.WorkDate >= ?
            AND wr.WorkDate <= ?");

                var parameters = new List<object> { isPaid, isComplete, startDate, endDate };

                // Add client filter if specified
                if (clientId.HasValue)
                {
                    query.Append(" AND cw.ClientId = ?");
                    parameters.Add(clientId.Value);
                }

                // Add sorting
                query.Append(" ORDER BY wr.WorkDate DESC, cw.AddedDateTicks DESC");

                // Get records directly
                var records = await _database.QueryAsync<ClientWorkRecord>(query.ToString(), parameters.ToArray());

                // Load related data for each record
                foreach (var record in records)
                {
                    await _database.GetChildrenAsync(record, recursive: true);
                }

                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting client work records");
                return new List<ClientWorkRecord>();
            }
        }

        // Alternative Option 2: If you need more complex filtering
        public async Task<List<ClientWorkRecord>> GetClientWorkRecordsAsync_Alternative(int? clientId, bool isPaid, bool isComplete, string period = "Current Week")
        {
            await EnsureInitializedAsync();
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // First get all ClientWorkRecords that match basic criteria
                var baseQuery = @"
            SELECT cw.Id
            FROM ClientWorkRecords cw
            JOIN WorkRecords wr ON cw.WorkRecordId = wr.Id
            WHERE cw.IsPaid = ?
            AND wr.IsJobCompleted = ?
            AND wr.WorkDate >= ?
            AND wr.WorkDate <= ?";

                var parameters = new List<object> { isPaid, isComplete, startDate, endDate };

                if (clientId.HasValue)
                {
                    baseQuery += " AND cw.ClientId = ?";
                    parameters.Add(clientId.Value);
                }

                baseQuery += " ORDER BY wr.WorkDate DESC, cw.AddedDateTicks DESC";

                // Get just the IDs first
                var idResults = await _database.QueryAsync<IdResult>(baseQuery, parameters.ToArray());
                var sortedIds = idResults.Select(r => r.Id).ToList();

                if (!sortedIds.Any())
                {
                    return new List<ClientWorkRecord>();
                }

                // Then get full records with children, maintaining order
                var allRecords = await _database.GetAllWithChildrenAsync<ClientWorkRecord>(recursive: true);

                // Filter and maintain order
                var orderedRecords = sortedIds
                    .Select(id => allRecords.FirstOrDefault(r => r.Id == id))
                    .Where(r => r != null)
                    .ToList();

                return orderedRecords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting client work records");
                return new List<ClientWorkRecord>();
            }
        }

        // Helper class for ID-only queries
        public class IdResult
        {
            public int Id { get; set; }
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
                _logger.LogInformation("Retrieving employees for work record {WorkRecordId}", workRecordId);
                return await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(
                 filter: ew => ew.WorkRecordId == workRecordId,
                 recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employees for work record {WorkRecordId}", workRecordId);
                return new List<EmployeeWorkRecord>();
            }
        }
        public async Task<List<Employee>> GetEmployeesForWorkRecordAsync(int workRecordId)
        {
            var workRecord = await _database.GetWithChildrenAsync<WorkRecord>(workRecordId, recursive: true);
            return workRecord?.Employees ?? new List<Employee>();
        }

        public async Task<int> AddEmployeeToWorkRecordAsync(int workRecordId, int employeeId)
        {
            await EnsureInitializedAsync();

            try
            {
                // Check if relationship already exists
                var existing = await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(
                    filter: ew => ew.WorkRecordId == workRecordId && ew.EmployeeId == employeeId);

                if (existing.Any())
                {
                    _logger.LogDebug($"Employee {employeeId} already assigned to work record {workRecordId}");
                    return existing.First().Id;
                }

                // Create new relationship with proper timestamp
                var joinRecord = new EmployeeWorkRecord
                {
                    WorkRecordId = workRecordId,
                    EmployeeId = employeeId
                };
                joinRecord.SetAddedDateToNow();

                await _database.InsertWithChildrenAsync(joinRecord);

                _logger.LogInformation($"Added employee {employeeId} to work record {workRecordId}");
                return joinRecord.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding employee {EmployeeId} to work record {WorkRecordId}",
                    employeeId, workRecordId);
                throw;
            }
        }
        public async Task<int> RemoveEmployeeFromWorkRecordAsync(int workRecordId, int employeeId)
        {
            await EnsureInitializedAsync();

            try
            {
                var joinRecords = await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(
                    filter: ew => ew.WorkRecordId == workRecordId && ew.EmployeeId == employeeId);

                if (joinRecords.Any())
                {
                    var joinRecord = joinRecords.First();
                    await _database.DeleteAsync(joinRecord, recursive: true);

                    _logger.LogInformation($"Removed employee {employeeId} from work record {workRecordId}");
                    return 1;
                }

                _logger.LogDebug($"No relationship found between employee {employeeId} and work record {workRecordId}");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing employee {EmployeeId} from work record {WorkRecordId}",
                    employeeId, workRecordId);
                throw;
            }
        }
        public async Task<List<WorkRecord>> GetWorkRecordsForEmployeeAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            await EnsureInitializedAsync();

            try
            {
                // First get the employee-workrecord relationships
                var employeeWorkRecords = await _database.Table<EmployeeWorkRecord>()
                    .Where(ewr => ewr.EmployeeId == employeeId)
                    .ToListAsync();

                if (!employeeWorkRecords.Any())
                    return new List<WorkRecord>();

                // Get the work records for these relationships
                var workRecordIds = employeeWorkRecords.Select(ewr => ewr.WorkRecordId).ToList();

                return await _database.Table<WorkRecord>()
                    .Where(wr =>
                        workRecordIds.Contains(wr.Id) &&
                        wr.WorkDate >= startDate &&
                        wr.WorkDate <= endDate)
                    .OrderByDescending(wr => wr.WorkDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting work records for employee {EmployeeId}", employeeId);
                return new List<WorkRecord>();
            }
        }



        public async Task<List<WorkRecord>> GetWorkRecordsAsync(int? clientId = null, int? jobId = null,
      DateTime? startDate = null, DateTime? endDate = null,
      bool? isCompleted = null, bool? isPaid = null, bool? isPaymentProcessed = null)
        {
            await EnsureInitializedAsync();

            try
            {
                // First get all records with children
                var allRecords = await _database.GetAllWithChildrenAsync<WorkRecord>(recursive: true);

                // Then apply filters in memory
                var filteredRecords = allRecords
                    .Where(wr =>
                        (!clientId.HasValue || wr.ClientId == clientId.Value) &&
                        (!jobId.HasValue || wr.JobId == jobId.Value) &&
                        (!startDate.HasValue || wr.WorkDate >= startDate.Value) &&
                        (!endDate.HasValue || wr.WorkDate <= endDate.Value) &&
                        (!isCompleted.HasValue || wr.IsJobCompleted == isCompleted.Value) &&
                        (!isPaid.HasValue || wr.IsPaid == isPaid.Value) &&
                        (!isPaymentProcessed.HasValue || wr.IsPaymentProcessed == isPaymentProcessed.Value))
                    .OrderByDescending(wr => wr.WorkDate)
                    .ToList();
                Debug.WriteLine($"Filtered work records count: {filteredRecords.Count}");

                return filteredRecords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered work records");
                return new List<WorkRecord>();
            }
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
            return await GetByIdWithChildrenAsync<WorkRecord>(id);
        }

        public async Task<int> SaveWorkRecordAsync(WorkRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            return await SaveAsync(record);
        }

        public async Task<int> SaveWorkRecordAndReturnIdAsync(WorkRecord workRecord)
        {
            if (workRecord == null)
            {
                _logger.LogError("Attempted to save a null work record");
                throw new ArgumentNullException(nameof(workRecord));
            }
            await EnsureInitializedAsync();
            try
            {
                _logger.LogDebug("Saving work record with ID {WorkRecordId}", workRecord.Id);
                var result = await SaveAsync(workRecord);

                if (result > 0)
                {
                    return workRecord.Id;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving work record with ID {WorkRecordId}", workRecord.Id);
                throw;
            }


        }



        #endregion

        #region EmployeeWorkRecord Relationship Methods
        public async Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsByWorkDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            await EnsureInitializedAsync();

            try
            {
                var allRecords = await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(recursive: true);

                return allRecords
                    .Where(ew => ew.WorkRecord != null &&
                                ew.WorkRecord.WorkDate >= startDate &&
                                ew.WorkRecord.WorkDate <= endDate)
                    .OrderByDescending(ew => ew.WorkRecord.WorkDate)
                    .ThenByDescending(ew => ew.AddedDateTicks)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee work records by work date range");
                return new List<EmployeeWorkRecord>();
            }
        }

  
        public async Task<List<Employee>> GetEmployeesForWorkRecordOrderedByAddedDateAsync(int workRecordId, bool mostRecentFirst = true)
        {
            await EnsureInitializedAsync();

            try
            {
                var employeeWorkRecords = await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(
                    filter: ew => ew.WorkRecordId == workRecordId,
                    recursive: true);

                var orderedRecords = mostRecentFirst
                    ? employeeWorkRecords.OrderByDescending(ew => ew.AddedDateTicks)
                    : employeeWorkRecords.OrderBy(ew => ew.AddedDateTicks);

                return orderedRecords.Select(ew => ew.Employee).Where(e => e != null).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees for work record ordered by added date");
                return new List<Employee>();
            }
        }


        public async Task<List<WorkRecord>> GetWorkRecordsForEmployeeOrderedByAddedDateAsync(int employeeId, bool mostRecentFirst = true)
        {
            await EnsureInitializedAsync();

            try
            {
                var employeeWorkRecords = await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(
                    filter: ew => ew.EmployeeId == employeeId,
                    recursive: true);

                var orderedRecords = mostRecentFirst
                    ? employeeWorkRecords.OrderByDescending(ew => ew.AddedDateTicks)
                    : employeeWorkRecords.OrderBy(ew => ew.AddedDateTicks);

                return orderedRecords.Select(ew => ew.WorkRecord).Where(wr => wr != null).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting work records for employee ordered by added date");
                return new List<WorkRecord>();
            }
        }



        #endregion

        #region Statistics and Reporting Methods
        public class EmployeeCompletionStats
        {
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; }
            public int CompletedJobsCount { get; set; }
            public decimal TotalEarnings { get; set; }
        }
        
        public async Task<Dictionary<string, object>> GetWorkRecordStatisticsAsync(DateTime startDate, DateTime endDate, int? employeeId = null)
        {
            await EnsureInitializedAsync();

            try
            {
                var records = await GetEmployeeWorkRecordsAsync(
                    startDate: startDate,
                    endDate: endDate,
                    employeeId: employeeId);

                return new Dictionary<string, object>
                {
                    ["TotalRecords"] = records.Count,
                    ["CompletedJobs"] = records.Count(r => r.WorkRecord?.IsJobCompleted == true),
                    ["PaidJobs"] = records.Count(r => r.WorkRecord?.IsPaid == true),
                    ["TotalEarnings"] = records.Sum(r => r.WorkRecord?.AmountPerEmployee ?? 0),
                    ["UniqueEmployees"] = records.Select(r => r.EmployeeId).Distinct().Count(),
                    ["UniqueWorkRecords"] = records.Select(r => r.WorkRecordId).Distinct().Count()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating work record statistics");
                return new Dictionary<string, object>();
            }
        }
        // In WorkRecordDatabaseService
public async Task<List<EmployeeCompletionStats>> GetEmployeeCompletionStatsAsync(
    DateTime startDate, 
    DateTime endDate,
    bool onlyActive = true)
{
    var query = @"
        SELECT 
            e.Id as EmployeeId,
            e.Name as EmployeeName,
            COUNT(ewr.WorkRecordId) as CompletedJobsCount,
            SUM(wr.AmountPerEmployee) as TotalEarnings
        FROM Employees e
        JOIN EmployeeWorkRecords ewr ON e.Id = ewr.EmployeeId
        JOIN WorkRecords wr ON ewr.WorkRecordId = wr.Id
        WHERE wr.IsJobCompleted = 1
        AND wr.CompletedDate BETWEEN ? AND ?
        AND (? = 0 OR e.IsActive = 1)
        GROUP BY e.Id, e.Name
        ORDER BY TotalEarnings DESC";
    
    return await _database.QueryAsync<EmployeeCompletionStats>(
        query, startDate, endDate, onlyActive ? 1 : 0);
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
                var workRecords = await _database.GetAllWithChildrenAsync<WorkRecord>(
                    filter: wr => wr.IsJobCompleted &&
                                 !wr.IsPaymentProcessed &&
                                 wr.CompletedDate >= startDate &&
                                 wr.CompletedDate <= endDate);

                return workRecords.Sum(wr => wr.TotalAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total accumulated revenue");
                return 0;
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

                var count = await _database.GetAllWithChildrenAsync<WorkRecord>(
                    filter: r => r.IsJobCompleted &&
                               !r.IsPaymentProcessed &&
                               r.CompletedDate >= startDate &&
                               r.CompletedDate <= endDate);

                return count.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting unprocessed completed jobs");
                return 0;
            }
        }

        #endregion


        public async Task<Dictionary<int, int>> GetEmployeeCountsForWorkRecordsAsync(List<int> workRecordIds)
        {
            await EnsureInitializedAsync();

            if (workRecordIds == null || !workRecordIds.Any())
                return new Dictionary<int, int>();

            try
            {
                // Create comma-separated list of IDs for SQL IN clause
                var idList = string.Join(",", workRecordIds);

                var query = $@"
            SELECT WorkRecordId, COUNT(*) as EmployeeCount 
            FROM EmployeeWorkRecords 
            WHERE WorkRecordId IN ({idList})
            GROUP BY WorkRecordId";

                var results = await _database.QueryAsync<EmployeeWorkRecordCount>(query);

                return results.ToDictionary(x => x.WorkRecordId, x => x.EmployeeCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee counts for work records");
                return new Dictionary<int, int>();
            }
        }

        private class EmployeeWorkRecordCount
        {
            public int WorkRecordId { get; set; }
            public int EmployeeCount { get; set; }
        }




        #region EmployeePayment Methods

        public async Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsWithDetailsByDateRangeAsync(DateTime startDate, DateTime endDate, int?
            employeeId = null, bool onlyCompleted = false, bool onlyPaid = false)
        {
            await EnsureInitializedAsync();

            try
            {
                var allRecords = await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(
                    filter: ew => ew.WorkRecord.WorkDate >= startDate &&
                                 ew.WorkRecord.WorkDate <= endDate &&
                                 (!employeeId.HasValue || ew.EmployeeId == employeeId.Value) &&
                                 (!onlyCompleted || ew.WorkRecord.IsJobCompleted) &&
                                 (!onlyPaid || ew.WorkRecord.IsPaid),
                    recursive: true);

                return allRecords
                    .OrderByDescending(ew => ew.WorkRecord.WorkDate)
                    .ThenBy(ew => ew.Employee.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee work records with details by date range");
                return new List<EmployeeWorkRecord>();
            }
        }

        public async Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsAsync(
    DateTime? startDate = null,
    DateTime? endDate = null,
    int? employeeId = null,
    int? workRecordId = null,
    bool? isCompleted = null,
    bool? isPaid = null,
    bool? isPaymentProcessed = null,
    int? limitResults = null,
    string orderBy = "WorkDate", // WorkDate, AddedDate, EmployeeName
    bool orderDescending = true)
        {
            await EnsureInitializedAsync();

            try
            {
                var records = await _database.GetAllWithChildrenAsync<EmployeeWorkRecord>(
                    filter: ew => (!startDate.HasValue || ew.WorkRecord.WorkDate >= startDate.Value) &&
                                 (!endDate.HasValue || ew.WorkRecord.WorkDate <= endDate.Value) &&
                                 (!employeeId.HasValue || ew.EmployeeId == employeeId.Value) &&
                                 (!workRecordId.HasValue || ew.WorkRecordId == workRecordId.Value) &&
                                 (!isCompleted.HasValue || ew.WorkRecord.IsJobCompleted == isCompleted.Value) &&
                                 (!isPaid.HasValue || ew.WorkRecord.IsPaid == isPaid.Value) &&
                                 (!isPaymentProcessed.HasValue || ew.WorkRecord.IsPaymentProcessed == isPaymentProcessed.Value),
                    recursive: true);

                // Apply ordering
                IOrderedEnumerable<EmployeeWorkRecord> orderedRecords = orderBy.ToLower() switch
                {
                    "addeddate" => orderDescending
                        ? records.OrderByDescending(ew => ew.AddedDateTicks)
                        : records.OrderBy(ew => ew.AddedDateTicks),
                    "employeename" => orderDescending
                        ? records.OrderByDescending(ew => ew.Employee?.Name)
                        : records.OrderBy(ew => ew.Employee?.Name),
                    _ => orderDescending // Default to WorkDate
                        ? records.OrderByDescending(ew => ew.WorkRecord?.WorkDate)
                        : records.OrderBy(ew => ew.WorkRecord?.WorkDate)
                };

                var result = orderedRecords.AsEnumerable();

                if (limitResults.HasValue)
                    result = result.Take(limitResults.Value);

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee work records with flexible filtering");
                return new List<EmployeeWorkRecord>();
            }
        }


        #endregion




        #region Helper Methods




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
/*
 * public async Task<List<WorkRecord>> GetWorkRecordsAsync(int? clientId = null, int? jobId = null,
    DateTime? startDate = null, DateTime? endDate = null,
    bool? isCompleted = null, bool? isPaid = null, bool? isPaymentProcessed = null)
{
    await EnsureInitializedAsync();

    try
    {
        // Build base query
        var query = "SELECT Id FROM WorkRecords WHERE 1=1";
        var parameters = new List<object>();

        if (clientId.HasValue)
        {
            query += " AND ClientId = ?";
            parameters.Add(clientId.Value);
        }
        
        if (jobId.HasValue)
        {
            query += " AND JobId = ?";
            parameters.Add(jobId.Value);
        }
        
        // Add other filters similarly...

        // Get matching IDs
        var matchingIds = await _database.QueryScalarsAsync<int>(query, parameters.ToArray());

        // Now load full records with children
        var records = new List<WorkRecord>();
        foreach (var id in matchingIds)
        {
            var record = await _database.GetWithChildrenAsync<WorkRecord>(id, recursive: true);
            if (record != null)
            {
                records.Add(record);
            }
        }

        return records
            .OrderByDescending(wr => wr.WorkDate)
            .ToList();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting filtered work records");
        return new List<WorkRecord>();
    }
}

public async Task<List<WorkRecord>> GetWorkRecordsAsync(int? clientId = null, int? jobId = null,
    DateTime? startDate = null, DateTime? endDate = null,
    bool? isCompleted = null, bool? isPaid = null, bool? isPaymentProcessed = null)
{
    await EnsureInitializedAsync();
    try
    {
        // First, get basic records with simple filtering
        var query = _database.Table<WorkRecord>();
        
        if (clientId.HasValue)
            query = query.Where(wr => wr.ClientId == clientId.Value);
        
        if (jobId.HasValue)
            query = query.Where(wr => wr.JobId == jobId.Value);
            
        if (startDate.HasValue)
            query = query.Where(wr => wr.WorkDate >= startDate.Value);
            
        if (endDate.HasValue)
            query = query.Where(wr => wr.WorkDate <= endDate.Value);
            
        if (isCompleted.HasValue)
            query = query.Where(wr => wr.IsJobCompleted == isCompleted.Value);
            
        if (isPaid.HasValue)
            query = query.Where(wr => wr.IsPaid == isPaid.Value);
            
        if (isPaymentProcessed.HasValue)
            query = query.Where(wr => wr.IsPaymentProcessed == isPaymentProcessed.Value);

        var basicRecords = await query.OrderByDescending(wr => wr.WorkDate).ToListAsync();
        
        // Then load children for each record
        var recordsWithChildren = new List<WorkRecord>();
        foreach (var record in basicRecords)
        {
            var recordWithChildren = await _database.GetWithChildrenAsync<WorkRecord>(record.Id, recursive: true);
            recordsWithChildren.Add(recordWithChildren);
        }
        
        return recordsWithChildren;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting filtered work records");
        return new List<WorkRecord>();
    }
}
public async Task<List<WorkRecord>> GetWorkRecordsAsync(int? clientId = null, int? jobId = null,
    DateTime? startDate = null, DateTime? endDate = null,
    bool? isCompleted = null, bool? isPaid = null, bool? isPaymentProcessed = null)
{
    await EnsureInitializedAsync();
    try
    {
        var sql = new StringBuilder("SELECT * FROM WorkRecords WHERE 1=1");
        var parameters = new List<object>();

        if (clientId.HasValue)
        {
            sql.Append(" AND ClientId = ?");
            parameters.Add(clientId.Value);
        }

        if (jobId.HasValue)
        {
            sql.Append(" AND JobId = ?");
            parameters.Add(jobId.Value);
        }

        if (startDate.HasValue)
        {
            sql.Append(" AND WorkDate >= ?");
            parameters.Add(startDate.Value);
        }

        if (endDate.HasValue)
        {
            sql.Append(" AND WorkDate <= ?");
            parameters.Add(endDate.Value);
        }

        if (isCompleted.HasValue)
        {
            sql.Append(" AND IsJobCompleted = ?");
            parameters.Add(isCompleted.Value);
        }

        if (isPaid.HasValue)
        {
            sql.Append(" AND IsPaid = ?");
            parameters.Add(isPaid.Value);
        }

        if (isPaymentProcessed.HasValue)
        {
            sql.Append(" AND IsPaymentProcessed = ?");
            parameters.Add(isPaymentProcessed.Value);
        }

        sql.Append(" ORDER BY WorkDate DESC");

        var basicRecords = await _database.QueryAsync<WorkRecord>(sql.ToString(), parameters.ToArray());
        
        // Load children for each record
        var recordsWithChildren = new List<WorkRecord>();
        foreach (var record in basicRecords)
        {
            var recordWithChildren = await _database.GetWithChildrenAsync<WorkRecord>(record.Id, recursive: true);
            recordsWithChildren.Add(recordWithChildren);
        }
        
        return recordsWithChildren;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting filtered work records");
        return new List<WorkRecord>();
    }
}
 */