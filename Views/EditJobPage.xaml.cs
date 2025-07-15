using ResearchApp.ViewModels;
using System.Diagnostics;

namespace ResearchApp.Views;


public partial class EditJobPage : ContentPage
{
	public EditJobPage(EditJobsViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
    }
    
}