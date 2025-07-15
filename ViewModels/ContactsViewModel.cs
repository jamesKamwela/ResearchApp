#region usings

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls; 
using Microsoft.Maui.Graphics;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using ResearchApp.StaticClasses;
using ResearchApp.Utils;
using ResearchApp.Views;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using static ResearchApp.ViewModels.EditEmployeeViewModel;
#endregion

namespace ResearchApp.ViewModels
    {
        public partial class ContactsViewModel : ObservableObject, IDisposable
        {

        /* =============================================
           * TASK: DEPENDENCY INJECTION AND INITIALIZATION
           * =============================================
           * This section handles the setup of required services
           * and initial data loading when the ViewModel is created.
           * It uses constructor injection to get database services
           * and logger, then starts loading contacts.
           */
        #region dependencies
        
        private readonly IClientDatabaseService _clientDatabaseService;
            private readonly IEmployeeDatabaseService _employeeDatabaseService;
            private readonly ILogger<ContactsViewModel> _logger;

          

            public ContactsViewModel(
                IClientDatabaseService clientDatabaseService,
                IEmployeeDatabaseService employeeDatabaseService,
                ILogger<ContactsViewModel> logger)
            {
            _clientDatabaseService = clientDatabaseService;
                _employeeDatabaseService = employeeDatabaseService;
                _logger = logger;

                _logger.LogInformation("ViewModel initialized");
                try
                {
                    InitializeMessaging(); // Subscribe to messages for adding new contacts
                    _ = LoadContactsAsync(); // Load initial contacts from the database
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ViewModel initialization failed: {ex}");
                    throw; // Re-throw to see the error
                }
            ContactEvents.EmployeeDeleted += OnEmployeeDeleted;
            ContactEvents.ClientDeleted += OnClientDeleted;


        }


        #endregion

        /* =============================================
    * TASK: DATA STORAGE AND PROPERTIES
    * =============================================
    * This section defines the core data properties
    * that the View will bind to. It includes:
    * - AllContacts: Complete collection of contacts
    * - FilteredContacts: Subset currently displayed
    * - Search/Filter related properties
    * - Loading state properties
    * All properties use CommunityToolkit's ObservableProperty
    * for automatic INotifyPropertyChanged implementation.
    */

        #region properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredContacts))]
        private ObservableCollection<ContactDisplayModel> _allContacts = new();

        [ObservableProperty]
        private ObservableCollection<ContactDisplayModel> _filteredContacts = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredContacts))]
        private string _currentFilter = "All";

        // **Task: Searching**
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredContacts))]
        private string _searchText = string.Empty;

        #endregion

        /* =============================================
       * TASK: LAZY LOADING AND PAGINATION
       * =============================================
       * Implements paginated data loading to improve performance
       * by only loading small chunks of data at a time.
       * Key features:
       * - PageSize controls how many items load at once
       * - Separate pagination for Clients and Employees
       * - Tracks whether more data is available
       * - Prevents duplicate loading requests
       */
        // **Task: Lazy Loading**

        #region pagination
        private const int PageSize = 10;
            private int _currentClientPage = 0;
            private int _currentEmployeePage = 0;
            private bool _hasMoreClients = true;
            private bool _hasMoreEmployees = true;
            private bool _isInitialLoad = true;

            [ObservableProperty]
            private bool _isLoadingMore;

            [ObservableProperty]
            private bool _canLoadMore = true;

            [RelayCommand]
            public async Task LoadMoreContacts()
            {
                if (IsLoadingMore || !CanLoadMore) return;
                await LoadContactsAsync(loadMore: true);
            }

            private async Task LoadContactsAsync(bool loadMore = false)
            {
                _logger.LogInformation($"LoadContactsAsync started (loadMore: {loadMore})");
                if (!loadMore && !_isInitialLoad)
                {
                    _logger.LogInformation("Skipping load - not initial load and not loadMore");
                    return;
                }

                try
                {
                    IsLoadingMore = true;
                    CanLoadMore = false;
                    _logger.LogInformation("Loading more contacts...");

                // Reset pagination for fresh loads
                if (!loadMore)
                    {
                        _logger.LogInformation("Resetting pagination for fresh load");
                        _currentClientPage = 0;
                        _currentEmployeePage = 0;
                        _hasMoreClients = true;
                        _hasMoreEmployees = true;
                        AllContacts.Clear(); // Clear existing contacts for a fresh load
                    }

                    List<ContactDisplayModel> newContacts = new();

                // **Algorithm: Paginated Loading of Clients**
                // Retrieves a limited number of clients from the database based on the current page and page size.
                // It keeps track of whether there are more clients to load.

                // Load clients if more available
                if (_hasMoreClients)
                    {
                        _logger.LogInformation($"Loading clients page {_currentClientPage}");
                        var clients = await _clientDatabaseService.GetClientsWithoutJobsAsync(_currentClientPage * PageSize, PageSize);
                        newContacts.AddRange(clients.Select(c => new ContactDisplayModel(c)));
                        _hasMoreClients = clients.Count == PageSize;
                        if (_hasMoreClients) _currentClientPage++;
                        _logger.LogInformation($"Retrieved {clients.Count} clients");
                    }

                // **Algorithm: Paginated Loading of Employees**
                // Retrieves a limited number of employees from the database based on the current page and page size.
                // It keeps track of whether there are more employees to load.
                // Load employees if more available
                if (_hasMoreEmployees)
                    {
                        _logger.LogInformation($"Loading employees page {_currentEmployeePage}");
                        var employees = await _employeeDatabaseService.GetEmployeesAsync(_currentEmployeePage * PageSize, PageSize);
                        newContacts.AddRange(employees.Select(e => new ContactDisplayModel(e)));
                        _hasMoreEmployees = employees.Count == PageSize;
                        if (_hasMoreEmployees) _currentEmployeePage++;
                        _logger.LogInformation($"Retrieved {employees.Count} employees");
                    }

                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _logger.LogInformation($"Adding {newContacts.Count} contacts to UI");
                        foreach (var contact in newContacts)
                        {
                            AllContacts.Add(contact); // Add loaded contacts to the main list
                        }
                        UpdateFilterOptions(); // Update filter options based on the new data
                        RefreshFilteredContactsCommand.Execute(null); // Refresh the filtered view
                    });

                    CanLoadMore = _hasMoreClients || _hasMoreEmployees; // Determine if more data can be loaded
                    _isInitialLoad = false;
                    _logger.LogInformation($"Load completed. CanLoadMore: {CanLoadMore}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading contacts");
                    Debug.WriteLine($"Error loading contacts: {ex.Message}");
                }
                finally
                {
                    IsLoadingMore = false;
                    _logger.LogInformation("LoadContactsAsync completed");
                }
            }
        private void OnEmployeeDeleted(Employee employee)
        {
            _logger.LogInformation($"Employee deleted event received: {employee.Name}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var contactToRemove = AllContacts.FirstOrDefault(c => c.Id == employee.Id && c.IsEmployee);
                if (contactToRemove != null)
                {
                    AllContacts.Remove(contactToRemove);
                    UpdateFilterOptions();
                    RefreshFilteredContactsCommand.Execute(null);
                }
            });
        }
        private void OnClientDeleted(Client client)
        {
            _logger.LogInformation($"Client deleted event received: {client.Name}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var contactToRemove = AllContacts.FirstOrDefault(c => c.Id == client.Id && !c.IsEmployee);
                if (contactToRemove != null)
                {
                    AllContacts.Remove(contactToRemove);
                    UpdateFilterOptions();
                    RefreshFilteredContactsCommand.Execute(null);
                }
            });
        }
        #endregion
        /* =============================================
               * TASK: FILTERING AND SEARCHING
               * =============================================
               * Handles the logic for filtering and searching contacts.
               * Key features:
               * - Debouncing to prevent rapid UI updates while typing
               * - Combined filter (All/Clients/Employees) and search
               * - Case-insensitive search across multiple fields
               * - Automatic refresh when criteria change
               */
        #region filtering
        private CancellationTokenSource _filterDebounceToken;
            private bool _isDisposed;

    

        // Trigger debounced refresh when search text changes
        partial void OnSearchTextChanged(string value) => DebounceRefresh();

        // Trigger debounced refresh when filter changes
        partial void OnCurrentFilterChanged(string value) => DebounceRefresh();

        // Debounce implementation to prevent rapid refreshes

        // **Algorithm: Debouncing Filter/Search**
        // Introduces a short delay before refreshing the filtered list. This prevents the UI from
        // updating too frequently as the user types or changes the filter, improving performance.
        private void DebounceRefresh()
            {
                if (_isDisposed) return;
                try
                {
                    _filterDebounceToken?.Cancel(); // Cancel any pending refresh
                    _filterDebounceToken = new CancellationTokenSource();
                    Task.Delay(300, _filterDebounceToken.Token).ContinueWith(t =>
                    {
                        if (!t.IsCanceled && !_isDisposed)
                        {
                            MainThread.BeginInvokeOnMainThread(() => RefreshFilteredContactsCommand.Execute(null));
                        }
                    }, TaskScheduler.Default);
                }
                catch (ObjectDisposedException) { }
            }

            [RelayCommand]
            private void RefreshFilteredContacts()
            {
                if (_isDisposed) return;
                try
                {
                    // **Algorithm: Filtering and Sorting Contacts**
                    // 1. Filters the _allContacts collection based on the _currentFilter.
                    // 2. If _searchText is not empty, it further filters the results to include contacts
                    //    whose Name, Phone, or Address contain the search text (case-insensitive).
                    // 3. Orders the final filtered list by Name.
                    var newItems = GetFilteredContacts()
                   .OrderBy(c => c.Name)
                   .ToList();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FilteredContacts.Clear(); // Clear the current filtered list
                        foreach (var item in newItems)
                            FilteredContacts.Add(item); // Add the newly filtered and sorted items
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Filter refresh failed");
                }
            }

            // **Algorithm: Applying Filter**
            // Returns an IEnumerable<ContactDisplayModel> based on the _currentFilter.
            private IEnumerable<ContactDisplayModel> GetFilteredContacts()
            {
                var query = CurrentFilter switch
                {
                    "Clients" => AllContacts.Where(c => !c.IsEmployee),
                    "Employees" => AllContacts.Where(c => c.IsEmployee),
                    _ => AllContacts // "All" or any other value returns all contacts
                };

                // **Algorithm: Applying Search**
                // If _searchText is not null or whitespace, it filters the query to include contacts
                // that match the search criteria.
                return string.IsNullOrWhiteSpace(SearchText)
                    ? query
                    : query.Where(MatchesSearch).ToList();
            }

            // **Algorithm: Matching Search Text**
            // Checks if a ContactDisplayModel's Name, Phone, or Address contains the _searchText
            // (case-insensitive).
            private bool MatchesSearch(ContactDisplayModel contact) =>
                contact.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                contact.Phone.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                contact.Address.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        #endregion

        /* =============================================
      * TASK: FILTER OPTIONS MANAGEMENT
      * =============================================
      * Manages the filter options displayed in the UI picker.
      * Dynamically updates counts when contacts change.
      * Maintains synchronization between picker selection
      * and actual filter being applied.
      */
        #region filter options

        // **Task: Filter Options Management**
        private ObservableCollection<FilterOption> _filterOptions = new();
            public ObservableCollection<FilterOption> FilterOptions
            {
                get => _filterOptions;
                private set => SetProperty(ref _filterOptions, value);
            }

            [ObservableProperty]
            private int _selectedFilterIndex = 0;

            partial void OnSelectedFilterIndexChanged(int value)
            {
                _logger.LogInformation("startingFilterIndex");
                if (value >= 0 && value < FilterOptions.Count)
                {
                    SelectedFilterOption = FilterOptions[value];
                }
                _logger.LogInformation("FilterIndex completed");
            }

            [ObservableProperty]
            [NotifyPropertyChangedFor(nameof(CurrentFilter))] // Ensure CurrentFilter is updated
            private FilterOption? _selectedFilterOption;

            partial void OnSelectedFilterOptionChanged(FilterOption? value)
            {
                if (value != null)
                {
                    CurrentFilter = value.FilterType; // Update CurrentFilter when selection changes
                }
            }

            // **Algorithm: Updating Filter Options**
            // Creates a new list of FilterOption objects based on the current contents of _allContacts.
            // It counts the number of "All", "Clients", and "Employees" and updates the display text
            // of the filter options accordingly.
            private void UpdateFilterOptions()
            {
                var options = new List<FilterOption>
            {
                new() {
                    DisplayText = $"All ({AllContacts.Count})",
                    FilterType = "All"
                },
                new() {
                    DisplayText = $"Clients ({AllContacts.Count(c => !c.IsEmployee)})",
                    FilterType = "Clients"
                },
                new() {
                    DisplayText = $"Employees ({AllContacts.Count(c => c.IsEmployee)})",
                    FilterType = "Employees"
                }
            };

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FilterOptions.Clear();
                    foreach (var option in options)
                        FilterOptions.Add(option);

                    // Ensure the SelectedFilterIndex corresponds to the CurrentFilter
                    var selectedOptionIndex = FilterOptions.IndexOf(FilterOptions.FirstOrDefault(fo => fo.FilterType == CurrentFilter));
                    if (selectedOptionIndex >= 0)
                    {
                        SelectedFilterIndex = selectedOptionIndex;
                    }
                });
            }

   
        #endregion
        /* =============================================
      * TASK: MESSAGING FOR NEW CONTACTS
      * =============================================
      * Handles communication with other parts of the app
      * when new contacts are added. Uses WeakReferenceMessenger
      * to prevent memory leaks.
      */
        #region messaging
        private void InitializeMessaging()
            {

            // **Algorithm: Handling EmployeeAddedMessage**
            // When an EmployeeAddedMessage is received, it creates a new ContactDisplayModel
            // from the employee and adds it to the _allContacts collection. It then updates
            // the filter options and refreshes the filtered list.
            WeakReferenceMessenger.Default.Register<EmployeeAddedMessage>(this, (r, m) =>
                {
                    _logger.LogInformation($"Adding new employee: {m.Value.Name}");
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AllContacts.Add(new ContactDisplayModel(m.Value));
                        UpdateFilterOptions();
                        RefreshFilteredContactsCommand.Execute(null); // Refresh filtered list
                    });
                });

                // **Algorithm: Handling ClientAddedMessage**
                // When a ClientAddedMessage is received, it creates a new ContactDisplayModel
                // from the client and adds it to the _allContacts collection. It then updates
                // the filter options and refreshes the filtered list.
                WeakReferenceMessenger.Default.Register<ClientAddedMessage>(this, (r, m) =>
                {
                    _logger.LogInformation($"Adding new Client: {m.Value.Name}");
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AllContacts.Add(new ContactDisplayModel(m.Value));
                        UpdateFilterOptions();
                        RefreshFilteredContactsCommand.Execute(null); // Refresh filtered list
                    });
                });
            }
        #endregion

        /*==============================================
         * Task: CLICKABLE CONTACTS
         *==============================================*/


        // Command executed when the pointer moves over the designated area


        #region Navigate to editPage

        [ObservableProperty]
        private ContactDisplayModel _selectedContact;

        [RelayCommand]
        private async Task ContactTapped(ContactDisplayModel contact)
        {
            if (contact == null) return;

            try
            {
                string route = contact.IsEmployee ? "EditEmployee" : "EditClient";
                var parameters = new Dictionary<string, object>
        {
            { "ContactId", contact.Id }
        };

                await Shell.Current.GoToAsync(route, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation failed");
                await Shell.Current.DisplayAlert("Error", "Failed to navigate to edit page", "OK");
            }
        }

        #endregion
        /* =============================================
    * TASK: DISPOSAL AND CLEANUP
    * =============================================
    * Properly cleans up resources when ViewModel is disposed.
    * Cancels any pending operations and unregisters message handlers.
    */
        #region cleanUp
        protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _filterDebounceToken?.Cancel();
                    _filterDebounceToken?.Dispose();
                    WeakReferenceMessenger.Default.UnregisterAll(this);
                }
            }
        public void Dispose()
        {
            // Clean up all message subscriptions
            WeakReferenceMessenger.Default.UnregisterAll(this);

            // Clean up event handlers
            ContactEvents.EmployeeDeleted -= OnEmployeeDeleted;
            ContactEvents.ClientDeleted -= OnClientDeleted;

            // Clean up other resources
            _filterDebounceToken?.Cancel();
            _filterDebounceToken?.Dispose();

            GC.SuppressFinalize(this);
        }

        ~ContactsViewModel()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: false);
            }
        }
    #endregion
    /* =============================================
  * SUPPORTING CLASSES
  * =============================================
  * Message types and model classes used by the ViewModel.
  */
    #region supporting classes
    public sealed record EmployeeAddedMessage(Employee Value);
        public sealed record ClientAddedMessage(Client Value);

        public class ContactDisplayModel
        {
            public int Id { get; }
            public string Name { get; }
            public string Phone { get; }
            public string Address { get; }
            public bool IsEmployee { get; }
        public bool IsHovered { get; set; }
        public string DisplayAddress => string.IsNullOrWhiteSpace(Address) ? "No address" : Address;

            public ContactDisplayModel(Client client) : this(client.Id, client.Name, client.Phone, client.Address, false) { }

            public ContactDisplayModel(Employee employee) : this(employee.Id, employee.Name, employee.Phone, employee.Address, true) { }

            private ContactDisplayModel(int id, string name, string phone, string address, bool isEmployee)
            {
                Id = id;
                Name = name;
                Phone = phone;
                Address = address;
                IsEmployee = isEmployee;
            }
        }

        public class FilterOption
        {
            public string DisplayText { get; set; } = string.Empty;
            public string FilterType { get; set; } = string.Empty;
        }
    }
#endregion