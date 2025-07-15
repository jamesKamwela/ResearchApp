using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ResearchApp.ViewModels
{
    [QueryProperty(nameof(ClientId), "clientId")]
    [QueryProperty(nameof(ClientName), "clientName")]
    [QueryProperty(nameof(Period), "period")]
    public partial class ClientJobsDataViewModel : ObservableObject
    {
        private readonly ILogger<ClientJobsDataViewModel> _logger;
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly IClientDatabaseService _clientDatabaseService;

        public ClientJobsDataViewModel(
            ILogger<ClientJobsDataViewModel> logger,
            IWorkRecordDatabaseService workRecordDatabaseService,
            IClientDatabaseService clientDatabaseService)
        {
            _logger = logger;
            _workRecordDatabaseService = workRecordDatabaseService;
            _clientDatabaseService = clientDatabaseService;
            _logger.LogDebug("Setting up MessagingCenter subscription...");
            MessagingCenter.Subscribe<PendingJobsViewModel, WorkRecord>(this, "WorkRecordSaved",
                async (sender, updatedRecord) => {
                    _logger.LogDebug("WorkRecordSaved message received, reloading data...");
                    await LoadClientJobsAsync();
                });
            // Initialize collections
            IndividualClientStats = new ObservableCollection<IndividualClientStatsViewModel>();
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
        private int clientId;

        [ObservableProperty]
        private string clientName = string.Empty;

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
        private ObservableCollection<IndividualClientStatsViewModel> individualClientStats;

        public List<string> PeriodOptions { get; }

        partial void OnClientIdChanged(int value)
        {
            if (value > 0)
            {
                _logger.LogDebug($"Loading jobs for client {ClientName}");
                LoadClientJobsAsync().SafeFireAndForget();
            }
            else
            {
                _logger.LogDebug("No client ID provided - showing blank state");
                HasData = false;
                StatusMessage = "No client selected";
            }
        }

        partial void OnSelectedPeriodChanged(string value)
        {
            if (ClientId > 0)
            {
                _logger.LogDebug($"Period changed to {value}, reloading data");
                LoadClientJobsAsync().SafeFireAndForget();
            }
        }

        [RelayCommand]
        private async Task LoadClientJobsAsync()
        {
            if (ClientId <= 0) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Loading jobs...";
                HasData = false;
                IndividualClientStats?.Clear();

                var (startDate, endDate) = GetDateRangeForPeriod(SelectedPeriod);
                Debug.WriteLine($"Date range for {SelectedPeriod}: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                // Fetch work records for this client
                var clientWorkRecords = await _workRecordDatabaseService.GetClientWorkRecordsAsync(
                    clientId: ClientId, isPaid: false, isComplete: true, period: SelectedPeriod

                 );


                if (clientWorkRecords == null || clientWorkRecords.Count == 0)
                {
                    _logger.LogInformation($"No jobs found for client {ClientName} in the selected period.");
                    StatusMessage = "No jobs found for selected period";

                    return;
                }
                Debug.WriteLine($"Found {clientWorkRecords.Count} work records for client {ClientName} in the selected period.");

                // Now optionally load details for the records we found
                var detailedRecords = new List<WorkRecord>();
                foreach (var record in clientWorkRecords)
                {
                    var detailedRecord = await _workRecordDatabaseService.GetByIdWithChildrenAsync<WorkRecord>(record.Id);
                    if (detailedRecord != null)
                    {
                        detailedRecords.Add(detailedRecord);
                    }
                }

                // Create view models
                var statsViewModels = detailedRecords
                    .OrderByDescending(r => r.WorkDate)
                    .Select(r => new IndividualClientStatsViewModel(r))
                    .ToList();

                // Update UI
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IndividualClientStats = new ObservableCollection<IndividualClientStatsViewModel>(statsViewModels);
                    HasData = IndividualClientStats.Count > 0;
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

        public partial class IndividualClientStatsViewModel : ObservableObject
        {
            [ObservableProperty]
            private DateTime workDate;

            [ObservableProperty]
            private string jobName;

            [ObservableProperty]
            private int employeeCount;

            [ObservableProperty]
            private decimal quantityCompleted;

            [ObservableProperty]
            private string unit;

            [ObservableProperty]
            private decimal totalAmount;

            [ObservableProperty]
            private decimal adminCommission;

            // Formatted properties for display
            public string FormattedWorkDate => $"{WorkDate:yyyy-MM-dd} ({WorkDate:ddd})";
            public string FormattedQuantityCompleted => $"{QuantityCompleted:N0} {Unit}";
            public string FormattedTotalAmount => $"TL\u00A0{TotalAmount:N2}";
            public string FormattedAdminCommission => $"TL\u00A0{AdminCommission:N2}";

            public IndividualClientStatsViewModel(WorkRecord record)
            {
                if (record != null)
                {
                    WorkDate = record.WorkDate;
                    JobName = record.DisplayJobName;
                    EmployeeCount = record.EmployeeCount;
                    Unit = record.Unit;
                    QuantityCompleted = record.QuantityCompleted;
                    TotalAmount = record.TotalAmount;
                    AdminCommission = record.AdminCommission;
                }
            }
        }
        public void Dispose()
        {
            // Unsubscribe from MessagingCenter to prevent memory leaks
            MessagingCenter.Unsubscribe<PendingJobsViewModel, WorkRecord>(this, "WorkRecordSaved");
            _logger.LogDebug("Unsubscribed from WorkRecordSaved messages.");
        }
    }
}