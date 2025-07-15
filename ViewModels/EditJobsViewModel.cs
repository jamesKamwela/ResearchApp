using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ResearchApp.Views;


namespace ResearchApp.ViewModels
{
    public partial class EditJobsViewModel : ObservableObject, IQueryAttributable
    {
        //========================================
        //initialization
        //========================================
   
        private readonly IClientDatabaseService _databaseService;
        private readonly ILogger<EditJobsViewModel> _logger;

        [ObservableProperty]
        private int _jobId;

        [ObservableProperty]
        private int _currentClientId;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage;

     
       

        [ObservableProperty]
        private JobViewModel _editingJob = new JobViewModel();

        [ObservableProperty]
        private List<string> _units = new List<string> { "Item", "Ton", "Square meter" };
        public EditJobsViewModel(
            IClientDatabaseService databaseService,
            ILogger<EditJobsViewModel> logger)
        {
            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(JobId))
                {
                    Debug.WriteLine($"JobId changed to: {JobId}");
                }
            };
            EditingJob = new JobViewModel();
            _databaseService = databaseService;
            _logger = logger;
        }


        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            Debug.WriteLine($"Received parameters: {string.Join(", ", query.Keys)}");

            // 1. First handle the JobFromCollection if present
            if (query.TryGetValue("JobFromCollection", out object jobObj) &&
                jobObj is JobViewModel job)
            {
                Debug.WriteLine($"Job data received: {job.Name}, {job.Amount}, {job.Unit}");
                EditingJob = job;
                JobId = job.Id; // Set the JobId from the job object
            }
            // 2. Handle explicit JobId parameter if passed separately
            else if (query.TryGetValue("JobId", out object jobIdObj))
            {
                JobId = Convert.ToInt32(jobIdObj);
                Debug.WriteLine($"JobId received: {JobId}");
            }

            // 3. Handle ClientId
            if (query.TryGetValue("ClientId", out object clientIdObj))
            {
                _currentClientId = Convert.ToInt32(clientIdObj);
                Debug.WriteLine($"ClientId received: {_currentClientId}");
            }
        }

        [RelayCommand]
        private async Task SaveJob()
        {
            // Use EditingJob directly - no property copying needed
            var dbJob = new Job
            {
                Id = EditingJob.Id,
                JobName = EditingJob.Name,
                Amount = EditingJob.Amount,
                Unit = EditingJob.Unit,
                ClientId = _currentClientId
            };

            await _databaseService.UpdateJobAsync(dbJob);
            await Shell.Current.GoToAsync("..");
        }
       

        [RelayCommand]
        private async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");


            // Option 2: Absolute navigation (more reliable)
            // await Shell.Current.GoToAsync($"//{nameof(EditClient)}?ClientId={_currentClientId}");
        }

        [RelayCommand]
        private async Task DeleteJob()
        {
           
                if (JobId <= 0)
                {
                    await Shell.Current.DisplayAlert("Error",
                        $"No job to delete (ID: {JobId})", "OK"); // More detailed error
                    return;
                }
            

            bool confirm = await Shell.Current.DisplayAlert("Confirm Delete",
                "Are you sure you want to delete this job?", "Yes", "No");

            if (!confirm) return;

            try
            {
                var job = new Job { Id = JobId };
                await _databaseService.DeleteJobAsync(job);

                await Shell.Current.DisplayAlert("Success", "Job deleted successfully", "OK");

                // Navigate back with a callback to refresh the list
                await Shell.Current.GoToAsync("..", new Dictionary<string, object>
        {
            { "RefreshJobs", true }
        });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete job");
                await Shell.Current.DisplayAlert("Error", "Failed to delete job", "OK");
            }
        }
    }


}

