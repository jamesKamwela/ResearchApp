using ResearchApp.Models;
using ResearchApp.ViewModels;
using Microsoft.Extensions.Logging;
namespace ResearchApp.Views;

public partial class ClientWorkRecordsData : ContentView
{
	private readonly ILogger<ClientWorkRecordsData> _logger;
	private readonly ClientWorkRecordsDataViewModel _viewModel;
    public  ClientWorkRecordsData( ILogger<ClientWorkRecordsData> logger,
		ClientWorkRecordsDataViewModel viewModel

        )
	{
		InitializeComponent();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		BindingContext = _viewModel;
		_logger.LogDebug("ClientWorkRecordsData view initialized successfully.");

    }
}