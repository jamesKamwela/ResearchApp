using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using ResearchApp.Views;
using ResearchApp.Utils;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Specialized;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net.NetworkInformation;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
namespace ResearchApp.ViewModels
{
    public partial class ContactsViewModel : ObservableObject
    {
        private readonly IClientDatabaseService _clientDatabaseService;
        private readonly IEmployeeDatabaseService _employeeDatabaseService;
        private readonly ILogger<ContactsViewModel> _logger;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredContacts))]
        private ObservableCollection<ContactDisplayModel> _allContacts = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredContacts))]
        private string _currentFilter = "All";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredContacts))]
        private string _searchText = string.Empty;
        private ObservableCollection<FilterOption> _filterOptions = new ObservableCollection<FilterOption>();
        public ObservableCollection<FilterOption> FilterOptions
        {
            get => _filterOptions;
            private set => SetProperty(ref _filterOptions, value);
        }

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
             
                InitializeMessaging();
                _=LoadContactsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ViewModel initialization failed: {ex}");
                throw; // Re-throw to see the error
            }
        }

        // New fields for lazy loading
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

                if (!loadMore)
                {
                    _logger.LogInformation("Resetting pagination for fresh load");
                    _currentClientPage = 0;
                    _currentEmployeePage = 0;
                    _hasMoreClients = true;
                    _hasMoreEmployees = true;
                    AllContacts.Clear();
                }

                List<ContactDisplayModel> newContacts = new();

                if (_hasMoreClients)
                {
                    _logger.LogInformation($"Loading clients page {_currentClientPage}");
                    var clients = await _clientDatabaseService.GetClientsWithoutJobsAsync(_currentClientPage * PageSize, PageSize);
                    newContacts.AddRange(clients.Select(c => new ContactDisplayModel(c)));
                    _hasMoreClients = clients.Count == PageSize;
                    if (_hasMoreClients) _currentClientPage++;
                    _logger.LogInformation($"Retrieved {clients.Count} clients");
                }

                if (_hasMoreEmployees)
                {
                    _logger.LogInformation($"Loading employees page {_currentEmployeePage}");
                    var employees = await _employeeDatabaseService.GetEmployeesAsync(_currentEmployeePage * PageSize, PageSize);
                    newContacts.AddRange(employees.Select(e => new ContactDisplayModel(e)));
                    _hasMoreEmployees = employees.Count == PageSize;
                    if (_hasMoreEmployees) _currentEmployeePage++;
                    _logger.LogInformation($"Retrieved {employees.Count} employees");

                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _logger.LogInformation($"Adding {newContacts.Count} contacts to UI");
                    foreach (var contact in newContacts)
                    {
                        AllContacts.Add(contact);
                    }
                    UpdateFilterOptions();
                });

                CanLoadMore = _hasMoreClients || _hasMoreEmployees;
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
        public ObservableCollection<ContactDisplayModel> FilteredContacts =>
          new(GetFilteredContacts().OrderBy(c => c.Name));

        [RelayCommand]
        private void FilterChanged() => OnPropertyChanged(nameof(FilteredContacts));

        private void InitializeMessaging()
        {
            WeakReferenceMessenger.Default.Register<EmployeeAddedMessage>(this, (r, m) =>
            {
                _logger.LogInformation($"Adding new employee: {m.Value.Name}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _logger.LogInformation($"Adding new employee: {m.Value.Name}");
                    AllContacts.Add(new ContactDisplayModel(m.Value));
                    UpdateFilterOptions();
                });
            });

            WeakReferenceMessenger.Default.Register<ClientAddedMessage>(this, (r, m) =>
            {
                 _logger.LogInformation($"Adding new Client: {m.Value.Name}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _logger.LogInformation($"Adding new Client: {m.Value.Name}");
                    AllContacts.Add(new ContactDisplayModel(m.Value));
                    UpdateFilterOptions();
                });
            });
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredContacts))]
        private FilterOption? _selectedFilterOption;

        partial void OnSelectedFilterOptionChanged(FilterOption? value)
        {
            if (value != null)
            {
                CurrentFilter = value.FilterType; // Update CurrentFilter when selection changes
                OnPropertyChanged(nameof(FilteredContacts)); // Notify UI to update
            }
        }
      

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
            });
        }



        private IEnumerable<ContactDisplayModel> GetFilteredContacts()
        {
            var query = CurrentFilter switch
            {
                "Clients" => AllContacts.Where(c => !c.IsEmployee),
                "Employees" => AllContacts.Where(c => c.IsEmployee),
                _ => AllContacts
            };

            return string.IsNullOrWhiteSpace(SearchText)
                ? query
                : query.Where(MatchesSearch).ToList();
        }

        private bool MatchesSearch(ContactDisplayModel contact) =>
             contact.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             contact.Phone.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             contact.Address.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                WeakReferenceMessenger.Default.UnregisterAll(this);
                GC.SuppressFinalize(this);
            }
        }
    }

    // Message types for WeakReferenceMessenger
    public sealed record EmployeeAddedMessage(Employee Value);
    public sealed record ClientAddedMessage(Client Value);

    public class ContactDisplayModel
    {
        public int Id { get; }
        public string Name { get; }
        public string Phone { get; }
        public string Address { get; }
        public bool IsEmployee { get; }

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