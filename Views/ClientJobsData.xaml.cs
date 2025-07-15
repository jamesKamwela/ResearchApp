using ResearchApp.ViewModels;
using Microsoft.Maui.Controls;

namespace ResearchApp.Views
{
    public partial class ClientJobsData : ContentPage
    {
        public ClientJobsData(ClientJobsDataViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        
    }
}