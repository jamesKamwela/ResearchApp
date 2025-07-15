using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResearchApp.Models;

namespace ResearchApp.DataStorage
{
    public interface IClientDatabaseService
    {
       
        
        // Client operations
        Task<Client> GetLastClientAsync();
        Task<Client?> GetClientByDetailsAsync(string name, string phone, string address);
        Task<Job> GetJobByIdAsync(int jobId);


        Task<int> GetTotalClientCountAsync();
        Task<List<Client>> GetAllClientsMinusJobsAsync();
        Task<bool> UpdateClientAsync(Client client);
        Task<Client> GetClientMinusJobsWithId(int clientId);

        Task<bool> UpdateJobAsync(Job job);
        Task<int> DeleteJobAsync(Job job);
        Task<Client> SaveClientAsync(Client client);
        Task<List<Client>> GetClientsAsync();
        Task<List<Client>> GetClientsWithoutJobsAsync(int skip = 0, int take = 10);
        Task<Client> GetClientByIdAsync(int id);
        Task<int> DeleteClientAsync(Client client);
        Task ResetDatabaseAsync();

        // Job operations
        Task SaveJobsAsync(List<Job> jobs);
        Task<List<Job>> GetJobsByClientIdAsync(int clientId);

    }
}
