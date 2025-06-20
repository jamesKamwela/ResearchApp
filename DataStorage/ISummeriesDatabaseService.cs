using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.DataStorage
{
    public interface  ISummeriesDatabaseService : IDisposable
    {

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

    }
}
