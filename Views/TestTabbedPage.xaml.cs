using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace ResearchApp.Views;

public partial class TestTabbedPage : ContentPage, ITabNavigationHelper, INotifyPropertyChanged
{
    private readonly IWorkRecordDatabaseService _workRecordService;
    private readonly IClientDatabaseService _clientService;
    private readonly IEmployeeDatabaseService _employeeService;
    private readonly ILogger<TestTabbedPage> _logger;
    private readonly ILogger<WorkEntryViewModel> _workEntryLogger;
    private readonly ILogger<PendingJobsViewModel> _pendingJobsLogger;
    private readonly ILogger<PendingJobs> _pendingCsJobsLogger;

    private readonly ILogger<ActiveEmployees> _activeEmployeesLogger;
    private readonly ILogger<ActiveEmployeesViewModel> _activeEmployeesViewModelLogger;
    private readonly ILogger<ClientWorkRecordsDataViewModel> _clientWorkRecordsDataViewModelLogger;
    private readonly ILogger<ClientWorkRecordsData> _clientWorkRecordsDataLogger;

    private TabItem _selectedTab;
    private ObservableCollection<TabItem> _tabItems;

    public ObservableCollection<TabItem> TabItems
    {
        get => _tabItems;
        set
        {
            _tabItems = value;
            OnPropertyChanged(nameof(TabItems));
        }
    }
  
    public async Task NavigateToAsync(Page page)
    {
        try
        {
            // Hide tabs
            var tabsLayout = this.FindByName<StackLayout>("TabsLayout");
            if (tabsLayout != null)
            {
                tabsLayout.IsVisible = false;
            }

            // Navigate
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed");

            // Make sure tabs are visible even if navigation fails
            var tabsLayout = this.FindByName<StackLayout>("TabsLayout");
            if (tabsLayout != null)
            {
                tabsLayout.IsVisible = true;
            }

            throw;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Ensure tabs are visible when returning
        var tabsLayout = this.FindByName<StackLayout>("TabsLayout");
        if (tabsLayout != null)
        {
            tabsLayout.IsVisible = true;
        }
    }



    public void SwitchToTab(int index) => SelectedTab = TabItems[index];

    public TabItem SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value) return;

            if (_selectedTab != null)
                _selectedTab.IsSelected = false;

            _selectedTab = value;

            if (_selectedTab != null)
            {
                _selectedTab.IsSelected = true;
                // Force lazy loading when tab is selected
                var content = _selectedTab.Content;
                _logger.LogInformation($"Selected tab: {_selectedTab.Title}, Content loaded: {content != null}");
            }

            OnPropertyChanged(nameof(SelectedTab));
        }
    }

    public TestTabbedPage(
        IWorkRecordDatabaseService workRecordService,
        IClientDatabaseService clientService,
        IEmployeeDatabaseService employeeService,
        ILogger<TestTabbedPage> logger,
        ILoggerFactory loggerFactory)
    {
        _workRecordService = workRecordService;
        _clientService = clientService;
        _employeeService = employeeService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workEntryLogger = loggerFactory.CreateLogger<WorkEntryViewModel>();
        _pendingJobsLogger = loggerFactory.CreateLogger<PendingJobsViewModel>();
        _activeEmployeesViewModelLogger = loggerFactory.CreateLogger<ActiveEmployeesViewModel>();
        _activeEmployeesLogger = loggerFactory.CreateLogger<ActiveEmployees>();

        _clientWorkRecordsDataViewModelLogger = loggerFactory.CreateLogger<ClientWorkRecordsDataViewModel>();
        _clientWorkRecordsDataLogger = loggerFactory.CreateLogger<ClientWorkRecordsData>();

        InitializeComponent();
        BindingContext = this;
        TabItems = new ObservableCollection<TabItem>();
        InitializeTabs();
    }

    private void InitializeTabs()
    {
        try
        {
            _logger.LogInformation("Initializing tabs with lazy loading...");

            TabItems.Clear();

            // Create tabs with lazy-loaded content using factory functions
            TabItems.Add(new TabItem("Work Entry", () =>
            {
                _logger.LogInformation("Creating Work Entry view...");
                return new Isvar(new WorkEntryViewModel(
                    _workRecordService,
                    _workEntryLogger,
                    this,
                    _clientService,
                    _employeeService));
            }));

            TabItems.Add(new TabItem("Pending Jobs", () =>
            {
                _logger.LogInformation("Creating Pending Jobs view...");
                return new PendingJobs(new PendingJobsViewModel(
                    this,
                    _workRecordService,
                    _clientService,
                    _employeeService,
                    _pendingJobsLogger), _pendingCsJobsLogger);
            }));
            TabItems.Add(new TabItem("Active Employees", () =>
            {
                _logger.LogInformation("Creating Active Employees view...");
                return new ActiveEmployees(new ActiveEmployeesViewModel(
                    _employeeService,
                    this,
                    _workRecordService,
                    _activeEmployeesViewModelLogger),
                    _activeEmployeesLogger); // Pass the required logger argument
            }));

            TabItems.Add(new TabItem("Client Data", () =>
            {
                _logger.LogInformation("Creating Client Data view...");
                return new ClientWorkRecordsData(_clientWorkRecordsDataLogger,new ClientWorkRecordsDataViewModel(
                    this,
                    _clientService,
                    _workRecordService,
                    _clientWorkRecordsDataViewModelLogger
                    ));
            }));



            // Set the first tab as selected (this will trigger lazy loading for the first tab)
            SelectedTab = TabItems.FirstOrDefault();

            _logger.LogInformation($"Initialized {TabItems.Count} tabs with lazy loading");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tabs");
            throw;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class TabItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private View _content;
    private readonly Func<View> _contentFactory;
    private bool _isContentLoaded = false;

    public string Title { get; }

    public View Content
    {
        get
        {
            // Lazy loading: create content only when first accessed
            if (_content == null && _contentFactory != null && !_isContentLoaded)
            {
                try
                {
                    _isContentLoaded = true; // Set flag to prevent multiple creation attempts
                    _content = _contentFactory.Invoke();
                    Debug.WriteLine($"Lazy loaded content for tab: {Title}");
                    OnPropertyChanged(nameof(Content));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating content for tab '{Title}': {ex.Message}");
                    _isContentLoaded = false; // Reset flag to allow retry
                    throw;
                }
            }
            return _content;
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                Debug.WriteLine($"Tab {Title} IsSelected changed to {value}");
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    // Constructor for lazy loading
    public TabItem(string title, Func<View> contentFactory)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        _contentFactory = contentFactory ?? throw new ArgumentNullException(nameof(contentFactory));
    }

    

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

  
}