using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ResearchApp;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using ResearchApp.Utils;
using ResearchApp.ViewModels;
using ResearchApp.Views;
using SQLite;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using static ResearchApp.ViewModels.ContactsViewModel;



public partial class AddClientViewModel : ObservableObject
{
    // =============================================
    // 1. INITIALIZATION & DEPENDENCIES
    // =============================================
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly IClientDatabaseService _databaseService;
    private readonly ILogger<AddClientViewModel> _logger;
    private readonly Stack<Job> _undoStack = new();

    public AddClientViewModel(IClientDatabaseService databaseService,
                            ILogger<AddClientViewModel> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
        Jobs = new ObservableCollection<Job>();
        ResetForm();
        // LoadClientsWithJobs();
        // ResetDatabaseNow();
    }
    // =============================================
    // 2. PROPERTIES & VALIDATION
    // =============================================
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isJobSectionVisible;
    [ObservableProperty] private bool isClientSaveExitVisible;
    [ObservableProperty] private bool isClientSaved;
    [ObservableProperty] private bool newJobAddedEnabled;
    [ObservableProperty] private int clientId;
    [ObservableProperty] private bool isnewJobAddedVisible;

    //summery properties
    [ObservableProperty]
    private bool isSummaryVisible;

    [ObservableProperty]
    private string clientSummary;

    [ObservableProperty]
    private string jobsSummary;

    public string FullSummary => $"{ClientSummary}\n\n{JobsSummary}";

    // Client Properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string clientName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string clientAddress = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string clientPhone = string.Empty;

