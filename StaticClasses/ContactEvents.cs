using ResearchApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.StaticClasses
{
    public static class ContactEvents
    {
        public static event Action<Employee> EmployeeDeleted;
        public static event Action<Client> ClientDeleted;
        public static event Action<Employee>EmployeeAdded;
        public static event Action<Client> ClientAdded;

        public static void OnEmployeeDeleted(Employee employee)
        {
            EmployeeDeleted?.Invoke(employee);
        }
        public static void OnClientDeleted(Client client)
        {
            ClientDeleted?.Invoke(client);
        }

        public  static void OnEmployeeAdded(Employee employee)
        {
            EmployeeAdded?.Invoke(employee);
        }
        public static void OnClientAdded(Client client)
        {
            ClientAdded?.Invoke(client);
        }
    }
}
