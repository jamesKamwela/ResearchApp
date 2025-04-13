using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchApp.Models;
using ResearchApp.DataStorage;
using System.Threading.Tasks;
using ResearchApp.Utils;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel.Communication;
using CommunityToolkit.Mvvm.Messaging;
using static ResearchApp.ViewModels.ContactsViewModel;


namespace ResearchApp.ViewModels
{
    public partial class AddEmployeeViewModel : ObservableObject
    {
        private readonly IEmployeeDatabaseService _databaseService;

        public AddEmployeeViewModel(IEmployeeDatabaseService databaseService)
        {
            _databaseService = databaseService;
            //ResetDatabaseNow();

        }


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))] // Notify when IsValid changes
        private string employeeName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))] // Notify when IsValid changes
        private string employeeAddress = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))] // Notify when IsValid changes
        private string employeePhone = string.Empty;

        // Computed property to check if all fields are valid

   
           public bool IsValid => ValidateEmployee(); // Client validation

        private bool ValidateEmployee()
        {
            // Validate client name
            bool isClientNameValid = ValidationHelper.ValidateText(EmployeeName, minLength: 3, maxLength: 100);

            // Validate client phone
            bool isClientPhoneValid = ValidationHelper.ValidateNumber(EmployeePhone, isPhoneNumber: true);

            // Validate client address
            bool isClientAddressValid = ValidationHelper.ValidateText(EmployeeAddress, minLength: 5, maxLength: 200);

            return isClientNameValid && isClientPhoneValid && isClientAddressValid;
        }


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

        // Cancel button command
        [RelayCommand]
        private async Task Cancel()
        {
            // Ask the user for confirmation before canceling
            bool confirmCancel = await App.Current.MainPage.DisplayAlert(
                "Confirm Cancel",
                "Are you sure you want to cancel? Any unsaved changes will be lost.",
                "Yes", "No");

            if (confirmCancel)
            {
                try
                {
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Navigation failed: {ex.Message}");
                    await App.Current.MainPage.DisplayAlert("Error", "Failed to navigate back.", "OK");
                }
            }
        }

        // Save button command
        [RelayCommand]
        private async Task SaveEmployee()
        {
            try
            {
                // Ask the user for confirmation before saving
                bool confirmSave = await App.Current.MainPage.DisplayAlert(
                    "Confirm Save",
                    "Are you sure you want to save this employee?",
                    "Yes", "No");

                if (!confirmSave)
                {
                    return;
                }

                // Create a new Employee object
                var employee = new Employee
                {
                    Name = EmployeeName.CapitalizeFirstLetter(),
                    Address = EmployeeAddress.CapitalizeFirstLetter(),
                    Phone = EmployeePhone
                };

                // Save the employee to the database
    
                //await _databaseService.SaveEmployeeAsync(employee);
                var newEmployee= await _databaseService.SaveEmployeeAsync(employee);

                // Show success message
                await App.Current.MainPage.DisplayAlert("Success", $"Employee saved successfully! Id: {employee.Id}", "OK");

                // Clear the form
                EmployeeName = string.Empty;
                EmployeeAddress = string.Empty;
                EmployeePhone = string.Empty;

                WeakReferenceMessenger.Default.Send(new EmployeeAddedMessage(newEmployee));
                // Navigate back after successful save
                //MessagingCenter.Send<object>(this, "ContactAdded");
                await Shell.Current.GoToAsync("..");
                //await Shell.Current.GoToAsync("..", animate: false);

            }
            catch (Exception ex)
            {
                await App.Current.MainPage.DisplayAlert("Error", $"Failed to save employee: {ex.Message}", "OK");
            }
        }
       
            
    }
}