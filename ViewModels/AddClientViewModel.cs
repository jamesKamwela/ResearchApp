using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SQLite;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using ResearchApp;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ResearchApp.ViewModels;
using ResearchApp.Utils;
using CommunityToolkit.Mvvm.Messaging;
using static ResearchApp.ViewModels.ContactsViewModel;
using System.Net;



public partial class AddClientViewModel : ObservableObject
{
    private readonly IClientDatabaseService _databaseService;
    //private readonly IJobDatabaseService _jobdatabaseService;

    private readonly ILogger<AddClientViewModel> _logger;
    private Stack<Job> _undoStack = new Stack<Job>();
    public AddClientViewModel(IClientDatabaseService databaseService, IJobDatabaseService jobdatabaseService, 
        ILogger<AddClientViewModel> logger)
    {
        _databaseService = databaseService;
        //_jobdatabaseService = jobdatabaseService;
        _logger = logger;
        Jobs = new ObservableCollection<Job>();
        ResetForm(); // Initialize the form to its default state
        // LoadClientsWithJobs();
        //ResetDatabaseNow();
    }

    [ObservableProperty]
    private bool isBusy;

   

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))] // Notify when IsValid changes
    private string clientName = string.Empty;

    [ObservableProperty]
    private bool isJobSectionSaveCancelVisible;

    [ObservableProperty]
    private bool newJobAddedEnabled;

    public ObservableCollection<Job> Jobs { get; }

    [ObservableProperty]
    private int clientId;

    [ObservableProperty]
    private bool isClientSaveExitVisible;

    [ObservableProperty]
    private bool isClientSectionVisible;

    [ObservableProperty]
    private bool isClientSaved;

    [ObservableProperty]
    private bool isClientSectionSaveCancelVisible;

    [ObservableProperty]
    private bool isJobSectionVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))] // Notify when IsValid changes
    private string clientAddress = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))] // Notify when IsValid changes
    private string clientPhone = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJobValid))] // Notify when IsJobValid changes
    private string jobName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJobValid))] // Notify when IsJobValid changes
    private string jobAmount = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJobValid))] // Notify when IsJobValid changes
    private string selectedJobUnit;

    public bool IsValid => ValidateClient(); // Client validation
    public bool IsJobValid => ValidateJob(); // Job validation



    private async void ResetDatabaseNow()
    {
        try
        {
            await _databaseService.ResetDatabaseAsync();
            Debug.WriteLine("Database reset successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error resetting database: {ex.Message}");
        }
    }
    public async Task LoadClientsWithJobs()
    {
        try
        {
            // Fetch all clients with their jobs
            var clientsWithJobs = await _databaseService.GetClientsAsync();

            // Check if there are any clients
            if (clientsWithJobs == null || clientsWithJobs.Count == 0)
            {
                Debug.WriteLine("No clients found.");
                return;
            }

            // Loop through each client
            foreach (var client in clientsWithJobs)
            {
                // Print client details
                Debug.WriteLine($"Client ID: {client.Id}");
                Debug.WriteLine($"Client Name: {client.Name}");
                Debug.WriteLine($"Client Address: {client.Address}");
                Debug.WriteLine($"Client Phone: {client.Phone}");
                Debug.WriteLine("Jobs:");

                // Check if the client has any jobs
                if (client.Jobs == null || client.Jobs.Count == 0)
                {
                    Debug.WriteLine("  No jobs found for this client.");
                }
                else
                {
                    // Loop through each job for the client
                    foreach (var job in client.Jobs)
                    {
                        Debug.WriteLine($"  Job ID: {job.Id}");
                        Debug.WriteLine($"  Job Name: {job.JobName}");
                        Debug.WriteLine($"  Amount: {job.Amount:C}");
                        Debug.WriteLine($"  Unit: {job.Unit}");
                        Debug.WriteLine(""); // Add a blank line for readability
                    }
                }

                Debug.WriteLine(new string('-', 40)); // Separator between clients
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"An error occurred while fetching or printing clients and jobs: {ex.Message}");
        }
    }


    private bool ValidateClient()
    {
        return !string.IsNullOrWhiteSpace(ClientName) &&
               ClientName.Length >= 3 &&
               ClientName.Length <= 100 &&
               !string.IsNullOrWhiteSpace(ClientAddress) &&
               ClientAddress.Length >= 1 &&
               ClientAddress.Length <= 200 &&
               !string.IsNullOrWhiteSpace(ClientPhone) &&
               ClientPhone.Length == 10 &&
               ClientPhone.All(char.IsDigit);
    }

    private bool ValidateJob()
    {
        return !string.IsNullOrWhiteSpace(JobName) &&
               JobName.Length >= 3 &&
               JobName.Length <= 100 &&
               !string.IsNullOrWhiteSpace(JobAmount) &&
               decimal.TryParse(JobAmount, out _) &&
               !string.IsNullOrWhiteSpace(SelectedJobUnit);
    }

    [RelayCommand]
    private async Task SaveClient()
    {
        if (!ValidateClient())
        {
            await App.Current.MainPage.DisplayAlert("Validation Error", "Please enter valid client details.", "OK");
            return;
        }

        // Confirm with the user before saving
        bool confirm = await App.Current.MainPage.DisplayAlert(
            "Confirm Save",
            $"Are you sure you want to save the following client?\n\nName: {ClientName}\nAddress: {ClientAddress}\nPhone: {ClientPhone}",
            "Yes", "No");

        if (confirm)
        {
            try
            {
                var client = new Client
                {
                    Name = ClientName.CapitalizeFirstLetter(),
                    Address = ClientAddress.CapitalizeFirstLetter(),
                    Phone = ClientPhone,
                    //Jobs = Jobs.ToList() // Assign the jobs to the client
                };

                // Save the client and store the generated ID
                await _databaseService.SaveClientAsync(client);
                ClientId = client.Id; // Store the client ID for later use
                IsClientSaved = true;
                IsClientSaveExitVisible = false; // Show bottom buttons after saving client
                await App.Current.MainPage.DisplayAlert("Success", "Client saved successfully.", "OK");

                NewJobAddedEnabled = true; // Enable the "Add a new job" button

                // Do not clear client fields
                IsJobSectionVisible = false;
                IsClientSectionSaveCancelVisible = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving the client.");
                await App.Current.MainPage.DisplayAlert("Error", $"An error occurred while saving the client: {ex.Message}", "OK");
            }
        }
    }

    [RelayCommand]
    private async Task CancelClient()
    {
        bool confirm = await App.Current.MainPage.DisplayAlert("Confirm Cancellation", "Are you sure you want to cancel?", "Yes", "No");
        if (confirm)
        {
            try
            {
                // Clear client fields
                ClientName = string.Empty;
                ClientAddress = string.Empty;
                ClientPhone = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while canceling the client.");
                await App.Current.MainPage.DisplayAlert("Error", $"An error occurred while canceling: {ex.Message}", "OK");
            }
        }
    }

    [RelayCommand]
    private void AddJob()
    {
        try
        {
            IsJobSectionVisible = true; // Show job section
            IsJobSectionSaveCancelVisible = true;
            IsClientSectionSaveCancelVisible = false;
            IsClientSaveExitVisible = false; // Hide bottom buttons
            NewJobAddedEnabled = false; // Disable "Add a new job" button while adding a job

            _logger.LogInformation("Job section opened for adding a new job.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while adding a job.");
            Debug.WriteLine($"An error occurred while adding a job: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveJob()
    {
        if (!ValidateJob())
        {
            await App.Current.MainPage.DisplayAlert("Validation Error", "Please enter valid job details.", "OK");
            return;
        }

        // Confirm with the user before saving
        bool confirm = await App.Current.MainPage.DisplayAlert(
            "Confirm Job",
            $"Job Name: {JobName}\nAmount: {JobAmount}\nUnit: {SelectedJobUnit}",
            "Yes", "No");

        if (confirm)
        {
            try
            {
                if (ClientId <= 0)
                {
                    await App.Current.MainPage.DisplayAlert("Error", "No client selected. Please save the client first.", "OK");
                    return;
                }

                var job = new Job
                {
                    JobName = JobName.CapitalizeFirstLetter(),
                    Amount = decimal.Parse(JobAmount),
                    Unit = SelectedJobUnit,
                    ClientId = ClientId // Assign the existing client's ID to the job
                };

                // Save the job to the database
                //await _databaseService.SaveJobsAsync(job);

                // Add the job to the observable collection
                Jobs.Add(job);
                await App.Current.MainPage.DisplayAlert("Success", "Job added successfully.", "OK");

                // Clear job fields and hide job section
                JobName = string.Empty;
                JobAmount = string.Empty;
                SelectedJobUnit = null;
                IsJobSectionVisible = false; // Hide job section
                NewJobAddedEnabled = true; // Re-enable the "Add a new job" button
                IsJobSectionSaveCancelVisible = false;
                IsClientSaveExitVisible = true;

                _logger.LogInformation("Job saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving the job.");
                await App.Current.MainPage.DisplayAlert("Error", $"An error occurred while saving the job: {ex.Message}", "OK");
            }
        }
    }
    [RelayCommand]
    private async Task SaveAndExit()
    {
        if (IsBusy) return; // Prevent multiple simultaneous saves

        IsBusy = true; // Set the busy flag

        try
        {
            if (!ValidateClient() || Jobs.Count == 0)
            {
                await App.Current.MainPage.DisplayAlert("Validation Error", "Please enter valid client and job details.", "OK");
                return;
            }

            // Fetch the last client from the database
            var lastClient = await _databaseService.GetLastClientAsync();

            // Check if the last client has the same name, phone, and address
            if (lastClient != null &&
                lastClient.Name == ClientName &&
                lastClient.Phone == ClientPhone &&
                lastClient.Address == ClientAddress)
            {
                // Add jobs to the last client
                foreach (var job in Jobs)
                {
                    job.ClientId = lastClient.Id; // Assign the last client's ID to the job
                }

                // Save the jobs to the last client
                await _databaseService.SaveJobsAsync(Jobs.ToList());


                var updatedClient = await _databaseService.GetClientByIdAsync(lastClient.Id);
                WeakReferenceMessenger.Default.Send(new ClientAddedMessage(updatedClient));
                await App.Current.MainPage.DisplayAlert("Success", "Jobs added to the existing client successfully.", "OK");

            }
            else
            {
                // Create a new client and save it with the jobs
                var client = new Client
                {
                    Name = ClientName,
                    Address = ClientAddress,
                    Phone = ClientPhone,
                    Jobs = Jobs.ToList() // Assign the jobs to the client
                };

                // Save the client and their jobs
                var newClient= await _databaseService.SaveClientAsync(client);
                WeakReferenceMessenger.Default.Send(new ClientAddedMessage(newClient));

                await App.Current.MainPage.DisplayAlert("Success", "New client and jobs saved successfully.", "OK");

            }

            // Reset the form to its initial state
            ResetForm();
            await Shell.Current.GoToAsync("..");
            

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while saving and exiting.");
            await App.Current.MainPage.DisplayAlert("Error", $"An error occurred while saving and exiting: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false; // Reset the busy flag
        }
    }
   
    [RelayCommand]
    private async Task CancelAndExit()
    {
        bool confirm = await App.Current.MainPage.DisplayAlert("Confirm Cancellation", "Are you sure you want to cancel?", "Yes", "No");
        if (confirm)
        {
            try
            {
                ResetForm();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while canceling and exiting.");
                await App.Current.MainPage.DisplayAlert("Error", $"An error occurred while canceling and exiting: {ex.Message}", "OK");
            }
        }
    }

    [RelayCommand]
    private void UndoLastJob()
    {
        try
        {
            if (Jobs.Count > 0)
            {
                var lastJob = Jobs.Last();
                _undoStack.Push(lastJob);
                Jobs.Remove(lastJob);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while undoing the last job.");
            Debug.WriteLine($"An error occurred while undoing the last job: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RedoLastJob()
    {
        try
        {
            if (_undoStack.Count > 0)
            {
                var lastUndoneJob = _undoStack.Pop();
                Jobs.Add(lastUndoneJob);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while redoing the last job.");
            Debug.WriteLine($"An error occurred while redoing the last job: {ex.Message}");
        }
    }

    private void ResetForm()
    {
        try
        {
            // Clear client fields
            ClientName = string.Empty;
            ClientAddress = string.Empty;
            ClientPhone = string.Empty;

            // Clear job fields
            JobName = string.Empty;
            JobAmount = string.Empty;
            SelectedJobUnit = null;

            // Clear the Jobs collection
            Jobs.Clear();

            // Reset visibility states
            IsJobSectionVisible = false;
            IsClientSectionVisible = true;
            IsClientSaveExitVisible = false;
            IsJobSectionSaveCancelVisible = false;
            IsClientSectionSaveCancelVisible = true;

            // Reset other states
            IsClientSaved = false;
            NewJobAddedEnabled = false;
            ClientId = 0; // Reset the client ID

            _logger.LogInformation("Form reset successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while resetting the form.");
            Debug.WriteLine($"An error occurred while resetting the form: {ex.Message}");
        }
    }
}