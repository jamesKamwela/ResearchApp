using ResearchApp.DataStorage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResearchApp.Models;
using System.Collections.ObjectModel;
using Syncfusion.Maui.Core.Carousel;
using ResearchApp.ViewModels;
namespace ResearchApp.Views;

public partial class DirectoryPage : ContentPage
{
    public DirectoryPage(ContactsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as IDisposable)?.Dispose();

    

}

}
   




