using ResearchApp.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static ResearchApp.DataStorage.WorkRecordDatabaseService;

namespace ResearchApp.DataStorage
{
    public interface IWorkRecordDatabaseService : IDisposable
    {
        // Initialization
        Task InitializeAsync();
        // Core Database Operations
        Task<int> InsertAsync<T>(T entity) where T : new();
        Task<int> UpdateAsync<T>(T entity) where T : new();
        Task<int> SaveAsync<T>(T entity) where T : IEntity, new();
        Task<int> DeleteAsync<T>(T entity) where T : new();
        Task<T> GetByIdAsync<T>(int id) where T : IEntity, new();
       Task<List<T>> GetByIdsAsync<T>(List<int> ids) where T : IEntity, new();
      
                Task<List<T>> GetAllAsync<T>(bool loadChildren = false) where T : new();

        // Generic Query Methods
        Task<List<T>> GetFilteredAsync<T>(
            Expression<Func<T, bool>> predicate = null,
            Func<AsyncTableQuery<T>, AsyncTableQuery<T>> queryModifier = null)
            where T : new();

        Task<int> GetCountAsync<T>(Expression<Func<T, bool>> predicate = null)
            where T : new();

        // WorkRecord Operations
        Task<List<WorkRecord>> GetWorkRecordsAsync(
            int? clientId = null,
            int? jobId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            bool? isCompleted = null,
            bool? isPaid = null,
            bool? isPaymentProcessed = null);

        Task<List<WorkRecord>> GetWorkRecordsByPeriodAsync( string period = "Current Week", bool? isCompleted = null, bool? isPaymentProcessed = null,DateTime? customStartDate = null,DateTime? customEndDate = null);
        Task<List<EmployeeWorkRecord>> GetWorkRecordEmployeesAsync(int workRecordId);
        Task<bool> DeleteWorkRecordByIdAsync(int workRecordId);
        Task<List<Employee>> GetEmployeesByWorkRecordIdAsync(int workRecordId);
        Task<List<WorkRecord>> GetWorkRecordsForEmployeeAsync(int employeeId, DateTime startDate, DateTime endDate);
        Task<int> AddEmployeeToWorkRecordAsync(int workRecordId, int employeeId);
        Task<List<Employee>> GetEmployeesForWorkRecordAsync(int workRecordId);
        Task<int> SaveWorkRecordAndReturnIdAsync(WorkRecord workRecord);
        Task<List<WorkRecord>> GetWorkRecordsByIdsAsync(List<int> workRecordIds, bool loadChildren = false, bool? completedOnly = null,
            string period = "Current Week",DateTime? customStartDate = null,DateTime? customEndDate = null);
        Task<int> RemoveEmployeeFromWorkRecordAsync(int workRecordId, int employeeId);
        Task<Dictionary<int, int>> GetEmployeeCountsForWorkRecordsAsync(List<int> workRecordIds);
        Task<List<Employee>> GetEmployeesForWorkRecordOrderedByAddedDateAsync(int workRecordId,bool mostRecentFirst = true);
        Task<Dictionary<string, object>> GetWorkRecordStatisticsAsync(DateTime startDate, DateTime endDate, int? employeeId = null);
        Task<List<WorkRecord>> GetWorkRecordsForEmployeeOrderedByAddedDateAsync(int employeeId, bool mostRecentFirst = true);
        Task<T> GetByIdWithChildrenAsync<T>(int id, bool recursive = true) where T : IEntity, new();
        Task<WorkRecord> GetWorkRecordWithDetailsAsync(int id);
        Task<int> SaveWorkRecordAsync(WorkRecord record);
        Task<bool> UpdateWorkRecord(WorkRecord record);
        // Task<int> SaveWorkRecordWithEmployeesInTransactionAsync(WorkRecord workRecord, List<EmployeeWorkRecord> employeeWorkRecords);
        Task<int> SaveWorkRecordWithEmployeesInTransactionAsync(WorkRecord workRecord, List<EmployeeWorkRecord> employeeWorkRecords,
             ClientWorkRecord clientWorkRecord = null);
        Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsByWorkDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsAsync(
       DateTime? startDate = null,
       DateTime? endDate = null,
       int? employeeId = null,
       int? workRecordId = null,
       bool? isCompleted = null,
       bool? isPaid = null,
       bool? isPaymentProcessed = null,
       int? limitResults = null,
       string orderBy = "WorkDate", // WorkDate, AddedDate, EmployeeName
       bool orderDescending = true);
        Task<int> GetUnprocessedCompletedJobsCountAsync(string period = "Current Week",
            DateTime? customStartDate = null, DateTime? customEndDate = null);

        Task<List<EmployeeCompletionStats>> GetEmployeeCompletionStatsAsync(DateTime startDate,DateTime endDate,bool onlyActive = true);

        // Calculations
        Task<decimal> GetCompletedJobsTotalAccumulatedRevenueAsync(
            string period = "Current Week",
            DateTime? customStartDate = null,
            DateTime? customEndDate = null);

        //client work records
        Task<List<ClientWorkRecord>> GetAllClientWorkRecordsAsync(bool isComplete, bool isPaid, string period = "Current Week");



        // Utility
        (DateTime startDate, DateTime endDate) GetDateRange(string period);

        //client
        Task<List<ClientWorkRecord>> GetClientWorkRecordsAsync(int? clientId, bool isPaid, bool isComplete, string period = "Current Week");
    }
}