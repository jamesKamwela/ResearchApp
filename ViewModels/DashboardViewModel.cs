using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.ViewModels
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly IClientDatabaseService _clientDatabaseService;
        private readonly IEmployeeDatabaseService _employeeDatabaseService;
        private readonly ILogger<DashboardViewModel> _logger;

        public DashboardViewModel( 
            IClientDatabaseService clientDatabaseService,
            IEmployeeDatabaseService employeeDatabaseService,
            IWorkRecordDatabaseService workRecordDatabaseService,
            ILogger<DashboardViewModel> logger

            ) { 

            _clientDatabaseService = clientDatabaseService ?? throw new ArgumentNullException(nameof(clientDatabaseService));
            _employeeDatabaseService = employeeDatabaseService ?? throw new ArgumentNullException(nameof(employeeDatabaseService));
            _workRecordDatabaseService = workRecordDatabaseService ?? throw new ArgumentNullException(nameof(workRecordDatabaseService));
            _logger = logger;
            _ = Task.Run(async () =>
            {
                await LoadCompletedJobsAsync();
                await LoadContactStatisticsAsync();
            });

            MessagingCenter.Subscribe<PendingJobsViewModel>(this, "WorkRecordSaved", async (sender) =>
            {
                await LoadCompletedJobsAsync();
            });
            _ = LoadCompletedJobsAsync();
        }

        [ObservableProperty]
        private string _selectedPeriod = "Current Week";


        partial void OnSelectedPeriodChanged(string value)
        {
            Debouncer.Debounce("periodChange", TimeSpan.FromMilliseconds(300), async () =>
            {
                try
                {
                    IsLoading = true;
                    await LoadCompletedJobsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load jobs for period");
                    // Optionally show an alert to the user
                }
                finally
                {
                    IsLoading = false;
                }
            });
        }


        [ObservableProperty]
        private int _totalJobsCompleted;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private int _totalEmployees;

        [ObservableProperty]
        private int _totalClients;

        [ObservableProperty]
        private Chart _contactDistributionChart;
        public ObservableCollection<WorkRecord> CompletedJobs { get; private set; } = new ObservableCollection<WorkRecord>();

        public List<string> PeriodOptions { get; } = new List<string>{
            "Current Week",
            "Last Week",
            "Last Two Weeks",
            "Last Month",
            "Last 3 Months",
            "Last 6 Months",
            "Last Year"
             };

        [RelayCommand]
        private async Task LoadCompletedJobsAsync()
        {
            try
            {
                IsLoading = true;
                var jobs = await _workRecordDatabaseService.GetWorkRecordsByPeriodAsync(SelectedPeriod);

                CompletedJobs.Clear();

                if (jobs != null)
                {
                    // Get all employee relationships for these jobs in one query
                    var jobIds = jobs.Select(j => j.Id).ToList();
                    var employeeCounts = await _workRecordDatabaseService.GetEmployeeCountsForWorkRecordsAsync(jobIds);

                    foreach (var job in jobs)
                    {
                        // Set employee count from the pre-loaded data
                        job.EmployeeCount = employeeCounts.TryGetValue(job.Id, out var count) ? count : 0;

                        CompletedJobs.Add(job);
                        _logger.LogInformation($"Loaded job: {job.DisplayJobName} for client: {job.DisplayClientName} on" +
                            $" {job.WorkDate.ToShortDateString()} with {job.EmployeeCount} employees.");
                    }
                }
                _logger.LogInformation($"Loaded {CompletedJobs.Count} completed jobs.");
                TotalJobsCompleted = CompletedJobs.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load completed jobs");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public static class Debouncer
        {
            private static Dictionary<string, System.Timers.Timer> _timers = new();

            public static void Debounce(string key, TimeSpan wait, Action action)
            {
                if (_timers.TryGetValue(key, out var timer))
                {
                    timer.Stop();
                    timer.Dispose();
                }

                timer = new System.Timers.Timer(wait.TotalMilliseconds)
                {
                    AutoReset = false
                };
                timer.Elapsed += (s, e) =>
                {
                    action();
                    _timers.Remove(key);
                    timer.Dispose();
                };
                _timers[key] = timer;
                timer.Start();
            }
        }

        #region pie chart
        //===================================================
        //Employee and Client Pie Chart
        //===================================================
        private async Task<(int employeeCount, int clientCount)> GetContactCountsAsync()
        {
            try
            {
                // Get both counts in parallel for better performance
                var employeeTask = _employeeDatabaseService.GetTotalEmployeeCountAsync();
                var clientTask = _clientDatabaseService.GetTotalClientCountAsync();

                await Task.WhenAll(employeeTask, clientTask);

                return (employeeTask.Result, clientTask.Result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching contact counts");
                return (0, 0); // Return zeros if there's an error
            }
        }
        [RelayCommand]
        private async Task LoadContactStatisticsAsync()
        {
            try
            {
                IsLoading = true;

                // Get the counts from database
                var (employeeCount, clientCount) = await GetContactCountsAsync();

                // Update properties
                TotalEmployees = employeeCount;
                TotalClients = clientCount;

                // Only update chart if we have data
                if (employeeCount > 0 || clientCount > 0)
                {
                    UpdateContactDistributionChart();
                }

                _logger.LogInformation($"Contact stats loaded - Employees: {employeeCount}, Clients: {clientCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load contact statistics");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateContactDistributionChart()
        {
            try
            {
                var entries = new List<ChartEntry>();

                // Add employee entry if count > 0
                if (TotalEmployees > 0)
                {
                    entries.Add(new ChartEntry(TotalEmployees)
                    {
                        Label = "Employees",
                        ValueLabel = TotalEmployees.ToString(),
                        Color = SKColor.Parse("#FF6B35"), // Bright Orange
                        TextColor = SKColors.White,
                        ValueLabelColor = SKColors.White
                    });
                }

                // Add client entry if count > 0
                if (TotalClients > 0)
                {
                    entries.Add(new ChartEntry(TotalClients)
                    {
                        Label = "Clients",
                        ValueLabel = TotalClients.ToString(),
                        Color = SKColor.Parse("#00D4FF"), // Bright Cyan
                        TextColor = SKColors.White,
                        ValueLabelColor = SKColors.White
                    });
                }

                // Only create chart if we have entries
                if (entries.Any())
                {
                    ContactDistributionChart = new PieChart
                    {
                        Entries = entries,
                        LabelTextSize = 35, // Increased size for better visibility inside
                        LabelMode = LabelMode.None, // Remove external labels
                        BackgroundColor = SKColors.Transparent,
                        AnimationDuration = TimeSpan.FromMilliseconds(800),
                        IsAnimated = true,
                        HoleRadius = 0.0f, // Remove the hole to have more space for labels
                                           // Enable inner labels
                        GraphPosition = GraphPosition.Center
                    };
                }
                else
                {
                    // Create empty chart if no data
                    ContactDistributionChart = new PieChart
                    {
                        Entries = new List<ChartEntry>(),
                        BackgroundColor = SKColors.Transparent
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact distribution chart");
            }
        }
        #endregion
        public async Task RefreshDashboardAsync()
        {
            await LoadCompletedJobsAsync();
        }
    

      

        public void Dispose() { 
                // Unsubscribe from MessagingCenter to prevent memory leaks
                MessagingCenter.Unsubscribe<PendingJobsViewModel>(this, "WorkRecordSaved");
                _logger.LogInformation("DashboardViewModel disposed and unsubscribed from MessagingCenter.");
        }
    }
}




