using Microsoft.Maui.Controls;
using ResearchApp.ViewModels;

namespace ResearchApp.Views
{
    public partial class AddClient : ContentPage
    {
        public AddClient(AddClientViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel; // Set the BindingContext to the ViewModel
        }
    }
}