using ResearchApp.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SQLite;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using ResearchApp;
using System.Diagnostics;
using Microsoft.Extensions.Logging;


namespace ResearchApp.Views; 
public partial class EditEmployee : ContentPage
{
	public EditEmployee(EditEmployeeViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
    }
}