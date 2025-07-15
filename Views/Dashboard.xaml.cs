using ResearchApp.ViewModels;

namespace ResearchApp.Views;

public partial class Dashboard : ContentPage
{
	private readonly DashboardViewModel _viewModel;

	public Dashboard(DashboardViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
    }
}