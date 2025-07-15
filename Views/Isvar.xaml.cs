using ResearchApp.ViewModels;
using System.Diagnostics;


namespace ResearchApp.Views;

public partial class Isvar : ContentView
{
    public Isvar(WorkEntryViewModel viewModel)
    {
        try
        {
            Debug.WriteLine("Isvar: Initializing component");
            InitializeComponent();
            BindingContext = viewModel;
            Debug.WriteLine("Isvar: BindingContext set successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Isvar: Exception during initialization - {ex.Message}");
        }
    }

}

