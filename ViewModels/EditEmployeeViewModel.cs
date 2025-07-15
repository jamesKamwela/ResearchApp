using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage; 
using ResearchApp.Models;   
using System;
using ResearchApp.StaticClasses;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations; // For potential future validation attributes
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions; // For Regex validation
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace ResearchApp.ViewModels
{
    public partial class EditEmployeeViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IEmployeeDatabaseService _employeeDatabaseService;
        private readonly ILogger<EditEmployeeViewModel> _logger;
        private int _currentEmployeeId; // To store the ID of the employee being edited

        // --- Observable Properties for Binding ---

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))] // Re-check validity when Name changes
        private string _name;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))] // Re-check validity when Phone changes
        private string _phone;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))] // Re-check validity when Address changes
        private string _address;
        public sealed record EmployeeDeletedMessage(Employee Value);

        // Expose IsValid directly (calculated property)
        public bool IsValid => Validate();

        // Constructor for Dependency Injection
        public EditEmployeeViewModel(IEmployeeDatabaseService employeeDatabaseService, ILogger<EditEmployeeViewModel> logger)
        {
            _employeeDatabaseService = employeeDatabaseService;
            _logger = logger;
            _logger.LogInformation("EditEmployeeViewModel created.");
        }

        // --- IQueryAttributable Implementation ---
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ContactId", out object contactIdObj) && int.TryParse(contactIdObj?.ToString(), out int contactId))
            {
                _currentEmployeeId = contactId;
                _logger.LogInformation("Received Employee ID: {EmployeeId}", _currentEmployeeId);
                await LoadEmployeeAsync(_currentEmployeeId);
            }
            else
            {
                _logger.LogError("Could not get valid EmployeeId from query parameters.");
                // Handle error - maybe navigate back or show an error message
                await Shell.Current.DisplayAlert("Error", "Could not load employee details.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        private async Task LoadEmployeeAsync(int employeeId)
        {
            try
            {
                _logger.LogInformation("Loading employee with ID: {EmployeeId}", employeeId);
                // Assuming GetEmployeeByIdAsync exists in your service
                Employee employee = await _employeeDatabaseService.GetEmployeeByIdAsync(employeeId);

                if (employee != null)
                {
                    Name = employee.Name;
                    Phone = employee.Phone;
                    Address = employee.Address;
                    // Important: Trigger initial validation check after loading
                    OnPropertyChanged(nameof(IsValid));
                    _logger.LogInformation("Employee loaded successfully: {EmployeeName}", Name);
                }
                else
                {
                    _logger.LogWarning("Employee with ID {EmployeeId} not found.", employeeId);
                    await Shell.Current.DisplayAlert("Error", "Employee not found.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load employee with ID {EmployeeId}", employeeId);
                await Shell.Current.DisplayAlert("Error", "Failed to load employee data.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        // --- Validation Logic ---
        // This method mirrors the validation rules defined in the XAML behaviors
        private bool Validate()
        {
            // Name Validation (MinLength=3, MaxLength=100)
            bool isNameValid = !string.IsNullOrWhiteSpace(Name) && Name.Length >= 3 && Name.Length <= 100;

            // Phone Validation (Regex for 10-15 digits)
            bool isPhoneValid = !string.IsNullOrWhiteSpace(Phone) && Regex.IsMatch(Phone, @"^\d{10,15}$");

            // Address Validation (MinLength=5, MaxLength=100)
            bool isAddressValid = !string.IsNullOrWhiteSpace(Address) && Address.Length >= 5 && Address.Length <= 100;

            bool overallValidity = isNameValid && isPhoneValid && isAddressValid;
            // _logger.LogTrace("Validation Result: Name={IsNameValid}, Phone={IsPhoneValid}, Address={IsAddressValid}, Overall={Overall}",
            //                 isNameValid, isPhoneValid, isAddressValid, overallValidity);

            return overallValidity;
        }


        // --- Commands ---

        [RelayCommand(CanExecute = nameof(IsValid))]
        private async Task SaveEmployee()
        {
            try
            {
                var employee = new Employee
                {
                    Id = _currentEmployeeId,
                    Name = Name,
                    Phone = Phone,
                    Address = Address
                };

                bool success = await _employeeDatabaseService.UpdateEmployeeAsync(employee);
                if (success)
                {
                    await Shell.Current.DisplayAlert("Success", "Employee updated successfully", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to update employee", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving employee");
                await Shell.Current.DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
            }
        }

        // **Note:** Your XAML binds the "Cancel" button to `DeleteContactCommand`.
        // This command implements a "Cancel" action (navigate back).
        // If you intend to *delete*, you should rename the command here and in XAML,
        // and implement the deletion logic.
        [RelayCommand]
        private async Task Cancel() // Renamed from DeleteContactCommand for clarity
        {
            _logger.LogInformation("CancelCommand executed.");
            await Shell.Current.GoToAsync(".."); // Navigate back without saving
        }
        [RelayCommand]
        private async Task DeleteEmployee()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete {Name}?",
                "Yes", "No");

            if (confirm)
            {
                try
                {
                    // Make sure we're using the correct employee ID
                    var employee = await _employeeDatabaseService.GetEmployeeByIdAsync(_currentEmployeeId);

                    if (employee != null)
                    {
                        _logger.LogInformation($"About to delete employee: ID:{employee.Id}, Name:{employee.Name}");

                        // Delete from database first
                        await _employeeDatabaseService.DeleteEmployeeAsync(employee);

                        // Then send the message with the employee that was just deleted
                        _logger.LogInformation($"Sending EmployeeDeletedMessage for: ID:{employee.Id}, Name:{employee.Name}");
                        ContactEvents.OnEmployeeDeleted(employee);

                        await Shell.Current.DisplayAlert("Success", "Employee deleted", "OK");
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        _logger.LogError($"Employee with ID {_currentEmployeeId} not found");
                        await Shell.Current.DisplayAlert("Error", "Employee not found", "OK");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting employee");
                    await Shell.Current.DisplayAlert("Error", "Failed to delete employee", "OK");
                }
            }
        }


    }
}