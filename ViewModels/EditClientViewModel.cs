using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Exceptions;
using ResearchApp.Models;
using ResearchApp.StaticClasses;
using ResearchApp.Views;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations; 
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions; 
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Media.Protection.PlayReady;
using static Microsoft.Maui.ApplicationModel.Permissions;


namespace ResearchApp.ViewModels
{
    public partial class EditClientViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IClientDatabaseService _databaseService;
        private readonly ILogger<EditClientViewModel> _logger;
        private int _currentClientId;

        public EditClientViewModel(
            IClientDatabaseService databaseService,
            ILogger<EditClientViewModel> logger)
        {
            _databaseService = databaseService;
            _logger = logger;

            IsClientSectionVisible = true;
            IsJobsListVisible = false;
            IsShowJobsButtonVisible = true;
            Jobs = new ObservableCollection<JobViewModel>();
            _logger.LogInformation("EditClientViewModel created.");
        }



        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage;


        // Client Properties
        public sealed record ClientDeletedMessage(Client Value);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _clientName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _clientAddress = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _clientPhone = string.Empty;

       public bool IsValid =>
            !string.IsNullOrWhiteSpace(ClientName) &&
            ClientName.Length is >= 3 and <= 100 &&
            !string.IsNullOrWhiteSpace(ClientAddress) &&
            ClientAddress.Length is >= 1 and <= 200 &&
            IsPhoneValid && (ClientName != _originalName ||
            ClientPhone != _originalPhone ||
            ClientAddress != _originalAddress);
        public bool IsPhoneValid => ValidatePhone(ClientPhone);

        private string _originalName = string.Empty;
        private string _originalPhone = string.Empty;
        private string _originalAddress = string.Empty;

        // UI State Properties
        [ObservableProperty] private bool isClientSectionVisible;
        [ObservableProperty] private bool isJobsListVisible;
        [ObservableProperty] private bool isShowJobsButtonVisible;
        [ObservableProperty] private bool hasJobs;
        [ObservableProperty] private bool noJobsFound;

        // Jobs Collection
        [ObservableProperty]
        private JobViewModel? _selectedJob;

        private ObservableCollection<JobViewModel> _jobs = new();
        public ObservableCollection<JobViewModel> Jobs
        {
            get => _jobs;
            set => SetProperty(ref _jobs, value);
        }

        // ===================== VALIDATION =====================
        private bool ValidateClient() =>
            !string.IsNullOrWhiteSpace(ClientName) &&
            ClientName.Length is >= 3 and <= 100 &&
            !string.IsNullOrWhiteSpace(ClientAddress) &&
            ClientAddress.Length is >= 1 and <= 200 &&
            IsPhoneValid;

        private bool ValidatePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;

            // Remove all non-digit characters
            var digitsOnly = new string(phone.Where(c => char.IsDigit(c)).ToArray());

            // Validate length
            return digitsOnly.Length is >= 7 and <= 15;
        }

        // ===================== NAVIGATION & DATA LOADING =====================
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ContactId", out object? contactIdObj) &&
                int.TryParse(contactIdObj?.ToString(), out int contactId))
            {
                _currentClientId = contactId;
                await LoadClientAsync(contactId);
            }
            else
            {
                await ShowErrorAndNavigateBack("Invalid client ID");
            }
        }

        private async Task LoadClientAsync(int clientId)
        {
            try
            {
                IsLoading = true;
                HasError = false;

                var client = await _databaseService.GetClientMinusJobsWithId(clientId);
                if (client == null)
                {
                    await ShowErrorAndNavigateBack("Client not found");
                    return;
                }
                // Store original values
                _originalName = client.Name;
                _originalPhone = client.Phone;
                _originalAddress = client.Address;

                // Update UI-bound properties
                ClientName = client.Name;
                ClientPhone = client.Phone;
                ClientAddress = client.Address;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load client");
                await ShowErrorAndNavigateBack("Failed to load client data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ShowErrorAndNavigateBack(string message)
        {
            await Shell.Current.DisplayAlert("Error", message, "OK");
            await Shell.Current.GoToAsync("..");
        }

        // ===================== COMMANDS =====================
        [RelayCommand]
        private async Task DeleteClient()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm",
                "Are you sure you want to delete this client?",
                "Yes", "No");

            if (!confirm) return;

            try
            {
                var client = await _databaseService.GetClientByIdAsync(_currentClientId);
                if (client != null)
                {
                    await _databaseService.DeleteClientAsync(client);
                    ContactEvents.OnClientDeleted(client);

                    await Shell.Current.DisplayAlert("Success", "Client deleted", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete client");
                await Shell.Current.DisplayAlert("Error", "Delete failed", "OK");
            }
        }

        [RelayCommand(CanExecute = nameof(IsValid))]
        private async Task SaveClient()
        {
            if (!IsValid) return;

            try
            {
                var client = new Client
                {
                    Id = _currentClientId,
                    Name = ClientName.Trim(),
                    Phone = ClientPhone.Trim(),
                    Address = ClientAddress.Trim()
                };

                bool success = await _databaseService.UpdateClientAsync(client);
                if (success)
                {
                    await Shell.Current.DisplayAlert("Success", "Client updated", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save client");
                await Shell.Current.DisplayAlert("Error", "Save failed", "OK");
            }
        }

        [RelayCommand]
        private async Task Cancel() => await Shell.Current.GoToAsync("..");

        [RelayCommand]
        private async Task ToggleJobsView()
        {
            if (IsJobsListVisible)
                await HideJobs();
            else
                await ShowJobs();
        }

        [RelayCommand]
        private async Task ShowJobs()
        {
            IsClientSectionVisible = false;
            IsShowJobsButtonVisible = false;
            await LoadJobs();
            IsJobsListVisible = true;
        }

        [RelayCommand]
        private async Task HideJobs()
        {
            IsClientSectionVisible = true;
            IsShowJobsButtonVisible = true;
            IsJobsListVisible = false;
            SelectedJob = null;
        }

        [RelayCommand]
        private async Task LoadJobs()
        {
            try
            {
                var jobs = await _databaseService.GetJobsByClientIdAsync(_currentClientId);
                Jobs = new ObservableCollection<JobViewModel>(
                    jobs.Select(j => new JobViewModel
                    {
                        Id = j.Id,
                        Name = j.JobName,
                        Amount = j.Amount,
                        Unit = j.Unit
                    }));

                HasJobs = Jobs.Count > 0;
                NoJobsFound = !HasJobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load jobs");
                await Shell.Current.DisplayAlert("Error", "Failed to load jobs", "OK");
            }
        }

        [RelayCommand]
        private async Task AddNewJob()
        {
            var parameters = new Dictionary<string, object>
            {
                { "ClientId", _currentClientId }
            };
            await Shell.Current.GoToAsync(nameof(AddnewJob), parameters);
        }

        [RelayCommand]
        private async Task EditJob(JobViewModel? job)
        {
            if (job == null) return;

            try
            {
                var parameters = new Dictionary<string, object>
        {
            { "JobId", job.Id },
            { "ClientId", _currentClientId },
            { "JobFromCollection", job }  // This is the crucial addition
        };

                await Shell.Current.GoToAsync("EditJobPage", parameters);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation failed: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Failed to navigate to edit page", "OK");
            }
        }
        [RelayCommand]
        private async Task JobTapped(JobViewModel? job) =>
            await EditJob(job);
    }

    public partial class JobViewModel : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _unit = string.Empty;

        public string FormattedAmount =>$"{Amount} TL";
    }


}