using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static ResearchApp.DataStorage.WorkRecordDatabaseService;

namespace ResearchApp.ViewModels
{
    public partial class ClientWorkRecordsDataViewModel : ObservableObject, IDisposable
    {
        private readonly ILogger<ClientWorkRecordsDataViewModel> _logger;
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly IClientDatabaseService _clientDatabaseService;
        private readonly ITabNavigationHelper _tabNavigator;

        private readonly ClientCache _clientCache;

        public ClientWorkRecordsDataViewModel(
            ITabNavigationHelper tabNavigator,
            IClientDatabaseService clientDatabaseService,
            IWorkRecordDatabaseService workRecordDatabase,
            ILogger<ClientWorkRecordsDataViewModel> logger)
        {
            // Initialize properties or commands if needed
            _clientDatabaseService = clientDatabaseService;
            _workRecordDatabaseService = workRecordDatabase;
            Debug.WriteLine("Databases initialization complete");
            _clientCache = new ClientCache(clientDatabaseService);
            _tabNavigator = tabNavigator;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize the ClientStats collection
            ClientStats = new ObservableCollection<ClientCompletionStats>();

            Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Initializing LoadClientData");
                    await LoadClientData();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during initial data load");
                }
            });
        }

        public List<string> PeriodOptions { get; } = new List<string>
        {
            "Current Week",
            "Last Week",
            "Last Two Weeks",
            "Last Month",
            "Last 3 Months",
            "Last 6 Months",
            "Last Year",
            "Last 2 years",
            "Last 5 years",
            "Last 10 years"
        };

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _selectedPeriod = "Current Week";
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _hasData;
        [ObservableProperty] private decimal _totalJobs;
        [ObservableProperty] private decimal _totalRevenue;
        [ObservableProperty]
        private List<WorkRecord> _workRecords = new List<WorkRecord>();

        public ObservableCollection<ClientCompletionStats> ClientStats { get; set; }

        private async Task LoadClientData()
        {
            try
            {
                Debug.WriteLine("Loading client stats started");
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = true);

                // Get date range for selected period
                var (startDate, endDate) = GetDateRangeForPeriod(SelectedPeriod);
                Debug.WriteLine($"Period: {SelectedPeriod}, Date Range: {startDate:d} to {endDate:d}");

                // Get all completed work records for period
                Debug.WriteLine("Fetching completed work records...");
                var completedWorkRecords = await _workRecordDatabaseService.GetWorkRecordsAsync(
                    startDate: startDate,
                    endDate: endDate,
                    isCompleted: true);

                Debug.WriteLine($"Found {completedWorkRecords?.Count ?? 0} completed work records");

                if (completedWorkRecords == null || completedWorkRecords.Count == 0)
                {
                    Debug.WriteLine("No completed work records found");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ClientStats.Clear();
                        HasData = false;
                        StatusMessage = "No completed jobs found for period";
                    });
                    return;
                }

                // Create work record lookup
                var workRecordLookup = completedWorkRecords.ToDictionary(wr => wr.Id);

                // Get all client-workrecord relationships
                Debug.WriteLine("Fetching client-workrecord relationships...");
                var allClientWorkRecords = await _workRecordDatabaseService.GetClientWorkRecordsAsync(null, false, true, SelectedPeriod);

                // Group by client ID and filter to only include our completed work records
                var clientGroups = allClientWorkRecords?
                    .Where(cwr => workRecordLookup.ContainsKey(cwr.WorkRecordId))
                    .GroupBy(cwr => cwr.ClientId)
                    .ToList() ?? new List<IGrouping<int, ClientWorkRecord>>();

                Debug.WriteLine($"Found {clientGroups.Count} clients with work records");

                // Process each client group
                var clientStats = new List<ClientCompletionStats>();
                var processedClientIds = new HashSet<int>();

                foreach (var group in clientGroups)
                {
                    var clientId = group.Key;
                    if (processedClientIds.Contains(clientId)) continue;
                    processedClientIds.Add(clientId);

                    Debug.WriteLine($"Processing client ID: {clientId}");

                    // Get client details from cache
                    var client = await _clientCache.GetClientAsync(clientId);
                    if (client == null)
                    {
                        Debug.WriteLine($"Client {clientId} not found in cache");
                        continue;
                    }

                    // Get all work records for this client
                    var clientWorkRecords = group
                        .Select(cwr => workRecordLookup[cwr.WorkRecordId])
                        .ToList();

                    Debug.WriteLine($"Found {clientWorkRecords.Count} work records for {client.Name}");

                    clientStats.Add(new ClientCompletionStats
                    {
                        ClientId = clientId,
                        ClientName = client.Name,
                        CompletedJobsCount = clientWorkRecords.Count,
                        TotalEarnings = clientWorkRecords.Sum(wr => wr.TotalAmount)
                    });
                }

                // Update UI
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ClientStats.Clear();
                    foreach (var stat in clientStats.OrderByDescending(c => c.TotalEarnings))
                    {
                        ClientStats.Add(stat);
                    }

                    HasData = ClientStats.Any();
                    StatusMessage = HasData
                        ? $"Loaded stats for {ClientStats.Count} clients"
                        : "No client data found";
                    TotalJobs = ClientStats.Sum(c => c.CompletedJobsCount);
                    TotalRevenue = ClientStats.Sum(c => c.TotalEarnings);
                });

                Debug.WriteLine($"Completed loading stats for {clientStats.Count} clients");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading client stats: {ex}");
                _logger.LogError(ex, "Error loading client stats");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusMessage = "Error loading client stats";
                    HasData = false;
                });
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
        }

      
        partial void OnSelectedPeriodChanged(string value)
        {
            _logger.LogDebug($"Period changed to: {value}");
            _logger.LogDebug($"SelectedPeriod changed from {_selectedPeriod} to {value}");
            _logger.LogDebug($"Calling LoadClientData...");
            Task.Run(async () => await LoadClientData());
        }

        private (DateTime StartDate, DateTime EndDate) GetDateRangeForPeriod(string period)
        {
            var today = DateTime.Today;
            var currentDayOfWeek = (int)today.DayOfWeek;
            // Calculate days since Monday (0 for Monday, 1 for Tuesday, etc.)
            var daysSinceMonday = (currentDayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var thisWeekMonday = today.AddDays(-daysSinceMonday);
            var lastWeekMonday = thisWeekMonday.AddDays(-7);
            var lastWeekSunday = thisWeekMonday.AddDays(-1);

            var (startDate, endDate) = period switch
            {
                "Current Week" => (
                    thisWeekMonday,
                    thisWeekMonday.AddDays(6) // Sunday
                ),
                "Last Week" => (
                    lastWeekMonday,
                    lastWeekSunday
                ),
                "Last Two Weeks" => (
                    thisWeekMonday.AddDays(-14), // Monday two weeks ago
                    today // Include up to today
                ),
                "Last Month" => (
                    thisWeekMonday.AddDays(-28), // ~4 weeks ago
                    today // Include up to today
                ),
                "Last 3 Months" => (
                    today.AddMonths(-3),
                    today // Include up to today
                ),
                "Last 6 Months" => (
                    today.AddMonths(-6),
                    today // Include up to today
                ),
                "Last Year" => (
                    today.AddYears(-1),
                    today // Include up to today
                ),
                "Last 2 years" => (
                    today.AddYears(-2),
                    today),
                "Last 5 years" => (
                    today.AddYears(-5),
                    today),
                "Last 10 years" => (
                    today.AddYears(-10),
                    today),
                _ => (
                    thisWeekMonday,
                    thisWeekMonday.AddDays(6) // Default to current week
                )
            };

            // Ensure we don't go before minimum date (adjust if needed)
            if (startDate < new DateTime(1900, 1, 1))
            {
                startDate = new DateTime(1900, 1, 1);
            }

            _logger.LogDebug($"Date range for {period}: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            return (startDate, endDate);
        }

        [RelayCommand]
        private async Task ClientClicked(ClientCompletionStats client)
        {
            if (client == null)
            {
                Debug.WriteLine("No Client Found");
                return;
            }

            try
            {
                Debug.WriteLine($"Navigating to ClientJobsData for employee: {client.ClientName} (ID: {client.ClientId})");

                // Navigate to EmployeeJobsData page with employee details
                await Shell.Current.GoToAsync($"ClientJobsData?clientId={client.ClientId}&clientName={Uri.EscapeDataString(client.ClientName)}&period={Uri.EscapeDataString(SelectedPeriod)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error navigating to ClientJobsData for client {client.ClientName}");
                Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }
        public void Dispose()
        {
        }
    }
}