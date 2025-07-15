using ResearchApp.DataStorage;
using ResearchApp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ResearchApp.Helpers
{
    public class EmployeeCache
    {
        private readonly IEmployeeDatabaseService _employeeService;
        private Dictionary<int, Employee> _cache = new();
        private DateTime _lastRefresh = DateTime.MinValue;

        // Debugging counters
        private int _totalCacheHits;
        private int _totalCacheMisses;
        private int _totalRefreshes;


        public EmployeeCache(IEmployeeDatabaseService employeeService)
        {
            _employeeService = employeeService;
            Debug.WriteLine("EmployeeCache initialized");
        }

        public int CachedEmployeeCount => _cache.Count;
        public int TotalCacheHits => _totalCacheHits;
        public int TotalCacheMisses => _totalCacheMisses;
        public int TotalRefreshes => _totalRefreshes;
        public DateTime LastRefreshTime => _lastRefresh;

        public async Task RefreshAsync()
        {
            Debug.WriteLine($"Starting cache refresh (previous count: {_cache.Count})");
            var stopwatch = Stopwatch.StartNew();
            var employees = await _employeeService.GetAllEmployeesAsync();
            _cache = employees.ToDictionary(e => e.Id);
            _lastRefresh = DateTime.UtcNow;
            _totalRefreshes++;

            stopwatch.Stop();
            Debug.WriteLine($"Cache refreshed in {stopwatch.ElapsedMilliseconds}ms. " +
                           $"New count: {_cache.Count} employees");
        }

        public async Task<Employee> GetEmployeeAsync(int id)
        {
            Debug.WriteLine($"Requesting employee #{id}");

            if (_cache.TryGetValue(id, out var employee))
            {
                _totalCacheHits++;
                Debug.WriteLine($"Cache HIT for employee #{id}");
                return employee;
            }
            _totalCacheMisses++;
            Debug.WriteLine($"Cache MISS for employee #{id}, refreshing cache...");

            await RefreshAsync();

            if (_cache.TryGetValue(id, out employee))
            {
                Debug.WriteLine($"Found employee #{id} after refresh");
                return employee;
            }

            Debug.WriteLine($"Employee #{id} not found after refresh");
            return null;
        }

        public async Task<List<Employee>> GetEmployeesAsync(IEnumerable<int> ids)
        {
            var idList = ids.ToList();
            Debug.WriteLine($"Requesting {idList.Count} employees");

            var missingIds = idList.Where(id => !_cache.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                Debug.WriteLine($"Cache MISS for {missingIds.Count}/{idList.Count} employees, refreshing cache...");
                _totalCacheMisses += missingIds.Count;
                _totalCacheHits += idList.Count - missingIds.Count;

                await RefreshAsync();
            }
            else
            {
                _totalCacheHits += idList.Count;
                Debug.WriteLine($"Cache HIT for all {idList.Count} employees");
            }

            var result = idList.Select(id => _cache.TryGetValue(id, out var e) ? e : null)
                             .Where(e => e != null)
                             .ToList();

            Debug.WriteLine($"Returning {result.Count} employees");
            return result;
        }
        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            Debug.WriteLine("Getting all employees from cache");

            // If cache is empty or stale, refresh first
            if (_cache.Count == 0 || DateTime.UtcNow - _lastRefresh > TimeSpan.FromMinutes(5))
            {
                Debug.WriteLine("Cache empty or stale, refreshing...");
                await RefreshAsync();
            }

            return _cache.Values.ToList();
        }
        public void LogCacheStatistics()
        {
            Debug.WriteLine($"Employee Cache Statistics:");
            Debug.WriteLine($"- Current cached employees: {CachedEmployeeCount}");
            Debug.WriteLine($"- Last refresh time: {LastRefreshTime:yyyy-MM-dd HH:mm:ss}");
            Debug.WriteLine($"- Total refreshes: {TotalRefreshes}");
            Debug.WriteLine($"- Cache hits: {TotalCacheHits}");
            Debug.WriteLine($"- Cache misses: {TotalCacheMisses}");
            Debug.WriteLine($"- Hit ratio: {((TotalCacheHits + TotalCacheMisses) > 0 ?
    (double)TotalCacheHits / (TotalCacheHits + TotalCacheMisses) * 100 : 0):N2}%");


        }
    }
}
