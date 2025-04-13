using ResearchApp.Views;

namespace ResearchApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("main", typeof(MainPage));
            Routing.RegisterRoute("directory", typeof(DirectoryPage));
            Routing.RegisterRoute("AddClient", typeof(AddClient));
            Routing.RegisterRoute("AddEmployee", typeof(AddEmployee));
            Routing.RegisterRoute("Dashboard", typeof(Dashboard));
            Routing.RegisterRoute("Settings", typeof(Settings));
            Routing.RegisterRoute("Profile", typeof(Profile));
            Routing.RegisterRoute("editClient", typeof(EditClient));
            Routing.RegisterRoute("editEmployee", typeof(EditEmployee));

        }
    }
}
