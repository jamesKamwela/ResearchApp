using ResearchApp.Views;

namespace ResearchApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("Main", typeof(MainPage));
            Routing.RegisterRoute("Directory", typeof(DirectoryPage));
            Routing.RegisterRoute("AddClient", typeof(AddClient));
            Routing.RegisterRoute("AddEmployee", typeof(AddEmployee));
            Routing.RegisterRoute("Dashboard", typeof(Dashboard));
            Routing.RegisterRoute("Settings", typeof(Settings));
            Routing.RegisterRoute("Profile", typeof(Profile));
            Routing.RegisterRoute("EditClient", typeof(EditClient));
            Routing.RegisterRoute("EditEmployee", typeof(EditEmployee));
            Routing.RegisterRoute("EditJobPage", typeof(EditJobPage));
            Routing.RegisterRoute("ActiveEmployees" , typeof(ActiveEmployees));
            Routing.RegisterRoute("Isvar", typeof(Isvar));
            Routing.RegisterRoute("PendingJobs", typeof(PendingJobs));
            Routing.RegisterRoute("EmployeeWeeklyJobsList", typeof(EmployeeWeeklyJobsList));
            Routing.RegisterRoute("AddnewJob", typeof(AddnewJob));
            Routing.RegisterRoute("EmployeeJobsData", typeof(EmployeeJobsData));
            Routing.RegisterRoute("ClientWorkRecordsData" , typeof(ClientWorkRecordsData));
            Routing.RegisterRoute("ClientJobsData", typeof(ClientJobsData));



        }
    }
}
