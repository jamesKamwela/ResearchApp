using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResearchApp.Models;

namespace ResearchApp.DataStorage
{
    public interface IJobDatabaseService
    {
        Task SaveJobAsync(Job job);
        Task<List<Job>> GetJobsAsync(int clientId);
        Task<int> DeleteJobAsync(Job job);
    }
}
