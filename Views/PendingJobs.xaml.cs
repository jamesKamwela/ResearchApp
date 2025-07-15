using ResearchApp.Models;
using ResearchApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace ResearchApp.Views;

public partial class PendingJobs : ContentView
{
    private readonly PendingJobsViewModel _viewModel;
    private readonly ILogger<PendingJobs> _logger;

    public PendingJobs(PendingJobsViewModel viewModel, ILogger<PendingJobs> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        BindingContext = _viewModel;
    }

    private async void OnCompleteToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            if (sender is Switch switchControl && switchControl.BindingContext is WorkRecord record)
            {
                _logger?.LogInformation($"Toggling completion for job ID: {record.Id}, New Value: {e.Value}");

                // Let the ViewModel handle the logic
                await _viewModel.OnCompleteToggledAsync(record, e.Value);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error toggling job completion");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to update job status. Please try again.", "OK");
        }
    }

    private async void OnPaidToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            if (sender is Switch switchControl && switchControl.BindingContext is WorkRecord record)
            {
                _logger?.LogInformation($"Toggling payment for job ID: {record.Id}, New Value: {e.Value}");

                // Let the ViewModel handle the logic
                await _viewModel.OnPaidToggledAsync(record, e.Value);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error toggling job payment status");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to update job payment status. Please try again.", "OK");
        }
    }
}