using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchApp.DataStorage; 
using ResearchApp.Models;   
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations; // For potential future validation attributes
using System.Diagnostics;
using System.Text.RegularExpressions; // For Regex validation
using System.Threading.Tasks;
using static Microsoft.Maui.ApplicationModel.Permissions;
using System.Net;
using System.Xml.Linq;

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

        [RelayCommand(CanExecute = nameof(IsValid))] // Use IsValid to control CanExecute
        private async Task SaveContact()
        {
            _logger.LogInformation("SaveContactCommand executed.");
            if (!IsValid)
            {
                _logger.LogWarning("Save attempted while form is invalid.");
                await Shell.Current.DisplayAlert("Validation Error", "Please correct the highlighted fields.", "OK");
                return;
            }

            try
            {
                // Create or update the Employee object
                Employee employeeToSave = new Employee
                {
                    Id = _currentEmployeeId, // Make sure to use the ID loaded initially
                    Name = this.Name,
                    Phone = this.Phone,
                    Address = this.Address
                    // Add other properties if needed
                };

                _logger.LogInformation("Attempting to update employee ID: {EmployeeId}", employeeToSave.Id);
                // Assuming UpdateEmployeeAsync exists in your service
                bool success = await _employeeDatabaseService.UpdateEmployeeAsync(employeeToSave);

                if (success)
                {
                    _logger.LogInformation("Employee updated successfully.");
                    // Send message if other parts of the app need to know
                    // WeakReferenceMessenger.Default.Send(new EmployeeUpdatedMessage(employeeToSave));
                    await Shell.Current.GoToAsync(".."); // Navigate back on success
                }
                else
                {
                    _logger.LogError("Failed to update employee in database.");
                    await Shell.Current.DisplayAlert("Error", "Failed to save employee data.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving employee data.");
                await Shell.Current.DisplayAlert("Error", $"An error occurred while saving: {ex.Message}", "OK");
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


        // Optional: Placeholder for Delete if needed
        // [RelayCommand]
        // private async Task DeleteContact()
        // {
        //     bool confirmed = await Shell.Current.DisplayAlert("Confirm Delete", $"Are you sure you want to delete {Name}?", "Yes", "No");
        //     if (confirmed)
        //     {
        //         // Call service to delete _currentEmployeeId
        //         // Navigate back
        //     }
        // }
    }
}