using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel.Communication;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using ResearchApp.Utils;
using ResearchApp.Views;
using SQLite;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;
using static ResearchApp.ViewModels.ContactsViewModel;


namespace ResearchApp.ViewModels
{
    public partial class AddEmployeeViewModel : ObservableObject
    {
        private readonly IEmployeeDatabaseService _databaseService;

        public AddEmployeeViewModel(IEmployeeDatabaseService databaseService)
        {
            _databaseService = databaseService;
            //await ResetDatabaseNow();

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

   
           public bool IsValid => ValidateEmployee().IsValid; // Client validation

        private (bool IsValid, string ErrorMessage) ValidateEmployee()
        {
            if (string.IsNullOrWhiteSpace(EmployeeName))
                return (false, "Name is required.");

            if (EmployeeName.Length < 3 || EmployeeName.Length > 100)
                return (false, "Name must be 3-100 characters.");

            if (string.IsNullOrWhiteSpace(EmployeePhone))
                return (false, "Phone number is required.");

            if (!ValidationHelper.ValidateNumber(EmployeePhone, isPhoneNumber: true))
                return (false, "Invalid phone number format.");

            if (string.IsNullOrWhiteSpace(EmployeeAddress))
                return (false, "Address is required.");

            if (EmployeeAddress.Length < 5 || EmployeeAddress.Length > 200)
                return (false, "Address must be 5-200 characters.");

            return (true, null);
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
                var validation = await Task.Run(() => ValidateEmployee());
                // Ask the user for confirmation before saving
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!validation.IsValid)
                    {
                        await App.Current.MainPage.DisplayAlert("Error", validation.ErrorMessage, "OK");
                        return;
                    }

                    bool confirmSave = await App.Current.MainPage.DisplayAlert(
                        "Confirm Save",
                        "Are you sure you want to save this employee?",
                        "Yes", "No");

                    if (!confirmSave) return;
                });

                // Create a new Employee object
                var employee = new Employee
                {
                    Name = EmployeeName.CapitalizeFirstLetter(),
                    Address = EmployeeAddress.CapitalizeFirstLetter(),
                    Phone = EmployeePhone
                };

                // Save the employee to the database
                var newEmployee = await _databaseService.SaveEmployeeAsync(employee);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    // Clear form
                    EmployeeName = string.Empty;
                    EmployeeAddress = string.Empty;
                    EmployeePhone = string.Empty;

                    // Show success
                    await App.Current.MainPage.DisplayAlert(
                        "Success",
                        $"Employee saved successfully! ID: {newEmployee.Id}",
                        "OK");
                    var addedEmployee = await _databaseService.GetEmployeeByIdAsync(newEmployee.Id);
                    if (employee == null)
                    {
                        await App.Current.MainPage.DisplayAlert("Error", "Failed to retrieve saved employee.", "OK");
                        return;
                    }
                    //  send message
                    WeakReferenceMessenger.Default.Send(new EmployeeAddedMessage(addedEmployee));



                    // Safely navigate back
                    await Shell.Current.GoToAsync(".."); // Just go back one level

                   
                });
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Error",
                        "An employee with this phone number already exists.",
                        "OK");
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Error",
                        $"Failed to save employee: {ex.Message}",
                        "OK");
                });

                // Log the full error for debugging
                Debug.WriteLine($"SaveEmployee Error: {ex}");
            }
        }

      
    }
}