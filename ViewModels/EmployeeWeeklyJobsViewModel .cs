using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.Models;
using ResearchApp.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

public partial class EmployeeWeeklyJobsViewModel : ObservableObject
{

    private readonly IWorkRecordDatabaseService _workRecordDatabase;
    private readonly IEmployeeDatabaseService _employeeDatabase;
    private readonly ILogger<EmployeeWeeklyJobsViewModel> _logger;

    [ObservableProperty]
    private Employee _employee;

    [ObservableProperty]
    private string _weekRange = "This Week";

    [ObservableProperty]
    private ObservableCollection<WorkRecord> _completedJobs = new();

    public EmployeeWeeklyJobsViewModel(
        IWorkRecordDatabaseService workRecordDatabase,
        IEmployeeDatabaseService employeeDatabase,
        ILogger<EmployeeWeeklyJobsViewModel> logger)
    {
        _workRecordDatabase = workRecordDatabase;
        _employeeDatabase = employeeDatabase;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadEmployeeData(int employeeId)
    {
        try
        {
            // Load employee details first
            Employee = await _employeeDatabase.GetEmployeeByIdAsync(employeeId);

            if (Employee == null)
            {
                _logger.LogWarning($"Employee with ID {employeeId} not found");
                return;
            }

            await LoadData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading employee data");
        }
    }
    [RelayCommand]
    public async Task LoadData()
    {
        try
        {
            if (Employee == null || Employee.Id <= 0)
            {
                _logger.LogWarning("No valid employee selected");
                return;
            }

            // Get current week's start and end dates
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(6);

            WeekRange = $"{startOfWeek:MMM dd} - {endOfWeek:MMM dd, yyyy}";

            // Fetch work records for this employee within the week
            var records = await _workRecordDatabase.GetWorkRecordsForEmployeeAsync(Employee.Id, startOfWeek, endOfWeek);

            // Filter for completed jobs
            var completed = records.Where(r => r.IsJobCompleted).ToList();

            _logger.LogInformation($"Loaded {completed.Count} completed jobs for employee {Employee.Name} from {startOfWeek:MMM dd} to {endOfWeek:MMM dd, yyyy}");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CompletedJobs.Clear();
                foreach (var record in completed)
                {
                    CompletedJobs.Add(record);
                    _logger.LogInformation($"Job ID: {record.JobId}, Client: {record.ClientName}, Date: {record.WorkDate:MMM dd, yyyy}, Quantity: {record.QuantityCompleted}");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading employee jobs");
            Debug.WriteLine($"Error loading jobs: {ex.Message}");
        }
    }
}