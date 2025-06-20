using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.Models;
using ResearchApp.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;


namespace ResearchApp.ViewModels
{
    public partial class PendingJobsViewModel : ObservableObject
    {
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly IClientDatabaseService _clientDatabaseService;
        private readonly IEmployeeDatabaseService _employeeDatabaseService;
        private readonly ITabNavigationHelper _tabNavigator;

        private readonly ILogger<PendingJobsViewModel> _logger;
   

        [ObservableProperty]
        private DateTime _startDate;

        [ObservableProperty]
        private DateTime _endDate;

        //
        public PendingJobsViewModel(
           ITabNavigationHelper tabNavigator,
            IWorkRecordDatabaseService workRecordDatabaseService,
            IClientDatabaseService clientDatabaseService,
            IEmployeeDatabaseService employeeDatabaseService,
            ILogger<PendingJobsViewModel> logger
            )
        {
            
           var today = DateTime.Today;
            StartDate = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            EndDate = StartDate.AddDays(5); // Saturday



            _workRecordDatabaseService = workRecordDatabaseService;
            _clientDatabaseService = clientDatabaseService;
            _employeeDatabaseService = employeeDatabaseService;
            _tabNavigator = tabNavigator;


            _logger = logger;
            MarkAsCompleteCommand = new RelayCommand<WorkRecord>(async (record) => await MarkAsComplete(record));
            MarkAsPaidCommand = new RelayCommand<WorkRecord>(async (record) => await MarkAsPaid(record));
            MessagingCenter.Subscribe<WorkEntryViewModel>(this, "WorkRecordSaved", async (sender) =>
            {
                await LoadPendingJobs();
            });

            Task.Run(async () =>
            {
                try
                {
                    // Initialize all databases in proper order
                    await _employeeDatabaseService.InitializeAsync();
                    await _workRecordDatabaseService.InitializeAsync();

                    await LoadPendingJobs();
                    MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(PendingJobsList)));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Initialization failed");
                }
            });
        }
        [ObservableProperty]
        private ObservableCollection<WorkRecord> _pendingJobsList = new();
        //public ObservableCollection<WorkRecord> PendingJobsList { get; private set; } = new ObservableCollection<WorkRecord>();
        public ICommand MarkAsCompleteCommand { get; }
        public ICommand MarkAsPaidCommand { get; }

        public List<string> PeriodOptions { get; } = new List<string>{
            "Current Week",
            "Last Week",
            "Last Two Weeks",
            "Last Month",
             };

        [ObservableProperty]
        private string _selectedPeriod = "Current Week";
        partial void OnSelectedPeriodChanged(string value)
        {
            Task.Run(LoadPendingJobs);
        }

      
        public async Task LoadPendingJobs()
        {
            try
            {
               LoggerExtensions.LogInformation(_logger, $"Loading pending jobs for period: {SelectedPeriod}");

                // Get only incomplete jobs for the date range
                await _workRecordDatabaseService.InitializeAsync();
                var jobs = await _workRecordDatabaseService.GetWorkRecordsByPeriodAsync(
                   SelectedPeriod);  // This filters for incomplete jobs only

               
                var sortedJobs = jobs?
                    .OrderByDescending(j => j.WorkDate)
                    .ToList();

                PendingJobsList.Clear();
                foreach (var job in sortedJobs ?? new List<WorkRecord>())
                {
                    PendingJobsList.Add(job);
                }
                OnPropertyChanged(nameof(PendingJobsList));

                LoggerExtensions.LogInformation(_logger, $"Loaded {PendingJobsList.Count} incomplete jobs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending jobs");
            }
        }
        public async Task UpdateWorkRecord(WorkRecord record)
        {
            try
            {
                // Set the updated timestamp
                record.UpdatedAt = DateTime.UtcNow;

                // Save to database
                await _workRecordDatabaseService.UpdateWorkRecord(record);

                // Optional: Notify other parts of the app
                MessagingCenter.Send(this, "WorkRecordUpdated", record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating work record ID: {record.Id}");
                throw;
            }
        }

  
        public string WorkSummery(WorkRecord workRecord)
        {
            var workSummery = new StringBuilder();
            workSummery.AppendLine($"Client: {workRecord.Client?.Name}");
            workSummery.AppendLine($"Job: {workRecord.Job?.JobName}");
            workSummery.AppendLine($"Work Date: {workRecord.WorkDate.ToShortDateString()}");
            workSummery.AppendLine($"Quantity Completed: {workRecord.QuantityCompleted}");
            workSummery.AppendLine($"Total Amount: {workRecord.TotalAmount:C}");
            return workSummery.ToString();
        }

        public async Task MarkAsComplete(WorkRecord record)
        {
            try
            {
                record.IsJobCompleted = true;
                record.CompletedDate = DateTime.UtcNow;
                record.IsPaymentProcessed = false;
                await _workRecordDatabaseService.UpdateWorkRecord(record);
                await LoadPendingJobs(); // Refresh the list
                MessagingCenter.Send(this, "WorkRecordSaved", record); // Notify other parts of the app


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking job {record.Id} as complete");
            }
        }

        public async Task MarkAsPaid(WorkRecord record)
        {
            try
            {
                record.IsPaid = true;
                record.PaidDate = DateTime.UtcNow;
                await _workRecordDatabaseService.UpdateWorkRecord(record);
                await LoadPendingJobs(); // Refresh the list
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking job {record.Id} as paid");
            }
        }

        public void Dispose()
        {
            MessagingCenter.Unsubscribe<WorkEntryViewModel>(this, "WorkRecordSaved");
        }

    }
}
