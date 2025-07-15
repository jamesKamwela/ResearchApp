using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static ResearchApp.DataStorage.WorkRecordDatabaseService;

namespace ResearchApp.ViewModels
{
    public partial class ActiveEmployeesViewModel : ObservableObject, IDisposable
    {
        private readonly IEmployeeDatabaseService _employeeDatabaseService;
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly ITabNavigationHelper _tabNavigator;
        private readonly ILogger<ActiveEmployeesViewModel> _logger;
        private readonly EmployeeCache _employeeCache;

        public ActiveEmployeesViewModel(
            IEmployeeDatabaseService employeeDatabaseService,
            ITabNavigationHelper tabNavigator,
            IWorkRecordDatabaseService workRecordDatabaseService,
            ILogger<ActiveEmployeesViewModel> logger)
        {
            _logger = logger;
            _logger.LogDebug("=== ActiveEmployeesViewModel Constructor START ===");

            _employeeDatabaseService = employeeDatabaseService;
            _workRecordDatabaseService = workRecordDatabaseService;
            _tabNavigator = tabNavigator;
            // Initialize EmployeeCache
            _employeeCache = new EmployeeCache(_employeeDatabaseService);
            _logger.LogDebug("EmployeeCache initialized");


            _logger.LogDebug("Setting up MessagingCenter subscription...");
            MessagingCenter.Subscribe<PendingJobsViewModel, WorkRecord>(this, "WorkRecordSaved",
                async (sender, updatedRecord) => {
                    _logger.LogDebug("WorkRecordSaved message received, reloading data...");
                    await LoadEmployeeStatsAsync();
                });
            // Initialize the ObservableCollection
            EmployeeStats = new ObservableCollection<EmployeeCompletionStats>();

            _logger.LogDebug("Dependencies injected and EmployeeCache created");
            _logger.LogDebug("Starting background task for initial data load...");
            Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Initializing LoadEmployeeStatsAsync");
                    await LoadEmployeeStatsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during initial data load");
                }
            });

        }

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _selectedPeriod = "Current Week";
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _hasData;
        [ObservableProperty] private decimal _totalJobs;
        [ObservableProperty] private decimal _totalRevenue;
        [ObservableProperty]
        private List<WorkRecord> _workRecords = new List<WorkRecord>();

     

        // Change this to ObservableCollection for better UI binding
        public ObservableCollection<EmployeeCompletionStats> EmployeeStats { get; set; }

        [ObservableProperty] private int _totalEmployees ;

        public List<string> PeriodOptions { get; } = new List<string>
        {
            "Current Week",
            "Last Week",
            "Last Two Weeks",
            "Last Month",
            "Last 3 Months",
            "Last 6 Months",
            "Last Year"
        };
        [RelayCommand]
        private async Task LoadEmployeeStatsAsync()
        {
            try
            {
                Debug.WriteLine("Loading employee stats started");
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

                if (completedWorkRecords == null || completedWorkRecords.Count == 0)
                {
                    Debug.WriteLine("No completed work records found");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        EmployeeStats.Clear();
                        HasData = false;
                        StatusMessage = "No completed jobs found for period";
                    });
                    return;
                }

                // Create work record lookup
                var workRecordLookup = completedWorkRecords.ToDictionary(wr => wr.Id);

                // Get all employee-workrecord relationships
                Debug.WriteLine("Fetching employee-workrecord relationships...");
                var allEmployeeWorkRecords = await _workRecordDatabaseService.GetEmployeeWorkRecordsByWorkDateRangeAsync(startDate,endDate);

                // Group by employee ID and filter to only include our completed work records
                var employeeGroups = allEmployeeWorkRecords?
                    .Where(ewr => workRecordLookup.ContainsKey(ewr.WorkRecordId))
                    .GroupBy(ewr => ewr.EmployeeId)
                    .ToList() ?? new List<IGrouping<int, EmployeeWorkRecord>>();

                Debug.WriteLine($"Found {employeeGroups.Count} employees with work records");

                // Process each employee group
                var employeeStats = new List<EmployeeCompletionStats>();
                var processedEmployeeIds = new HashSet<int>();

                foreach (var group in employeeGroups)
                {
                    var employeeId = group.Key;
                    if (processedEmployeeIds.Contains(employeeId)) continue;
                    processedEmployeeIds.Add(employeeId);

                    Debug.WriteLine($"Processing employee ID: {employeeId}");

                    // Get employee details from cache
                    var employee = await _employeeCache.GetEmployeeAsync(employeeId);
                    if (employee == null)
                    {
                        Debug.WriteLine($"Employee {employeeId} not found in cache");
                        continue;
                    }

                    // Get all work records for this employee
                    var employeeWorkRecords = group
                        .Select(ewr => workRecordLookup[ewr.WorkRecordId])
                        .ToList();

                    Debug.WriteLine($"Found {employeeWorkRecords.Count} work records for {employee.Name}");

                    employeeStats.Add(new EmployeeCompletionStats
                    {
                        EmployeeId = employeeId,
                        EmployeeName = employee.Name,
                        CompletedJobsCount = employeeWorkRecords.Count,
                        TotalEarnings = employeeWorkRecords.Sum(wr => wr.AmountPerEmployee)
                    });
                }

                // Update UI
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    EmployeeStats.Clear();
                    foreach (var stat in employeeStats.OrderByDescending(e => e.TotalEarnings))
                    {
                        EmployeeStats.Add(stat);
                    }
                    TotalEmployees = employeeStats.Count;
                    HasData = EmployeeStats.Any();
                    StatusMessage = HasData
                        ? $"Loaded stats for {EmployeeStats.Count} employees"
                        : "No employee data found";
                    TotalJobs = EmployeeStats.Sum(e => e.CompletedJobsCount);
                    TotalRevenue = EmployeeStats.Sum(e => e.TotalEarnings);
                });

                Debug.WriteLine($"Completed loading stats for {employeeStats.Count} employees");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading employee stats: {ex}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusMessage = "Error loading employee stats";
                    HasData = false;
                });
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
        }

        [ObservableProperty]
        private ContactDisplayModel _selectedEmployee;

        [RelayCommand]
        private async Task EmployeeClicked(EmployeeCompletionStats employee)
        {
            if (employee == null)
            {
                Debug.WriteLine("No Employee Found");
                return;
            }

            try
            {
                Debug.WriteLine($"Navigating to EmployeeJobsData for employee: {employee.EmployeeName} (ID: {employee.EmployeeId})");

                // Navigate to EmployeeJobsData page with employee details
                await Shell.Current.GoToAsync($"EmployeeJobsData?employeeId={employee.EmployeeId}&employeeName={Uri.EscapeDataString(employee.EmployeeName)}&period={Uri.EscapeDataString(SelectedPeriod)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error navigating to EmployeeJobsData for employee {employee.EmployeeName}");
                Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }

        partial void OnSelectedPeriodChanged(string value)
        {
            _logger.LogDebug($"Period changed to: {value}");
            _logger.LogDebug($"SelectedPeriod changed from {_selectedPeriod} to {value}");
            _logger.LogDebug($"Calling LoadEmployeeStatsAsync...");
            Task.Run(async () => await LoadEmployeeStatsAsync());
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

        public void Dispose()
        {
            MessagingCenter.Unsubscribe<PendingJobsViewModel, WorkRecord>(this, "WorkRecordSaved");
        }
    }

    public class EmployeeStatsViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public int JobCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<int> WorkRecordIds { get; set; } = new List<int>();
    }
}