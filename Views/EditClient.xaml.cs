

using ResearchApp.Models;
using ResearchApp.ViewModels;

namespace ResearchApp.Views;

public partial class EditClient : ContentPage
{
	public EditClient(EditClientViewModel viewModel )
	{
        InitializeComponent();
        BindingContext =  viewModel;

    }
}


