using ResearchApp.ViewModels;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ResearchApp.Views;

public partial class ActiveEmployees : ContentView
{
    private readonly ActiveEmployeesViewModel _viewModel;
    private readonly ILogger<ActiveEmployees> _logger;

    public ActiveEmployees(
        ActiveEmployeesViewModel viewModel,
        ILogger<ActiveEmployees> logger)
    {
        try
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            BindingContext = _viewModel;
            _logger.LogDebug("ActiveEmployees view initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing ActiveEmployees view");
            throw;
        }
    }


}