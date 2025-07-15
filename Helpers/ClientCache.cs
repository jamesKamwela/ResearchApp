using ResearchApp.DataStorage;
using ResearchApp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace ResearchApp.Helpers
{
    public class ClientCache{
        private readonly IClientDatabaseService clientDatabaseService;
        private Dictionary<int, Client> _cache = new();
        private DateTime _lastRefresh = DateTime.MinValue;

        // Debugging counters
        private int _totalCacheHits;
        private int _totalCacheMisses;
        private int _totalRefreshes;

        public ClientCache(IClientDatabaseService clientDatabaseService)
        {
            this.clientDatabaseService = clientDatabaseService;
            Debug.WriteLine("ClientCache initialized");

        }

        public int CachedClientCount => _cache.Count;
        public int TotalCacheHits => _totalCacheHits;
        public int TotalCacheMisses => _totalCacheMisses;
        public int TotalRefreshes => _totalRefreshes;
        public DateTime LastRefreshTime => _lastRefresh;

        public async Task RefreshAsync()
        {
            Debug.WriteLine($"Starting cache refresh (previous count: {_cache.Count})");
            var stopwatch = Stopwatch.StartNew();
            var clients = await clientDatabaseService.GetAllClientsMinusJobsAsync();
            _cache = clients.ToDictionary(e => e.Id);
            _lastRefresh = DateTime.UtcNow;
            _totalRefreshes++;

            stopwatch.Stop();
            Debug.WriteLine($"Cache refreshed in {stopwatch.ElapsedMilliseconds}ms. " +
                           $"New count: {_cache.Count} clients");
        }

        public async Task<Client> GetClientAsync(int id)
        {
            Debug.WriteLine($"Requesting client #{id}");

            if (_cache.TryGetValue(id, out var client))
            {
                _totalCacheHits++;
                Debug.WriteLine($"Cache HIT for client #{id}");
                return client;
            }
            _totalCacheMisses++;
            Debug.WriteLine($"Cache MISS for client #{id}, refreshing cache...");

            await RefreshAsync();

            if (_cache.TryGetValue(id, out client))
            {
                Debug.WriteLine($"Found client #{id} after refresh");
                return client;
            }

            Debug.WriteLine($" Client #{id} not found after refresh");
            return null;

        }

        public async Task<List<Client>> GetAllClientsAsync()
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
    }
}
