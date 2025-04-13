

using ResearchApp.Models;
using ResearchApp.ViewModels;

namespace ResearchApp.Views;

public partial class EditClient : ContentPage
{
	public EditClient()
	{
        InitializeComponent();
        BindingContext = new EditClientViewModel();

    }
}