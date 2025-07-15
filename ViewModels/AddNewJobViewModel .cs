using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using ResearchApp.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.ViewModels
{
    public partial class AddNewJobViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IClientDatabaseService _databaseService;
        private readonly ILogger<AddNewJobViewModel> _logger;
        private int _clientId;

        public AddNewJobViewModel(
            IClientDatabaseService databaseService,
            ILogger<AddNewJobViewModel> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
            Jobs = new ObservableCollection<Job>();
            ResetForm();
        }

        // Properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsJobValid))]
        [NotifyPropertyChangedFor(nameof(ValidationSummary))] // Add this for debugging
        private string _jobName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsJobValid))]
        [NotifyPropertyChangedFor(nameof(ValidationSummary))] // Add this for debugging
        private string _jobAmount = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsJobValid))]
        [NotifyPropertyChangedFor(nameof(ValidationSummary))] // Add this for debugging
        private string _selectedJobUnit;

        [ObservableProperty]
        private ObservableCollection<Job> _jobs;

        [ObservableProperty]
        private bool _isJobSectionVisible = true;

        [ObservableProperty]
        private bool _isSummaryVisible;

        [ObservableProperty]
        private bool _canAddMoreJobs = true;

        // Add this property for debugging validation issues
        public string ValidationSummary
        {
            get
            {
                var issues = new List<string>();

                if (string.IsNullOrWhiteSpace(JobName))
                    issues.Add("Job name is empty");
                else if (JobName.Trim().Length < 2)
                    issues.Add("Job name too short");

                if (string.IsNullOrWhiteSpace(JobAmount))
                    issues.Add("Job amount is empty");
                else if (!decimal.TryParse(JobAmount.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
                    issues.Add("Job amount format invalid");
                else if (amount <= 0)
                    issues.Add("Job amount must be > 0");

                if (string.IsNullOrWhiteSpace(SelectedJobUnit))
                    issues.Add("Job unit not selected");

                if (_clientId <= 0)
                    issues.Add($"Client ID invalid: {_clientId}");

                return issues.Any() ? $"Issues: {string.Join(", ", issues)}" : "All valid";
            }
        }

        public bool IsJobValid
        {
            get
            {
                var isValid = !string.IsNullOrWhiteSpace(JobName) &&
                    JobName.Trim().Length >= 2 &&
                    !string.IsNullOrWhiteSpace(JobAmount) &&
                    decimal.TryParse(JobAmount.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) &&
                    amount > 0 &&
                    !string.IsNullOrWhiteSpace(SelectedJobUnit) &&
                    _clientId > 0;

                // Log validation result for debugging
                _logger.LogInformation($"IsJobValid: {isValid} - {ValidationSummary}");

                return isValid;
            }
        }

        private bool ValidateJobAmount(string amount) =>
            decimal.TryParse(amount?.Replace(',', '.'),
                           NumberStyles.Any,
                           CultureInfo.InvariantCulture,
                           out decimal result) && result > 0;

        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ClientId", out object? clientIdObj) &&
                int.TryParse(clientIdObj?.ToString(), out int clientId))
            {
                _clientId = clientId;
                _logger.LogInformation($"ClientId set to: {_clientId}");

                // Trigger validation update after ClientId is set
                OnPropertyChanged(nameof(IsJobValid));
                OnPropertyChanged(nameof(ValidationSummary));
            }
            else
            {
                _logger.LogError("Invalid or missing ClientId in navigation parameters");
                await Shell.Current.DisplayAlert("Error", "No client ID provided", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        // Make sure the command can execute based on IsJobValid
        [RelayCommand(CanExecute = nameof(IsJobValid))]
        private async Task SaveJob()
        {
            _logger.LogInformation("SaveJob command started");

            if (!IsJobValid)
            {
                _logger.LogWarning($"SaveJob called but validation failed: {ValidationSummary}");
                return;
            }

            // Debug logging
            _logger.LogInformation($"Saving job - ClientId: {_clientId}, JobName: '{JobName}', JobAmount: '{JobAmount}', Unit: '{SelectedJobUnit}'");

            // Additional ClientId check (should not be needed if IsJobValid works correctly)
            if (_clientId <= 0)
            {
                await Shell.Current.DisplayAlert("Error", "Invalid client ID", "OK");
                return;
            }

            bool confirm = await App.Current.MainPage.DisplayAlert(
                "Confirm Save",
                $"Are you sure you want to save the following Job?\n\nName: {JobName}\nAmount: {JobAmount}\nUnit: {SelectedJobUnit}",
                "Yes", "No");

            if (confirm)
            {
                try
                {
                    // Parse amount with validation
                    if (!decimal.TryParse(JobAmount.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
                    {
                        await Shell.Current.DisplayAlert("Error", "Invalid amount value", "OK");
                        return;
                    }

                    var job = new Job
                    {
                        JobName = JobName.CapitalizeFirstLetter(),
                        Amount = amount,
                        Unit = SelectedJobUnit,
                        ClientId = _clientId
                    };

                    // Log the job details before saving
                    _logger.LogInformation($"Job to save - ID: {job.Id}, ClientId: {job.ClientId}, Name: '{job.JobName}', Amount: {job.Amount}");

                    await _databaseService.SaveJobsAsync(new List<Job> { job });
                    Jobs.Add(job);

                    // Reset form
                    ResetForm();

                    await Shell.Current.DisplayAlert("Success", "Job saved successfully", "OK");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save job");
                    await Shell.Current.DisplayAlert("Error", $"Failed to save job: {ex.Message}", "OK");
                }
            }
        }

        // Add a test command to debug the save button
        [RelayCommand]
        private async Task TestSave()
        {
            await Shell.Current.DisplayAlert("Debug",
                $"IsJobValid: {IsJobValid}\n{ValidationSummary}\nClientId: {_clientId}", "OK");
        }

        [RelayCommand]
        private async Task FinishAddingJobs()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private async Task AddAnotherJob()
        {
            IsJobSectionVisible = true;
            IsSummaryVisible = false;
        }

        private void ResetForm()
        {
            JobName = string.Empty;
            JobAmount = string.Empty;
            SelectedJobUnit = null;
            // Don't clear Jobs collection here if you want to keep added jobs
            // Jobs.Clear();
            IsJobSectionVisible = true;
            IsSummaryVisible = false;
            CanAddMoreJobs = true;
        }

        private string GenerateSummary()
        {
            var summary = new StringBuilder();
            summary.AppendLine("Jobs Summary:");
            foreach (var job in Jobs)
            {
                summary.AppendLine($"- {job.JobName}: {job.Amount} TL per {job.Unit}");
            }
            return summary.ToString();
        }
    }
}