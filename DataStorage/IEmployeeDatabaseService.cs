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
        Task<List<Employee>> GetEmployeesAsync(int skip = 0, int take = 20);
        Task<Employee> GetEmployeeByIdAsync(int id);
        Task<int> DeleteEmployeeAsync(Employee employee);
        Task AddEmployeeAsync(Employee employee);
        Task ResetDatabaseAsync();
    }
}
