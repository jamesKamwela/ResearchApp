using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ResearchApp.Models;
using ResearchApp.DataStorage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ResearchApp.ViewModels
{
    [QueryProperty(nameof(EmployeeId), "employeeId")]
    [QueryProperty(nameof(EmployeeName), "employeeName")]
    [QueryProperty(nameof(Period), "period")]
    public partial class EmployeeJobsDataViewModel : ObservableObject
    {
        private readonly ILogger<EmployeeJobsDataViewModel> _logger;
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly IEmployeeDatabaseService _employeeDatabaseService;

        public EmployeeJobsDataViewModel(
            ILogger<EmployeeJobsDataViewModel> logger,
            IWorkRecordDatabaseService workRecordDatabaseService,
            IEmployeeDatabaseService employeeDatabaseService)
        {
            _logger = logger;
            _workRecordDatabaseService = workRecordDatabaseService;
            _employeeDatabaseService = employeeDatabaseService;

            // Initialize collections
            IndividualEmployeeStats = new ObservableCollection<IndividualEmployeeStatsViewModel>();
            PeriodOptions = new List<string>
            {
                "Current Week",
                "Last Week",
                "Last Two Weeks",
                "Last Month",
                "Last 3 Months",
                "Last 6 Months",
                "Last Year"
            };
        }

        [ObservableProperty]
        private int employeeId;

        [ObservableProperty]
        private string employeeName = string.Empty;

        [ObservableProperty]
        private string period = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoData))]
        private bool hasData;

        public bool HasNoData => !HasData;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private string selectedPeriod = "Current Week";

        [ObservableProperty]
        private ObservableCollection<IndividualEmployeeStatsViewModel> individualEmployeeStats;

        public List<string> PeriodOptions { get; }

        partial void OnEmployeeIdChanged(int value)
        {
            if (value > 0)
            {
                _logger.LogDebug($"Loading jobs for employee {EmployeeName}");
                LoadEmployeeJobsAsync().SafeFireAndForget();
            }
            else
            {
                _logger.LogDebug("No employee ID provided - showing blank state");
                HasData = false;
                StatusMessage = "No employee selected";
            }
        }

        partial void OnSelectedPeriodChanged(string value)
        {
            if (EmployeeId > 0)
            {
                _logger.LogDebug($"Period changed to {value}, reloading data");
                LoadEmployeeJobsAsync().SafeFireAndForget();
            }
        }
        [RelayCommand]
        private async Task LoadEmployeeJobsAsync()
        {
            if (EmployeeId <= 0) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Loading jobs...";
                HasData = false;
                IndividualEmployeeStats?.Clear();

                var (startDate, endDate) = GetDateRangeForPeriod(SelectedPeriod);
                Debug.WriteLine($"Date range for {SelectedPeriod}: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                // Fetch basic work records without children first
                var workRecords = await _workRecordDatabaseService.GetWorkRecordsForEmployeeAsync(
                    EmployeeId,
                    startDate,
                    endDate);

                if (workRecords == null || workRecords.Count == 0)
                {
                    StatusMessage = "No jobs found for selected period";
                    return;
                }

                // Now optionally load details for the records we found
                var detailedRecords = new List<WorkRecord>();
                foreach (var record in workRecords)
                {
                    var detailedRecord = await _workRecordDatabaseService.GetByIdAsync<WorkRecord>(record.Id);
                    if (detailedRecord != null)
                    {
                        detailedRecords.Add(detailedRecord);
                    }
                }

                // Create view models
                var statsViewModels = detailedRecords
                    .OrderByDescending(r => r.WorkDate)
                    .Select(r => new IndividualEmployeeStatsViewModel(r))
                    .ToList();

                // Update UI
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IndividualEmployeeStats = new ObservableCollection<IndividualEmployeeStatsViewModel>(statsViewModels);
                    HasData = IndividualEmployeeStats.Count > 0;
                    StatusMessage = HasData ? string.Empty : "No job details available";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading jobs: {ex}");
                StatusMessage = "Failed to load jobs. Please try again later.";
            }
            finally
            {
                IsLoading = false;
            }
        }
        private (DateTime StartDate, DateTime EndDate) GetDateRangeForPeriod(string period)
        {
            var today = DateTime.Today;
            var currentDayOfWeek = (int)today.DayOfWeek;
            var daysSinceMonday = (currentDayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var thisWeekMonday = today.AddDays(-daysSinceMonday);
            var lastWeekMonday = thisWeekMonday.AddDays(-7);
            var lastWeekSunday = thisWeekMonday.AddDays(-1);

            return period switch
            {
                "Current Week" => (thisWeekMonday, thisWeekMonday.AddDays(6)),
                "Last Week" => (lastWeekMonday, lastWeekSunday),
                "Last Two Weeks" => (thisWeekMonday.AddDays(-14), today),
                "Last Month" => (thisWeekMonday.AddDays(-28), today),
                "Last 3 Months" => (today.AddMonths(-3), today),
                "Last 6 Months" => (today.AddMonths(-6), today),
                "Last Year" => (today.AddYears(-1), today),
                _ => (thisWeekMonday, thisWeekMonday.AddDays(6))
            };
        }

        public partial class IndividualEmployeeStatsViewModel : ObservableObject
        {
            [ObservableProperty]
            private DateTime workDate;

            [ObservableProperty]
            private string jobName;

            [ObservableProperty]
            private string clientName;

            [ObservableProperty]
            private int employeeCount;

            [ObservableProperty]
            private decimal quantityCompleted;

            [ObservableProperty]
            private string unit;

            [ObservableProperty]
            private decimal employeeCut;

            // Formatted properties for display
            public string FormattedWorkDate => $"{WorkDate:yyyy-MM-dd} ({WorkDate:ddd})";
            public string FormattedQuantityCompleted =>  $"{QuantityCompleted:N0} {Unit}";

            

            public IndividualEmployeeStatsViewModel(WorkRecord record)
            {
                if (record != null)
                {
                    WorkDate = record.WorkDate;
                    JobName = record.DisplayJobName;
                    ClientName = record.DisplayClientName;
                    EmployeeCount = record.EmployeeCount;
                    Unit = record.Unit;
                    QuantityCompleted = record.QuantityCompleted;
                    EmployeeCut = record.AmountPerEmployee;
                }
            }
        }
    }

    // Extension method to safely fire-and-forget tasks
    public static class TaskExtensions
    {
        public static void SafeFireAndForget(this Task task, Action<Exception> onException = null)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted && onException != null)
                    onException(t.Exception);
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}