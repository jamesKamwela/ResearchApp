using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.Models;
using ResearchApp.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace ResearchApp.ViewModels
{
    public partial class ActiveEmployeesViewModel : ObservableObject
    {
        private readonly IEmployeeDatabaseService _employeeDatabaseService;
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly ITabNavigationHelper _tabNavigator;
        private readonly ILogger<ActiveEmployeesViewModel> _logger;
       private readonly ILogger<EmployeeWeeklyJobsList> _employeeWeeklyJobsListLogger;
        private readonly EmployeeCache _employeeCache;

        public ActiveEmployeesViewModel(
                                           IEmployeeDatabaseService employeeDatabaseService,
                                           ITabNavigationHelper tab1Navigator,
                                           IWorkRecordDatabaseService workRecordDatabaseService,
                                           ILogger<ActiveEmployeesViewModel> logger, ILogger<EmployeeWeeklyJobsList>employeeWeeklyJobsListLogger)
        {
            try
            {
                _logger = logger;
                _logger.LogInformation("Initializing ActiveEmployeesViewModel...");
                _employeeDatabaseService = employeeDatabaseService;
                _workRecordDatabaseService = workRecordDatabaseService;
                _tabNavigator = tab1Navigator;
                _employeeCache = new EmployeeCache(employeeDatabaseService);
                _employeeWeeklyJobsListLogger = employeeWeeklyJobsListLogger;

              /*  _logger.LogInformation("Starting Reset");
                // DEBUG: Reset database - Comment this out after use
                 Task.Run(async () => await ResetDatabaseAsync());
                _logger.LogInformation(" Reset Complete");*/


                _logger.LogInformation("ViewModel initialized successfully");
                _logger.LogDebug("Services initialized: EmployeeDB: {EmployeeDB}, WorkRecordDB: {WorkRecordDB}",
                                  employeeDatabaseService != null, workRecordDatabaseService != null);

                MessagingCenter.Subscribe<PendingJobsViewModel, WorkRecord>(this, "WorkRecordSaved",
                    async (sender, updatedRecord) =>
                    {
                        await LoadDataAsync();
                        _logger.LogInformation($"Received update for work record {updatedRecord?.Id}");
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ActiveEmployeesViewModel constructor");
                Console.WriteLine($"CONSTRUCTOR ERROR: {ex}");
                throw;
            }
        }

     

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _selectedPeriod = "Current Week";

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _hasData;
        public ObservableCollection<Employee> ActiveEmployeesList { get; private set; } = new ObservableCollection<Employee>();
        public ObservableCollection<EmployeePeriodSummary> EmployeePeriodSummaries { get; private set; } 
            = new ObservableCollection<EmployeePeriodSummary>();

        public List<string> PeriodOptions { get; } = new List<string>{
            "Current Week",
            "Last Week",
            "Last Two Weeks",
            "Last Month",
            "Last 3 Months",
            "Last 6 Months",
            "Last Year"
             };

        // Computed properties for summary statistics
        public int TotalEmployees => EmployeePeriodSummaries?.Count ?? 0;
        public decimal TotalEarnings => EmployeePeriodSummaries?.Sum(e => e.Earnings) ?? 0;

        [ObservableProperty]
        public decimal _totalJobs;

        [ObservableProperty]
        private decimal _totalRevenue;

        [RelayCommand]
        private async Task EmployeeSelected(EmployeePeriodSummary selectedSummary)
        {
            if (selectedSummary == null) return;

            try
            {
                // Create a simple dictionary with the employee ID
                var parameters = new Dictionary<string, object>
        {
            { "EmployeeId", selectedSummary.EmployeeId }
        };

                await Shell.Current.GoToAsync("EmployeeWeeklyJobsList", parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation failed");
            }
        }
        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting data load...");
                IsLoading = true;
                StatusMessage = "Loading employee data...";

                _logger.LogDebug("Refreshing employee cache...");
                await _employeeCache.RefreshAsync();
                _logger.LogInformation("Cache refreshed. Employee count: {EmployeeCount}", _employeeCache.CachedEmployeeCount);

                _logger.LogDebug("Loading period summary for: {Period}", SelectedPeriod);
                var summaries = await GetEmployeePeriodSummaryAsync();
                _logger.LogInformation("Retrieved {SummaryCount} period summaries", summaries?.Count ?? 0);

                // Get both count and revenue in parallel
                var countTask = _workRecordDatabaseService.GetUnprocessedCompletedJobsCountAsync(SelectedPeriod);
                var revenueTask = _workRecordDatabaseService.GetCompletedJobsTotalAccumulatedRevenueAsync(SelectedPeriod);

                await Task.WhenAll(countTask, revenueTask);

                TotalJobs = await countTask;
                TotalRevenue = await revenueTask;

                _logger.LogInformation(
                    "Found {TotalJobs} unprocessed jobs totaling {TotalRevenue:C} for period: {Period}",
                    TotalJobs, TotalRevenue, SelectedPeriod);

                // Wait for UI update to complete, then set HasData
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HasData = EmployeePeriodSummaries.Any();
                    StatusMessage = HasData ? $"Loaded {TotalEmployees} employees" : "No data available for selected period";
                });

                // Notify UI of computed property changes
                OnPropertyChanged(nameof(TotalEmployees));
                OnPropertyChanged(nameof(TotalJobs));
                OnPropertyChanged(nameof(TotalEarnings));
                HasData = EmployeePeriodSummaries.Any();

                _logger.LogInformation("Data load completed successfully. Stats - Employees: {TotalEmployees}, Jobs: {TotalJobs}, Earnings: {TotalEarnings}, Revenue: {TotalRevenue}",
                    TotalEmployees, TotalJobs, TotalEarnings, TotalRevenue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load data");
                StatusMessage = "Error loading data. Please try again.";
                TotalJobs = 0;
                TotalRevenue = 0;
                HasData = false;
            }
            finally
            {
                IsLoading = false;
                _logger.LogDebug("Load operation completed. IsLoading set to false");
            }
        }

        [RelayCommand]
        private async Task PeriodChangedAsync()
        {
            if (!string.IsNullOrEmpty(SelectedPeriod))
            {
                await GetEmployeePeriodSummaryAsync();
                await LoadDataAsync();
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            await LoadDataAsync();
            await ClearAndRefreshSummaries();

        }

        public async Task<List<EmployeePeriodSummary>> GetEmployeePeriodSummaryAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedPeriod))
                {
                    _logger.LogWarning("SelectedPeriod is null or empty");
                    return new List<EmployeePeriodSummary>();
                }

                _logger.LogDebug("Getting date range for period: {Period}", SelectedPeriod);
                var (startDate, endDate) = GetDateRangeForPeriod(SelectedPeriod);
                _logger.LogDebug("Date range: {StartDate} to {EndDate}", startDate, endDate);

                _logger.LogDebug("Retrieving completed jobs from database...");
                var jobs = await _workRecordDatabaseService.GetWorkRecordsByPeriodAsync(
                    customStartDate: startDate,
                    customEndDate: endDate,
                    isCompleted: true
                  );

                _logger.LogInformation("Retrieved {JobCount} completed jobs from database", jobs?.Count ?? 0);

                if (jobs == null || !jobs.Any())
                {
                    _logger.LogWarning("No completed jobs found for the selected period");

                    // Clear collections on main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        EmployeePeriodSummaries.Clear();
                        ActiveEmployeesList.Clear();
                    });

                    return new List<EmployeePeriodSummary>();
                }

                // Get all unique employee IDs from all jobs
                var employeeIds = jobs
                    .SelectMany(job => job.GetEmployeeIdList())
                    .Distinct()
                    .ToList();

                _logger.LogDebug("Found {EmployeeCount} unique employee IDs in jobs", employeeIds.Count);

                if (!employeeIds.Any())
                {
                    _logger.LogWarning("No employee IDs found in jobs");

                    // Clear collections on main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        EmployeePeriodSummaries.Clear();
                        ActiveEmployeesList.Clear();
                    });

                    return new List<EmployeePeriodSummary>();
                }

                // Fetch all employees from cache
                _logger.LogDebug("Loading employee details from cache...");
                var employees = await _employeeCache.GetEmployeesAsync(employeeIds);
                _logger.LogInformation("Retrieved {EmployeeCount} employees from cache", employees.Count);

                if (!employees.Any())
                {
                    _logger.LogWarning("No employees found in cache for the given IDs");

                    // Clear collections on main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        EmployeePeriodSummaries.Clear();
                        ActiveEmployeesList.Clear();
                    });

                    return new List<EmployeePeriodSummary>();
                }

                // Create summaries for each employee with their jobs
                var summaries = new List<EmployeePeriodSummary>();

                foreach (var employee in employees)
                {
                    var employeeJobs = jobs
                        .Where(j => j.GetEmployeeIdList().Contains(employee.Id))
                        .ToList();

                    if (employeeJobs.Count > 0)
                    {
                        var summary = new EmployeePeriodSummary
                        {
                            EmployeeId = employee.Id,
                            Name = employee.Name,
                            JobCount = employeeJobs.Count,
                            Earnings = employeeJobs.Sum(j => j.AmountPerEmployee),
                            Period = SelectedPeriod,
                            Employee = employee
                        };

                        summaries.Add(summary);
                        _logger.LogDebug("Created summary for employee {EmployeeName} - Jobs: {JobCount}, Earnings: {Earnings}",
                            employee.Name, summary.JobCount, summary.Earnings);
                    }
                }

                _logger.LogDebug("Created {SummaryCount} summaries with valid jobs", summaries.Count);

                // Order by earnings descending, then by job count
                var orderedSummaries = summaries
                    .OrderByDescending(s => s.Earnings)
                    .ThenByDescending(s => s.JobCount)
                    .ToList();

                // DEBUG: Log detailed summary information
                _logger.LogInformation("=== EMPLOYEE SUMMARIES DEBUG LIST ===");
                _logger.LogInformation("Period: {Period}", SelectedPeriod);
                _logger.LogInformation("Total Employees: {Count}", orderedSummaries.Count);
                _logger.LogInformation("----------------------------------------");

                for (int i = 0; i < orderedSummaries.Count; i++)
                {
                    var summary = orderedSummaries[i];
                    _logger.LogInformation("#{Index}: {Name} | Jobs: {JobCount} | Earnings: {Earnings:C}",
                        i + 1, summary.Name, summary.JobCount, summary.Earnings);
                }
                _logger.LogInformation("========================================");

                // IMPORTANT: Assign row indices for alternating row colors
                for (int i = 0; i < orderedSummaries.Count; i++)
                {
                    orderedSummaries[i].Row = i;
                }

                // Update UI collections on main thread and wait for completion
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _logger.LogDebug("Updating UI collections on main thread...");

                    EmployeePeriodSummaries.Clear();
                    foreach (var summary in orderedSummaries)
                    {
                        EmployeePeriodSummaries.Add(summary);
                    }

                    ActiveEmployeesList.Clear();
                    var activeEmployees = employees.Where(e => orderedSummaries.Any(s => s.EmployeeId == e.Id)).ToList();
                    foreach (var employee in activeEmployees)
                    {
                        ActiveEmployeesList.Add(employee);
                    }
                    _logger.LogInformation($"UI Collection Count: {EmployeePeriodSummaries.Count}");
                    foreach (var item in EmployeePeriodSummaries)
                    {
                        _logger.LogInformation($"Item: {item.Name}, Jobs: {item.JobCount}, Earnings: {item.Earnings}");
                    }

                    _logger.LogInformation("UI collections updated - Employees: {EmployeeCount}, Summaries: {SummaryCount}",
                        ActiveEmployeesList.Count, EmployeePeriodSummaries.Count);
                    _logger.LogInformation("UI collections updated - Employees: {EmployeeCount}, Summaries: {SummaryCount}",
                        ActiveEmployeesList.Count, EmployeePeriodSummaries.Count);
                });

                return orderedSummaries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get employee period summary for period: {Period}", SelectedPeriod);

                // Clear collections on error
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    EmployeePeriodSummaries.Clear();
                    ActiveEmployeesList.Clear();
                });

                throw;
            }
        }
        private (DateTime StartDate, DateTime EndDate) GetDateRangeForPeriod(string period)
        {
            var today = DateTime.Today;
            return period switch
            {
                "Current Week" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(6 - (int)today.DayOfWeek)),
                "Last Week" => (today.AddDays(-(int)today.DayOfWeek - 7), today.AddDays(-(int)today.DayOfWeek - 1)),
                "Last Two Weeks" => (today.AddDays(-(int)today.DayOfWeek - 14), today.AddDays(-(int)today.DayOfWeek - 1)),
                "Last Month" => (today.AddMonths(-1).AddDays(1 - today.AddMonths(-1).Day), today.AddDays(1 - today.Day).AddDays(-1)),
                "Last 3 Months" => (today.AddMonths(-3), today),
                "Last 6 Months" => (today.AddMonths(-6), today),
                "Last Year" => (today.AddYears(-1), today),
                _ => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(6 - (int)today.DayOfWeek)) // Default to current week
            };
        }
        [RelayCommand]
        private async Task ClearAndRefreshSummaries()
        {
            try
            {
                _logger.LogInformation("Clearing and refreshing employee summaries...");

                // Clear existing data on UI thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    EmployeePeriodSummaries.Clear();
                    ActiveEmployeesList.Clear();
                    HasData = false;
                    StatusMessage = "Refreshing data...";
                });

                // Force reload from database
                await GetEmployeePeriodSummaryAsync();
                await LoadDataAsync();

                _logger.LogInformation("Employee summaries refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear and refresh summaries");
            }
        }
        [RelayCommand]
      
        partial void OnSelectedPeriodChanged(string value)
        {
            Task.Run(async () => await PeriodChangedAsync());
        }

        [RelayCommand]
        private async Task ResetDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Starting database reset...");
                IsLoading = true;
                StatusMessage = "Resetting database...";

                // Reset the work record database
                await _workRecordDatabaseService.ResetDatabaseAsync();
                _logger.LogInformation("Work record database reset completed");

                // Clear all summaries and collections
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    EmployeePeriodSummaries.Clear();
                    ActiveEmployeesList.Clear();
                    HasData = false;
                    TotalJobs = 0;
                    TotalRevenue = 0;
                    StatusMessage = "Database reset complete. No data available.";
                });

                _logger.LogInformation("Database reset completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset database");
                StatusMessage = "Error resetting database. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }
        public async Task ResetDatabaseAndSummariesAsync()
        {
            try
            {
                _logger.LogInformation("Manually triggering database and summaries reset...");
                await _workRecordDatabaseService.ResetDatabaseAsync();
                _logger.LogInformation("Reset completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset database and summaries");
                throw;
            }
        }
        public void Dispose()
        {
            
            MessagingCenter.Unsubscribe<PendingJobsViewModel, WorkRecord>(this, "WorkRecordSaved");
        }
    }
}