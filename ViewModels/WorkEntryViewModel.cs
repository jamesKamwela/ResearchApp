using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.Models;
using ResearchApp.StaticClasses;
using ResearchApp.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; 
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace ResearchApp.ViewModels
{
    public partial class WorkEntryViewModel : ObservableValidator 
    {
        private readonly IWorkRecordDatabaseService _workRecordDatabaseService;
        private readonly ILogger<WorkEntryViewModel> _logger;
        private readonly IClientDatabaseService _clientDatabaseService;
        private readonly IEmployeeDatabaseService _employeeDatabaseService;
        private readonly ITabNavigationHelper _tabNavigator;

        
        public WorkEntryViewModel(
            IWorkRecordDatabaseService workRecordDatabaseService,
            ILogger<WorkEntryViewModel> logger, 
            ITabNavigationHelper tabNavigator,
            IClientDatabaseService clientDatabaseService,
            IEmployeeDatabaseService employeeDatabaseService)
        {
            WorkDate = DateTime.Today;
            EntryDate = DateTime.Today; 
            IsEntryDateDifferent = false;
            _workRecordDatabaseService = workRecordDatabaseService;
            _logger = logger;
            _clientDatabaseService = clientDatabaseService;
            _employeeDatabaseService = employeeDatabaseService;
            _tabNavigator=tabNavigator;

            InitializeMessaging();
           // Initialize search timers with debounce delay
            _clientSearchTimer = new System.Timers.Timer(SearchDelayMilliseconds) { AutoReset = false };
            _clientSearchTimer.Elapsed += OnClientSearchTimerElapsed;

            _employeeSearchTimer = new System.Timers.Timer(SearchDelayMilliseconds) { AutoReset = false };
            _employeeSearchTimer.Elapsed += OnEmployeeSearchTimerElapsed;

            _logger.LogInformation("WorkEntryViewModel initialized");
            _ = LoadClients();
            _ = LoadEmployees();
            SelectedEmployees.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsWorkDetailsFrameVisible));
                OnPropertyChanged(nameof(IsWorkSummaryFrameVisible));
                OnPropertyChanged(nameof(EmployeeSelectionSummary));
                OnPropertyChanged(nameof(IsEmployeeSearchEnabled));
                OnPropertyChanged(nameof(IsEmployeeSearchCollectionViewEnabled));
            };
        }
        #region Work Entry Properties (Enhanced)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActualWorkDate))]
        private DateTime _workDate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActualWorkDate))]
        private DateTime _entryDate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActualWorkDate))]
        private bool _isEntryDateDifferent;

        public DateTime ActualWorkDate => IsEntryDateDifferent ? EntryDate : WorkDate;

        partial void OnWorkDateChanged(DateTime value)
        {
            // If entry date hasn't been set or matches current work date, update it
            if (!IsEntryDateDifferent || EntryDate == default)
            {
                EntryDate = value;
            }
        }

        partial void OnEntryDateChanged(DateTime value)
        {
            // Automatically determine if dates are different
            IsEntryDateDifferent = value != WorkDate;
        }

        partial void OnIsEntryDateDifferentChanged(bool value)
        {
            // When toggled off, sync the dates
            if (!value)
            {
                EntryDate = WorkDate;
            }
        }
        #endregion
        #region messeging 

        public record ClientAddedMessage(Client Client);
        public record EmployeeAddedMessage(Employee Employee);
        public record WorkRecordSavedMessage(WorkRecord WorkRecord);

        private void InitializeMessaging()
        {
            WeakReferenceMessenger.Default.Register<ClientAddedMessage>(this, (r, m) =>
            {
                _logger.LogDebug("Client added message received");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await LoadClients();
                    // Optionally select the newly added client if needed
                });
            });

            WeakReferenceMessenger.Default.Register<EmployeeAddedMessage>(this, (r, m) =>
            {
                _logger.LogDebug("Employee added message received");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await LoadEmployees();
                    // Optionally select the newly added employee if needed
                });
            });
        }

            #endregion

            #region Client Related Properties

            [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredClients))]
        private string _clientSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Client> _allClients = new();

        [ObservableProperty]
        private ObservableCollection<Client> _filteredClients = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsJobSelectionVisible))]
        [NotifyPropertyChangedFor(nameof(IsSelectedClientFrameVisible))]
        [NotifyPropertyChangedFor(nameof(IsClientSelectionFrameVisible))]
        [NotifyPropertyChangedFor(nameof(IsClientSearchEnabled))]
        private Client? _selectedClient;

        [ObservableProperty]
        private bool _hasClientResults = true;
        public bool IsClientSearchEnabled => SelectedClient == null;
        public bool IsSelectedClientFrameVisible => SelectedClient != null;
        public bool IsClientSelectionFrameVisible => SelectedClient == null;
        #endregion

        #region Job Related Properties


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowNoJobsMessage))]
        private bool _hasJobs;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowNoJobsMessage))]
        [NotifyPropertyChangedFor(nameof(IsJobSelectionVisible))]
        [NotifyPropertyChangedFor(nameof(IsSelectedJobFrameVisible))]
        private ObservableCollection<Job> _clientJobs = new();

        public bool IsSelectedJobFrameVisible => SelectedJob != null;
        public bool ShowNoJobsMessage => SelectedClient != null &&
                                       (ClientJobs == null || ClientJobs.Count == 0);
        public bool IsJobSelectionVisible => !(SelectedClient != null && SelectedJob != null);



        #endregion

        #region Employee Related Properties

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmployeeSelectionFrameVisible))]
        [NotifyPropertyChangedFor(nameof(IsSelectedEmployeesFrameVisible))]
        private bool _isEmployeeSectionFull = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmployeeSearchCollectionViewEnabled))]
        [NotifyPropertyChangedFor(nameof(IsEmployeeSearchEnabled))]
        [NotifyPropertyChangedFor(nameof(EmployeeSelectionSummary))]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private ObservableCollection<Employee> _selectedEmployees = new();


        [ObservableProperty]
        private bool _hasEmployeeResults = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmployeeSearchEnabled))]
        private string _employeeSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Employee> _allEmployees = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWorkDetailsFrameVisible))]
        [NotifyPropertyChangedFor(nameof(IsWorkSummaryFrameVisible))]
        [NotifyPropertyChangedFor(nameof(EmployeeSelectionSummary))]
        [NotifyPropertyChangedFor(nameof(IsEmployeeSearchEnabled))]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(IsEmployeeSearchCollectionViewEnabled))]
        private string _numberOfEmployees = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Employee> _filteredEmployees = new();

        public bool IsJobSelected => SelectedJob != null;

        public bool IsEmployeeSelectionFrameVisible => !IsEmployeeSectionFull;
        public bool IsSelectedEmployeesFrameVisible => SelectedEmployees.Any();

        public bool IsEmployeeSearchEnabled =>int.TryParse(NumberOfEmployees, out int target) && SelectedEmployees.Count < target;

        public bool IsEmployeeSearchCollectionViewEnabled => IsEmployeeSearchEnabled && FilteredEmployees.Any();



        public string EmployeeSelectionSummary
        {
            get
            {
                int.TryParse(NumberOfEmployees, out int target);
                return $"{SelectedEmployees.Count} of {target} selected";
            }
        }


        #endregion

        #region Work Entry Properties
        private readonly System.Timers.Timer _clientSearchTimer;
        private readonly System.Timers.Timer _employeeSearchTimer;
        private const int SearchDelayMilliseconds = 300;
        [ObservableProperty]
        [Range(0.1, double.MaxValue, ErrorMessage = "Quantity must be positive")]
        private double? _quantityCompleted;

     

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(IsJobSelected))]
        [NotifyPropertyChangedFor(nameof(IsJobSelectionVisible))]
        [NotifyPropertyChangedFor(nameof(IsSelectedJobFrameVisible))]
        [NotifyPropertyChangedFor(nameof(WorkSummaryText))]
        [NotifyPropertyChangedFor(nameof(QuantityPlaceholder))]
        private Job? _selectedJob;

        [ObservableProperty]
        private string _quantityPlaceholder = "Enter quantity";

        partial void OnSelectedJobChanged(Job? value)
        {
            QuantityPlaceholder = value != null ? $"{value.Amount} TL per {value.Unit}" : "Enter quantity";
            OnPropertyChanged(nameof(WorkSummaryText));
        }

        [ObservableProperty]
        [Range(0, 100, ErrorMessage = "Commission must be 0-100%")]
        private double _commissionRate = 0;
       
        [ObservableProperty]
        [Range(0.1, double.MaxValue, ErrorMessage = "Quantity must be positive")]
        private double _workQuantity;

        [ObservableProperty]
        [Range(0, 100, ErrorMessage = "Commission must be 0-100%")]
        private double _workCommissionRate = 0;



        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWorkDetailsFrameVisible))]
        [NotifyPropertyChangedFor(nameof(IsWorkSummaryFrameVisible))]
        private string _workDescription;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWorkDetailsFrameVisible))]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(IsWorkSummaryFrameVisible))]
        private bool _workDetailsCompleted;

        public bool IsWorkDetailsFrameVisible
        {
            get
            {
                var result = int.TryParse(NumberOfEmployees, out int target) &&
                           SelectedEmployees.Count == target &&
                           !WorkDetailsCompleted;

                _logger.LogDebug($"IsWorkDetailsFrameVisible: {result} " +
                                $"(Target: {target}, Selected: {SelectedEmployees.Count}, " +
                                $"DetailsCompleted: {WorkDetailsCompleted})");

                return result;
            }
        }
        public bool IsWorkSummaryFrameVisible => WorkDetailsCompleted;

        public string WorkSummaryText
        {
            get
            {
                if (SelectedJob == null) return string.Empty;

                var totalAmount = (decimal)QuantityCompleted * SelectedJob.Amount;
                var summary = $"Work Details Summary:\n" +
                             $"• Work Date: {WorkDate:dd MMM yyyy}\n";

                if (IsEntryDateDifferent)
                {
                    summary += $"• Entry Date: {EntryDate:dd MMM yyyy}\n";
                }

                summary += $"• Quantity: {QuantityCompleted} {SelectedJob.Unit}\n" +
                           $"• Commission: {CommissionRate}%\n" +
                           $"• Price per {SelectedJob.Unit}: {SelectedJob.Amount:N2} TL\n" +
                           $"• Total Amount: {totalAmount:N2} TL";

                return summary;
            }
        }

        public bool IsFormValid
        {
            get
            {
                bool isValid = SelectedClient != null &&
                              SelectedJob != null &&
                              SelectedEmployees.Count > 0 &&
                              WorkDetailsCompleted &&
                              ActualWorkDate <= DateTime.Today; // Ensure date isn't in future

                _logger.LogDebug($"IsFormValid: {isValid} | " +
                                $"Client: {SelectedClient != null}, " +
                                $"Job: {SelectedJob != null}, " +
                                $"Employees: {SelectedEmployees.Count}, " +
                                $"Quantity: {QuantityCompleted}, " +
                                $"WorkDate: {ActualWorkDate}");

                return isValid;
            }
        }


        #endregion
        #region Client Commands
        [RelayCommand]
        private async Task LoadClients()
        {
            try
            {
                _logger.LogInformation("Loading clients...");
                var clients = await _clientDatabaseService.GetClientsAsync();
                AllClients = new ObservableCollection<Client>(clients);
                FilteredClients = new ObservableCollection<Client>(clients);
                _logger.LogInformation($"Loaded {clients.Count} clients");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load clients");
                await Shell.Current.DisplayAlert("Error", "Failed to load clients", "OK");
            }
        }
        partial void OnClientSearchTextChanged(string value)
        {
            _logger.LogDebug("Client search text changed: {Text}", value);
            _clientSearchTimer.Stop();
            _clientSearchTimer.Start();
        }

        [RelayCommand]
        private void ClearClientSelection()
        {
            _logger.LogInformation("Clearing client selection");
            SelectedClient = null;
            ClientSearchText = string.Empty;
            FilteredClients = new ObservableCollection<Client>(AllClients);
            SelectedJob = null;
        }

        partial void OnSelectedClientChanged(Client value)
        {
            if (value != null)
            {
                _logger.LogInformation("Client selected: {ClientName}", value.Name);
                LoadJobsCommand.Execute(null);
                ClientSearchText = string.Empty;
            }
            else
            {
                _logger.LogDebug("Client selection cleared");
                ClientJobs.Clear();
                SelectedJob = null;
            }
        }

        [RelayCommand]
        private void SelectClient(Client client)
        {
            if (client != null)
            {
                _logger.LogDebug("Selecting client: {ClientName}", client.Name);
                SelectedClient = client;
            }
        }

        private void OnClientSearchTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(FilterClients);
        }

        private void FilterClients()
        {
            try
            {
                _logger.LogDebug("Filtering clients with search text: {Text}", ClientSearchText);

                if (string.IsNullOrWhiteSpace(ClientSearchText))
                {
                    FilteredClients = new ObservableCollection<Client>(AllClients);
                    HasClientResults = AllClients.Any();
                }
                else
                {
                    var filtered = AllClients
                        .Where(c => c.Name.Contains(ClientSearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    FilteredClients = new ObservableCollection<Client>(filtered);
                    HasClientResults = filtered.Any();
                }

                _logger.LogDebug("Client filtering completed. Results: {Count}", FilteredClients.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering clients");
            }
        }
        #endregion

        #region Job Commands
     
        [RelayCommand]
        private async Task LoadJobs()
        {
            if (SelectedClient == null) return;

            try
            {
                var jobs = await _clientDatabaseService.GetJobsByClientIdAsync(SelectedClient.Id);
                ClientJobs.Clear();
                HasJobs = jobs.Any();

                if (HasJobs)
                {
                    foreach (var job in jobs)
                    {
                        ClientJobs.Add(job);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load jobs");
            }
        }

        [RelayCommand]
        private void ClearJobSelection()
        {
            _logger.LogDebug("Clearing job selection");
            SelectedJob = null;
        }

        [RelayCommand]
        private void SelectJob()
        {
            _logger.LogInformation("Job selected: {JobName}", SelectedJob?.JobName);
        }
        #endregion

        #region Employee Commands/Methods


        [RelayCommand]
        private async Task LoadEmployees()
        {
            try
            {
                _logger.LogInformation("Loading employees...");
                var employees = await _employeeDatabaseService.GetAllEmployeesAsync(); 
                AllEmployees = new ObservableCollection<Employee>(employees);

                FilterEmployees();
               _logger.LogInformation($"Loaded {employees.Count} employees");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load employees");
                await Shell.Current.DisplayAlert("Error", "Failed to load employees", "OK");
            }
        }
        [RelayCommand]
        private void SelectEmployee(Employee employee)
        {
            if (employee != null && !SelectedEmployees.Contains(employee))
            {
                SelectedEmployees.Add(employee);
                UpdateEmployeeDependentProperties();
            }
        }
        [RelayCommand]
        private void RemoveEmployee(Employee employee)
        {
            if (employee != null)
                SelectedEmployees.Remove(employee);
                UpdateEmployeeDependentProperties();

        }

        partial void OnEmployeeSearchTextChanged(string value)
        {
            _employeeSearchTimer.Stop();
            _employeeSearchTimer.Start();
        }

        [RelayCommand]
        private void FilterEmployees()
        {
            try
            {
                _logger.LogDebug("Filtering employees with search text: {Text}", EmployeeSearchText);

                if (string.IsNullOrWhiteSpace(EmployeeSearchText))
                {
                    FilteredEmployees = new ObservableCollection<Employee>(AllEmployees);
                    HasEmployeeResults = AllEmployees.Any();
                }
                else
                {
                    var filtered = AllEmployees
                        .Where(e => e.Name.Contains(EmployeeSearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    FilteredEmployees = new ObservableCollection<Employee>(filtered);
                    HasEmployeeResults = filtered.Any();
                }

                _logger.LogDebug("Employee filtering completed. Results: {Count}", FilteredEmployees.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering employees");
            }
        }

        partial void OnNumberOfEmployeesChanged(string value)
        {
            UpdateEmployeeDependentProperties();
            UpdateEmployeeSectionFullProperty();
        }


        private void OnEmployeeSearchTimerElapsed(object? sender, ElapsedEventArgs e)
        {
           MainThread.BeginInvokeOnMainThread(FilterEmployees);
        }
        private void UpdateEmployeeDependentProperties()
        {
            OnPropertyChanged(nameof(IsEmployeeSearchEnabled));
            OnPropertyChanged(nameof(IsEmployeeSearchCollectionViewEnabled));
            OnPropertyChanged(nameof(EmployeeSelectionSummary));
            OnPropertyChanged(nameof(IsEmployeeSectionFull));
            OnPropertyChanged(nameof(IsWorkDetailsFrameVisible));
            OnPropertyChanged(nameof(IsSelectedEmployeesFrameVisible));
            OnPropertyChanged(nameof(IsEmployeeSelectionFrameVisible));
        }

        private void UpdateEmployeeSectionFullProperty()
        {
            if (int.TryParse(NumberOfEmployees, out int target) && target > 0)
            {
                IsEmployeeSectionFull = SelectedEmployees.Count >= target;
            }
            else
            {
                IsEmployeeSectionFull = false;
            }
        }

        #endregion

        #region Work Entry Commands
  
        [RelayCommand]
        private void SaveWorkDetails()
        {
            WorkDetailsCompleted = true;
            OnPropertyChanged(nameof(WorkSummaryText)); // Force UI refresh
        }

        [RelayCommand]
        private void EditWorkDetails() => WorkDetailsCompleted = false;


        [RelayCommand]
        private async Task SaveWorkEntry()
        {
            if (!IsFormValid)
            {
                var errors = string.Join("\n", GetErrors().Select(e => e.ErrorMessage));
                await Shell.Current.DisplayAlert("Validation Errors",
                    $"Please fix these issues:\n{errors}", "OK");
                return;
            }

            bool confirmation = await Shell.Current.DisplayAlert("Confirm Save",
                "Are you sure you want to save this work entry?", "Yes", "No");
            if (!confirmation) return;

            if (SelectedClient == null || SelectedClient.Id <= 0)
            {
                await Shell.Current.DisplayAlert("Error", "Please select a valid client", "OK");
                return;
            }
            try
            {
                decimal quantity = (decimal)QuantityCompleted;
                decimal rate = (decimal)CommissionRate;
                decimal totalAmount = quantity * SelectedJob.Amount;
                decimal adminCommission = totalAmount * (rate / 100);
                decimal employeePool = totalAmount - adminCommission;
                decimal amountPerEmployee = SelectedEmployees.Count > 0 ? employeePool / SelectedEmployees.Count : 0;

                var workRecord = new WorkRecord
                {
                    WorkDate = ActualWorkDate,
                    EntryDate = EntryDate,
                    Unit = SelectedJob?.Unit ?? "Unit",
                    ClientId = SelectedClient!.Id,
                    ClientName = SelectedClient.Name,
                    JobId = SelectedJob!.Id,
                    JobName = SelectedJob.JobName,
                    QuantityCompleted = quantity,
                    CommissionRate = rate,
                    TotalAmount = totalAmount,
                    EmployeeCount = SelectedEmployees.Count,
                    IsJobCompleted = false,
                    IsPaid = false,
                    AdminCommission = adminCommission,
                    EmployeePool = employeePool,
                    AmountPerEmployee = amountPerEmployee,
                    CreatedAt = DateTime.UtcNow
                };

                // Detailed logging of all properties
                _logger.LogInformation("Saving WorkRecord with properties:");
                _logger.LogInformation($"- WorkDate: {workRecord.WorkDate}");
                _logger.LogInformation($"- EntryDate: {workRecord.EntryDate}");
                _logger.LogInformation($"- ClientId: {workRecord.ClientId}");
                _logger.LogInformation($"- ClientName: {workRecord.ClientName}");
                _logger.LogInformation($"- JobId: {workRecord.JobId}");
                _logger.LogInformation($"- JobName: {workRecord.JobName}");
                _logger.LogInformation($"- QuantityCompleted: {workRecord.QuantityCompleted}");
                _logger.LogInformation($"- CommissionRate: {workRecord.CommissionRate}");
                _logger.LogInformation($"- TotalAmount: {workRecord.TotalAmount}");
                _logger.LogInformation($"- EmployeeCount: {workRecord.EmployeeCount}");
                _logger.LogInformation($"- AdminCommission: {workRecord.AdminCommission}");
                _logger.LogInformation($"- EmployeePool: {workRecord.EmployeePool}");
                _logger.LogInformation($"- AmountPerEmployee: {workRecord.AmountPerEmployee}");
                _logger.LogInformation($"- CreatedAt: {workRecord.CreatedAt}");

                // Log selected employees
                _logger.LogInformation($"Selected Employees ({SelectedEmployees.Count}):");
                foreach (var employee in SelectedEmployees)
                {
                    _logger.LogInformation($"- EmployeeId: {employee.Id}, Name: {employee.Name}");
                }


                var employeeWorkRecords = SelectedEmployees.Select(employee => new EmployeeWorkRecord
                {
                    EmployeeId = employee.Id,
                    AddedDate = DateTime.UtcNow
                }).ToList();

                var clientWorkRecord = new ClientWorkRecord
                {
                    ClientId = SelectedClient.Id,
                    AddedDate = DateTime.UtcNow,
                    IsPaid = false,
                };

                // Save everything in a single transaction
                var savedWorkRecordId = await _workRecordDatabaseService.SaveWorkRecordWithEmployeesInTransactionAsync(workRecord, employeeWorkRecords, clientWorkRecord);

                if (savedWorkRecordId <= 0)
                {
                    throw new Exception("Failed to save WorkRecord - no ID returned");
                }
                //await SaveToEmployeeDatabase(savedWorkRecordId);

                _logger.LogInformation($"WorkRecord and employee relationships saved successfully with ID: {savedWorkRecordId}");

             
                await Shell.Current.DisplayAlert("Success", "Work record saved successfully", "OK");

                // Reset the form
                ResetForm();

                // Notify PendingJobsViewModel to refresh
                MessagingCenter.Send(this, "WorkRecordSaved");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving work record");
                await Shell.Current.DisplayAlert("Error",
                    "Failed to save work record. Please try again.", "OK");
            }
        }

        private void ResetForm()
        {
            // Reset all form fields
            SelectedClient = null;
            SelectedJob = null;
            SelectedEmployees.Clear();
            QuantityCompleted = 0;
            CommissionRate = 0;
            WorkDate = DateTime.Today;
            EntryDate = DateTime.Today;
            IsEntryDateDifferent = false;
            WorkDetailsCompleted = false;
            NumberOfEmployees = string.Empty;
            WorkDescription = string.Empty;

            // Reset search fields
            ClientSearchText = string.Empty;
            EmployeeSearchText = string.Empty;

            // Reload initial data
            LoadClientsCommand.Execute(null);
            LoadEmployeesCommand.Execute(null);
        }
     

        [RelayCommand]
        private async Task Cancel()
        {
            _logger.LogInformation("Canceling work entry");
            await Shell.Current.GoToAsync("..");
        }
    
            #endregion
        }
}