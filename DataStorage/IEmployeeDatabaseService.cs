using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResearchApp.Models;

namespace ResearchApp.DataStorage
{
    public interface IEmployeeDatabaseService
    {
        Task<Employee> SaveEmployeeAsync(Employee employee);
        Task InitializeAsync();
        Task ResetDatabaseAsync();
        Task<bool> VerifyTableCreated<T>();
        Task<bool> UpdateEmployeeAsync(Employee employee);
        Task<int> GetTotalEmployeeCountAsync();
        Task<List<Employee>> GetAllEmployeesAsync();
        Task<List<Employee>> GetActiveEmployeesAsync(DateTime? referenceDate = null);
        Task<List<Employee>> GetEmployeesAsync(int skip = 0, int take = 20);
        Task<Employee> GetEmployeeByIdAsync(int id);
        Task<int> DeleteEmployeeAsync(Employee employee);
        Task AddEmployeeAsync(Employee employee);
        Task<int> SaveEmployeeWorkRecordAsync(EmployeeWorkRecord record);
        Task<List<EmployeeWorkRecord>> GetEmployeeWorkRecordsAsync(int employeeId);
        Task<List<WorkRecord>> GetWorkRecordsForEmployeeAsync(int employeeId);
    }
}