    // Job Properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJobValid))]
    private string jobName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJobValid))]
    private string jobAmount = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJobValid))]
    private string selectedJobUnit;

    public ObservableCollection<Job> Jobs { get; }
    public bool IsValid => ValidateClient();
    public bool IsJobValid => ValidateJob();
    public bool IsPhoneValid => ValidatePhone(ClientPhone);




    [ObservableProperty]
    private bool isJobSectionSaveCancelVisible;



    [ObservableProperty]
    private bool isClientSectionVisible;

    [ObservableProperty]
    private bool isClientSectionSaveCancelVisible;

    // =============================================
    // 3. VALIDATION LOGIC
    // =============================================
    private bool ValidateClient() =>
        !string.IsNullOrWhiteSpace(ClientName) &&
        ClientName.Length is >= 3 and <= 100 &&
        !string.IsNullOrWhiteSpace(ClientAddress) &&
        ClientAddress.Length is >= 1 and <= 200 &&
        IsPhoneValid;

    private bool ValidatePhone(string phone) =>
        !string.IsNullOrWhiteSpace(phone) &&
        phone.Length is >= 7 and <= 15 &&
        phone.All(c => char.IsDigit(c) || c == '+' || c == ' ' || c == '-');

    private bool ValidateJob() =>
        !string.IsNullOrWhiteSpace(JobName) &&
        JobName.Length is >= 3 and <= 100 &&
        ValidateJobAmount(JobAmount) &&
        !string.IsNullOrWhiteSpace(SelectedJobUnit);

    private bool ValidateJobAmount(string amount) =>
        decimal.TryParse(amount?.Replace(',', '.'),
                       NumberStyles.Any,
                       CultureInfo.InvariantCulture,
                       out decimal result) && result > 0;
    // =============================================
    // 4. CLIENT OPERATIONS
    // =============================================

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
                IsnewJobAddedVisible = true; // Show "Add a new job" button
                newJobAddedEnabled = true; // Enable the "Add a new job" button
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
                ResetForm();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while canceling the client.");
                await App.Current.MainPage.DisplayAlert("Error", $"An error occurred while canceling: {ex.Message}", "OK");
            }
        }
    }
    // =============================================
    // 5. JOB OPERATIONS
    // =============================================


    [RelayCommand]
    private void AddJob()
    {
        try
        {
            IsJobSectionVisible = true;
            IsJobSectionSaveCancelVisible = true;
            IsnewJobAddedVisible = false;
            NewJobAddedEnabled = false;
            IsClientSaveExitVisible = false;
            IsClientSectionSaveCancelVisible = false;
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
        if (!IsJobValid)
        {
            await ShowValidationError("Please fix job errors");
            return;
        }

        bool confirm = await App.Current.MainPage.DisplayAlert(
            "Confirm Save",
            $"Are you sure you want to save the following Job?\n\nName: {JobName}\nAmount: {JobAmount}\nUnit: {SelectedJobUnit}",
            "Yes", "No");

        try
        {
            if (confirm)
            {
                var job = new Job
                {
                    JobName = JobName.CapitalizeFirstLetter(),
                    Amount = decimal.Parse(JobAmount.Replace(',', '.')),
                    Unit = SelectedJobUnit,
                    ClientId = ClientId
                };
                await _databaseService.SaveJobsAsync(new List<Job> { job });
                Jobs.Add(job);

                ClearJobFields();
                IsJobSectionVisible = false;
                IsnewJobAddedVisible = true;
                NewJobAddedEnabled = true;
                IsClientSaveExitVisible = true;
                GenerateSummaries();
                await ShowSuccess("Job saved!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job save failed");
            await ShowError($"Failed to save job: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelJob()
    {
        try
        {
            ClearJobFields();
            IsJobSectionVisible = false;
            IsJobSectionSaveCancelVisible = false;
            IsClientSectionSaveCancelVisible = false;
            IsClientSaveExitVisible = true; // Show bottom buttons after canceling job
            NewJobAddedEnabled = true; // Enable the "Add a new job" button
            _logger.LogInformation("Job section closed without saving.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while canceling the job.");
            Debug.WriteLine($"An error occurred while canceling the job: {ex.Message}");
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
 

    // =============================================
    // 6. FINALIZATION OPERATIONS
    // =============================================
    [RelayCommand]
    private async Task SaveAndExit()
    {
        if (IsBusy) return;
        IsBusy = true;

        GenerateSummaries();

        bool confirm = await App.Current.MainPage.DisplayAlert(
            "Review Before Finishing",
            FullSummary,
            "Confirm and Finish",
            "Continue Editing");

        if (!confirm)
        {
            IsBusy = false;
            return;
        }

        await _saveLock.WaitAsync();
        try
        {
            if (!IsClientSaved)
            {
                await ShowValidationError("Please save client first");
                return;
            }

            if (Jobs.Count == 0)
            {
                bool proceed = await App.Current.MainPage.DisplayAlert(
                    "No Jobs Added",
                    "You haven't added any jobs. Are you sure you want to finish?",
                    "Yes, Finish Anyway", "Add Jobs");

                if (!proceed) return;
            }
            var client = await _databaseService.GetClientByIdAsync(ClientId);
            WeakReferenceMessenger.Default.Send(new ClientAddedMessage(client));

            ResetForm();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SaveAndExit");
            await App.Current.MainPage.DisplayAlert("Error", "An error occurred while saving. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
            _saveLock.Release();
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

    //==============================================
    //7.Summery
    //==============================================
    private void GenerateSummaries()
    {
        // Client summary
        ClientSummary = $"Client: {ClientName}\n" +
                       $"Address: {ClientAddress}\n" +
                       $"Phone: {ClientPhone}";

        // Jobs summary
        if (Jobs.Count == 0)
        {
            JobsSummary = "No jobs added";
        }
        else
        {
            var jobsBuilder = new StringBuilder("Jobs:\n");
            for (int i = 0; i < Jobs.Count; i++)
            {
                jobsBuilder.AppendLine($"{i + 1}. {Jobs[i].JobName} - {Jobs[i].Amount} TL per {Jobs[i].Unit}");
            }
            JobsSummary = jobsBuilder.ToString();
        }

        OnPropertyChanged(nameof(FullSummary));
    }


    // =============================================
    // 8. HELPER METHODS
    // =============================================
    private void ClearJobFields()
    {
        JobName = string.Empty;
        JobAmount = string.Empty;
        SelectedJobUnit = null;
    }

    private void ResetForm()
    {
        try
        {
            // Clear client fields
            ClientName = ClientAddress = ClientPhone = string.Empty;
            ClearJobFields();
            Jobs.Clear();

            // Clear job fields
            JobName = string.Empty;
            JobAmount = string.Empty;
            SelectedJobUnit = null;


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
    private async Task ShowError(string message) =>
      await App.Current.MainPage.DisplayAlert("Error", message, "OK");

    private async Task ShowValidationError(string message) =>
        await App.Current.MainPage.DisplayAlert("Validation", message, "OK");

    private async Task<bool> ConfirmAction(string message) =>
        await App.Current.MainPage.DisplayAlert("Confirm", message, "Yes", "No");

    public void Dispose()
    {
        _undoStack.Clear();
        Jobs.Clear();
        _saveLock.Dispose();
        MessagingCenter.Unsubscribe<Client>(this, "ClientSaved");
    }
    

    private async Task ShowSuccess(string message)
    {
        await App.Current.MainPage.DisplayAlert("Success", message, "OK");
    }


}