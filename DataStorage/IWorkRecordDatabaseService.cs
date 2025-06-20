using ResearchApp.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ResearchApp.DataStorage
{
    public interface IWorkRecordDatabaseService : IDisposable
    {
        // Initialization
        Task InitializeAsync();
        Task ResetDatabaseAsync();
        Task DropTableIfExistsAsync<T>() where T : new();
        // Core Database Operations
        Task<int> InsertAsync<T>(T entity) where T : new();
        Task<int> UpdateAsync<T>(T entity) where T : new();
        Task<int> SaveAsync<T>(T entity) where T : IEntity, new();
        Task<int> DeleteAsync<T>(T entity) where T : new();
        Task<T> GetByIdAsync<T>(int id) where T : IEntity, new();
        Task<List<T>> GetAllAsync<T>() where T : new();

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

        Task<List<WorkRecord>> GetWorkRecordsByPeriodAsync(
            string period = "Current Week",
            bool? isCompleted = null,
            bool? isPaymentProcessed = null,
            DateTime? customStartDate = null,
            DateTime? customEndDate = null);
        Task<List<EmployeeWorkRecord>> GetWorkRecordEmployeesAsync(int workRecordId);
        Task<List<WorkRecord>> GetWorkRecordsForEmployeeAsync(int employeeId, DateTime startDate, DateTime endDate);
        Task<WorkRecord> GetWorkRecordWithDetailsAsync(int id);
        Task<int> SaveWorkRecordAsync(WorkRecord record);
        Task<int> UpdateWorkRecord(WorkRecord record);
        Task<int> DeleteWorkRecordAsync(WorkRecord record);
        Task<int> GetUnprocessedCompletedJobsCountAsync(string period = "Current Week",
            DateTime? customStartDate = null, DateTime? customEndDate = null);
 



        // Calculations
        Task<decimal> GetCompletedJobsTotalAccumulatedRevenueAsync(
            string period = "Current Week",
            DateTime? customStartDate = null,
            DateTime? customEndDate = null);

       

        // Employee Payments
        Task<int> SaveEmployeePaymentAsync(EmployeePayment payment);
        Task<decimal> GetEmployeeEarningsTotalAsync(
            int employeeId,
            string period = "Current Week",
            DateTime? customStartDate = null,
            DateTime? customEndDate = null);


        // Utility
        (DateTime startDate, DateTime endDate) GetDateRange(string period);
    }
}