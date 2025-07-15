using ResearchApp.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ResearchApp.DataStorage.EmployeeDatabaseService;

namespace ResearchApp.DataStorage
{
    public interface IEmployeeDatabaseService
    {

        Task<List<T>> GetAllAsync<T>(bool loadChildren = false) where T : IEntity,new();

        Task<Employee> SaveEmployeeAsync(Employee employee);
        Task<int> SaveEmployeeWorkRecordAsync(EmployeeWorkRecord record, SQLiteConnection transaction = null);
        Task InitializeAsync();
        Task<bool> VerifyTableCreated<T>();
        Task<bool> UpdateEmployeeAsync(Employee employee);
        Task<int> GetTotalEmployeeCountAsync();
        Task<List<Employee>> GetAllEmployeesAsync();
        Task<List<int>> GetEmployeeWorkRecordsIdsAsync(int employeeId, string period = "Current Week");
        Task<List<Employee>> GetEmployeesPerPeriodAsync(bool includeDetails = false, CancellationToken cancellationToken = default, string period = "Current Week");
        Task<List<EmployeeRecords>> GetEmployeeWorkRecordMappingsStructuredAsync(string period = "Current Week");
        Task<List<Employee>> GetEmployeesAsync(int skip = 0, int take = 20);
        Task<List<EmployeeWorkRecord>> GetAllEmployeeWorkRecordsAsync();
        Task<Employee> GetEmployeeByIdAsync(int id);
        Task<int> DeleteEmployeeAsync(Employee employee);
        Task AddEmployeeAsync(Employee employee);
        Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsAsync(int employeeId);
        Task<List<WorkRecord>> GetWorkRecordsForEmployeeAsync(int employeeId);
    }
}
