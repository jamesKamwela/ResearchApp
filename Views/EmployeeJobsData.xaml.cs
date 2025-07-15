using ResearchApp.ViewModels;
namespace ResearchApp.Views;

public partial class EmployeeJobsData : ContentPage
{
	private readonly EmployeeJobsDataViewModel _viewmodel;
    public EmployeeJobsData(EmployeeJobsDataViewModel viewModel )
	{
		InitializeComponent();
		_viewmodel = viewModel;
		BindingContext = _viewmodel;
	}
}