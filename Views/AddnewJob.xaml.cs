using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace ResearchApp.Views;

public partial class AddnewJob : ContentPage
{
    private readonly AddNewJobViewModel _viewModel;

    public AddnewJob( AddNewJobViewModel viewModel)
	{
		InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;


    }
}