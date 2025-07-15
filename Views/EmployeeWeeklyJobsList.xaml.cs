using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Models;

namespace ResearchApp.Views
{
    public partial class EmployeeWeeklyJobsList : ContentPage
    {
        private readonly EmployeeWeeklyJobsViewModel _viewModel;
        private readonly IEmployeeDatabaseService _employeeDatabase;

        private Employee _employee;

        public Employee Employee
        {
            get => _employee;
            set
            {
                _employee = value;
                _viewModel.Employee = value;
                Title = $"{value.Name}'s Jobs";
                _viewModel.LoadDataCommand.Execute(null);
            }
        }

        public EmployeeWeeklyJobsList(IWorkRecordDatabaseService workRecordDatabase,
            IEmployeeDatabaseService _employeeDatabase,

        ILogger<EmployeeWeeklyJobsList> logger)
        {
            InitializeComponent();  // This is the missing method

            _viewModel = new EmployeeWeeklyJobsViewModel(workRecordDatabase,_employeeDatabase,

                logger as ILogger<EmployeeWeeklyJobsViewModel>);
            BindingContext = _viewModel;
        }
    }
}