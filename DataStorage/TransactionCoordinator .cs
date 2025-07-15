using Microsoft.Extensions.Logging;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.DataStorage
{
    public interface ITransactionCoordinator
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation);
        Task ExecuteInTransactionAsync(Func<Task> operation);
    }

    public class TransactionCoordinator : ITransactionCoordinator
    {
        private readonly SQLiteAsyncConnection _connection;
        private readonly ILogger<TransactionCoordinator> _logger;

        public TransactionCoordinator(SQLiteAsyncConnection connection, ILogger<TransactionCoordinator> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            T result = default;
            await _connection.RunInTransactionAsync(async (tran) =>
            {
                result = await operation();
            });
            return result;
        }

        public async Task ExecuteInTransactionAsync(Func<Task> operation)
        {
            await _connection.RunInTransactionAsync(async (tran) =>
            {
                await operation();
            });
        }
    }
}
