using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SQLite;
using System.Threading.Tasks;

namespace ResearchApp.Utils
{
    public static class AsyncTableQueryExtensions
    {
        public static async Task<bool> AnyAsync<T>(this AsyncTableQuery<T> query) where T : new()
        {
            int count = await query.CountAsync();
            return count > 0;
        }
    } 
}
