using Microsoft.Maui.Controls;
using ResearchApp.ViewModels;

namespace ResearchApp.Views
{
    public partial class AddEmployee : ContentPage
    {
        public AddEmployee(AddEmployeeViewModel viewModel)
        {
            InitializeComponent();

            BindingContext = viewModel; // Set the BindingContext to the ViewModel
          
        }
    }
}