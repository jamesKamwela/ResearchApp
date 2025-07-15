using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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

        [ObservableProperty]
        private decimal _totalRevenue;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private int _totalJobs;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WorkRecord> _pendingJobsList = new();

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

        public PendingJobsViewModel(
           ITabNavigationHelper tabNavigator,
            IWorkRecordDatabaseService workRecordDatabaseService,
            IClientDatabaseService clientDatabaseService,
            IEmployeeDatabaseService employeeDatabaseService,
            ILogger<PendingJobsViewModel> logger)
        {
            _workRecordDatabaseService = workRecordDatabaseService;
            _clientDatabaseService = clientDatabaseService;
            _employeeDatabaseService = employeeDatabaseService;
            _tabNavigator = tabNavigator;
            _logger = logger;

            var today = DateTime.Today;
            StartDate = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            EndDate = StartDate.AddDays(5); // Saturday
            _logger.LogDebug($"Initial date range set: {StartDate} to {EndDate}");
            MarkAsCompleteCommand = new RelayCommand<WorkRecord>(async (record) => await MarkAsComplete(record));
            MarkAsPaidCommand = new RelayCommand<WorkRecord>(async (record) => await MarkAsPaid(record));

            _logger.LogDebug("Subscribing to WorkRecordSaved message");
            MessagingCenter.Subscribe<WorkEntryViewModel>(this, "WorkRecordSaved", async (sender) =>
            {
                _logger.LogDebug("Received WorkRecordSaved message, reloading pending jobs");
                await LoadPendingJobs();
            });

            Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Starting database initialization");

                    // Initialize all databases in proper order
                    await _employeeDatabaseService.InitializeAsync();
                    _logger.LogDebug("Employee database initialized");

                    await _workRecordDatabaseService.InitializeAsync();
                    _logger.LogDebug("WorkRecord database initialized");

                    await LoadPendingJobs();
                    _logger.LogDebug("Pending jobs loaded");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _logger.LogDebug("Notifying UI of property changes");
                        OnPropertyChanged(nameof(PendingJobsList));
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Initialization failed");
                }
            });
        }

        partial void OnSelectedPeriodChanged(string value)
        {
            _logger.LogDebug($"Period changed to: {value}");
            Task.Run(LoadPendingJobs);
        }

        public async Task LoadPendingJobs()
        {
            try
            {
                _logger.LogInformation($"Loading pending jobs for period: {SelectedPeriod}");

                // Get only incomplete jobs for the date range
                _logger.LogDebug("Initializing work record database service");
                await _workRecordDatabaseService.InitializeAsync();

                _logger.LogDebug("Getting work records by period");
                var jobs = await _workRecordDatabaseService.GetWorkRecordsByPeriodAsync(SelectedPeriod);
                _logger.LogDebug($"Retrieved {jobs?.Count ?? 0} jobs from database");

                var sortedJobs = jobs?
                    .OrderByDescending(j => j.WorkDate)
                    .ToList();
                _logger.LogDebug($"Sorted {sortedJobs?.Count ?? 0} jobs by date");

                _logger.LogDebug("Updating PendingJobsList");
                PendingJobsList.Clear();
                foreach (var job in sortedJobs ?? new List<WorkRecord>())
                {
                    PendingJobsList.Add(job);
                }
                CalculateTotals();
                _logger.LogDebug("Notifying property change for PendingJobsList");
                OnPropertyChanged(nameof(PendingJobsList));

                _logger.LogInformation($"Successfully loaded {PendingJobsList.Count} incomplete jobs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending jobs");
            }
        }

        [RelayCommand]
        private async Task DeleteJobAsync(WorkRecord job)
        {
            if (job == null)
            {
                _logger.LogWarning("Attempted to delete null job");
                return;
            }

            try
            {
                _logger.LogDebug("Attempting to delete job: {JobId} - {JobName}", job.Id, job.JobName);

                // Show confirmation dialog
                bool confirmed = await Shell.Current.DisplayAlert(
                    "Confirm Delete",
                    $"Are you sure you want to delete the job '{job.JobName}' for client '{job.ClientName}'?\n\nThis action cannot be undone.",
                    "Delete",
                    "Cancel");

                if (!confirmed)
                {
                    _logger.LogDebug("Job deletion cancelled by user");
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = true;
                    StatusMessage = "Deleting job...";
                });

                // Delete from database
                bool deleteSuccess = await _workRecordDatabaseService.DeleteWorkRecordByIdAsync(job.Id);

                if (deleteSuccess)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Remove from UI collection
                        PendingJobsList.Remove(job);

                        // Recalculate totals
                        CalculateTotals();

                        StatusMessage = $"Job '{job.JobName}' deleted successfully";
                    });

                    // Notify other ViewModels about the deletion
                    MessagingCenter.Send<PendingJobsViewModel, WorkRecord>(this, "WorkRecordDeleted", job);

                    _logger.LogInformation("Successfully deleted job: {JobId} - {JobName}", job.Id, job.JobName);

                    // Show success message
                    await Shell.Current.DisplayAlert("Success", $"Job '{job.JobName}' has been deleted.", "OK");
                }
                else
                {
                    _logger.LogError("Failed to delete job from database: {JobId}", job.Id);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        StatusMessage = "Failed to delete job";
                    });

                    await Shell.Current.DisplayAlert("Error", "Failed to delete the job. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting job: {JobId}", job?.Id);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusMessage = "Error deleting job";
                });

                await Shell.Current.DisplayAlert("Error", "An error occurred while deleting the job.", "OK");
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
            }
        }

        // New method to handle completion toggle from UI
        public async Task OnCompleteToggledAsync(WorkRecord job, bool isCompleted)
        {
            if (job == null)
            {
                _logger.LogWarning("Attempted to toggle completion for null job");
                return;
            }

            try
            {
                _logger.LogDebug($"Toggling completion for job ID {job.Id} to {isCompleted}");

                // Update the job's completion status
                job.IsJobCompleted = isCompleted;

                if (isCompleted)
                {
                    job.CompletedDate = DateTime.UtcNow;
                    job.IsPaymentProcessed = false; // Reset payment processing status
                }
                else
                {
                    job.CompletedDate = null;
                    job.IsPaymentProcessed = false;
                    job.IsPaid = false; // If uncompleted, also unpaid
                    job.PaidDate = null;
                }

                // Update in database
                await _workRecordDatabaseService.UpdateWorkRecord(job);

                // Recalculate totals
                CalculateTotals();

                // Send notification
                MessagingCenter.Send(this, "WorkRecordSaved", job);

                _logger.LogInformation($"Successfully toggled completion for job ID {job.Id} to {isCompleted}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling completion for job {job.Id}");

                // Revert the UI change if database update failed
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    job.IsJobCompleted = !isCompleted;
                    OnPropertyChanged(nameof(PendingJobsList));
                });

                await Shell.Current.DisplayAlert("Error", "Failed to update job completion status. Please try again.", "OK");
            }
        }

        // New method to handle payment toggle from UI
        public async Task OnPaidToggledAsync(WorkRecord job, bool isPaid)
        {
            if (job == null)
            {
                _logger.LogWarning("Attempted to toggle payment for null job");
                return;
            }

            try
            {
                _logger.LogDebug($"Toggling payment for job ID {job.Id} to {isPaid}");

                // Update the job's payment status
                job.IsPaid = isPaid;

                if (isPaid)
                {
                    job.PaidDate = DateTime.UtcNow;
                    // Ensure the job is also marked as completed if being paid
                    if (!job.IsJobCompleted)
                    {
                        job.IsJobCompleted = true;
                        job.CompletedDate = DateTime.UtcNow;
                    }
                }
                else
                {
                    job.PaidDate = null;
                }

                // Update in database
                await _workRecordDatabaseService.UpdateWorkRecord(job);

                // Recalculate totals
                CalculateTotals();

                // Send notification
                MessagingCenter.Send(this, "WorkRecordSaved", job);

                _logger.LogInformation($"Successfully toggled payment for job ID {job.Id} to {isPaid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling payment for job {job.Id}");

                // Revert the UI change if database update failed
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    job.IsPaid = !isPaid;
                    OnPropertyChanged(nameof(PendingJobsList));
                });

                await Shell.Current.DisplayAlert("Error", "Failed to update job payment status. Please try again.", "OK");
            }
        }

        private void CalculateTotals()
        {
            if (PendingJobsList?.Any() == true)
            {
                TotalJobs = PendingJobsList.Count;
                TotalRevenue = PendingJobsList.Sum(job => job.TotalAmount);
            }
            else
            {
                TotalJobs = 0;
                TotalRevenue = 0;
            }

            _logger.LogDebug("Calculated totals - Jobs: {JobCount}, Revenue: {Revenue:C}", TotalJobs, TotalRevenue);
        }

        public async Task UpdateWorkRecord(WorkRecord record)
        {
            try
            {
                _logger.LogDebug($"Updating work record ID: {record.Id}");

                // Set the updated timestamp
                record.UpdatedAt = DateTime.UtcNow;
                _logger.LogDebug($"Set updated timestamp to: {record.UpdatedAt}");

                // Save to database
                _logger.LogDebug("Saving record to database");
                await _workRecordDatabaseService.UpdateWorkRecord(record);

                // Optional: Notify other parts of the app
                _logger.LogDebug("Sending WorkRecordUpdated message");
                MessagingCenter.Send(this, "WorkRecordUpdated", record);

                _logger.LogInformation($"Successfully updated work record ID: {record.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating work record ID: {record.Id}");
                throw;
            }
        }

     

        public async Task MarkAsComplete(WorkRecord record)
        {
            try
            {
                _logger.LogDebug($"Marking job ID {record.Id} as complete");

                record.IsJobCompleted = true;
                record.CompletedDate = DateTime.UtcNow;
                record.IsPaymentProcessed = false;

                _logger.LogDebug("Updating record in database");
                await _workRecordDatabaseService.UpdateWorkRecord(record);

                _logger.LogDebug("Reloading pending jobs");
                await LoadPendingJobs();

                _logger.LogDebug("Sending WorkRecordSaved message");
                MessagingCenter.Send(this, "WorkRecordSaved", record);

                _logger.LogInformation($"Successfully marked job ID {record.Id} as complete");
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
                _logger.LogDebug($"Marking job ID {record.Id} as paid");

                record.IsPaid = true;
                record.PaidDate = DateTime.UtcNow;

                _logger.LogDebug("Updating record in database");
                await _workRecordDatabaseService.UpdateWorkRecord(record);

                _logger.LogDebug("Reloading pending jobs");
                await LoadPendingJobs();

                _logger.LogInformation($"Successfully marked job ID {record.Id} as paid");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking job {record.Id} as paid");
            }
        }

        public void Dispose()
        {
            _logger.LogDebug("Disposing PendingJobsViewModel");
            MessagingCenter.Unsubscribe<WorkEntryViewModel>(this, "WorkRecordSaved");
            _logger.LogDebug("Unsubscribed from WorkRecordSaved message");
        }
    }
}