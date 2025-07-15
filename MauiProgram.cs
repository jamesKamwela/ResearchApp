using CommunityToolkit.Maui;
using Microcharts.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.Helpers;
using ResearchApp.ViewModels;
using ResearchApp.Views;
using System.Diagnostics;


namespace ResearchApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()

                .UseMauiCommunityToolkit() // Add the Community Toolkit
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            builder.UseMicrocharts();
            builder.Services.AddLogging(configure =>
            {
                configure.AddDebug(); // Log to debug output
                                        // Add other providers as needed
            });


#if DEBUG
            builder.Logging.AddDebug(); // Enable debug logging in DEBUG mode
#endif

            // Register services
            RegisterServices(builder.Services);

            // Register ViewModels
            RegisterViewModels(builder.Services);

            // Register Pages
            RegisterPages(builder.Services);

            return builder.Build();
        }





        private static void RegisterServices(IServiceCollection services)
        {

            try
            {
                

                // Register EmployeeDatabaseService
                services.AddSingleton<IEmployeeDatabaseService>(provider =>
                    new EmployeeDatabaseService(Constants.EmployeesDatabasePath,provider.GetRequiredService<ILogger<EmployeeDatabaseService>>()));

                // Register ClientDatabaseService
                services.AddSingleton<IClientDatabaseService>(provider =>
                    new ClientDatabaseService(Constants.ClientsDatabasePath,provider.GetRequiredService<ILogger<ClientDatabaseService>>()));
                // Register JobDatabaseService
                services.AddSingleton<IJobDatabaseService>(provider =>
                    new JobDatabaseService(Constants.ClientsDatabasePath));
                // Register WorkRecordDatabaseService
                services.AddSingleton<IWorkRecordDatabaseService>(provider=>
                                    new WorkRecordDatabaseService(Constants.WorkRecordDatabasePath, provider.GetRequiredService<ILogger<WorkRecordDatabaseService>>()));
              

                Debug.WriteLine("Database services registered successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering services: {ex.Message}");
                throw;
            }
        }

        private static void RegisterViewModels(IServiceCollection services)
        {

         // Register ViewModels
            services.AddTransient<ContactsViewModel>();
            services.AddTransient<AddEmployeeViewModel>();
            services.AddTransient<AddClientViewModel>();
            services.AddTransient<EditEmployeeViewModel>();
            services.AddTransient<EditClientViewModel>();
            services.AddTransient<EditJobsViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<WorkEntryViewModel>();
            services.AddTransient<PendingJobsViewModel>();
            services.AddTransient<ActiveEmployeesViewModel>();
            services.AddTransient<EmployeeWeeklyJobsViewModel>();
            services.AddTransient<AddNewJobViewModel>();
            services.AddTransient<EmployeeJobsDataViewModel>();
            services.AddTransient<ClientWorkRecordsDataViewModel>();
            services.AddTransient<ClientJobsDataViewModel>();



            services.AddTransient<WorkEntryViewModel>(provider => new WorkEntryViewModel(
                provider.GetRequiredService<IWorkRecordDatabaseService>(),
                provider.GetRequiredService<ILogger<WorkEntryViewModel>>(),
                provider.GetRequiredService<ITabNavigationHelper>(),
                provider.GetRequiredService<IClientDatabaseService>(),
                provider.GetRequiredService<IEmployeeDatabaseService>()
     ));

            services.AddTransient<PendingJobsViewModel>(provider =>
                new PendingJobsViewModel(
                    provider.GetRequiredService<ITabNavigationHelper>(), 
                    provider.GetRequiredService<IWorkRecordDatabaseService>(),
                    provider.GetRequiredService<IClientDatabaseService>(),
                    provider.GetRequiredService<IEmployeeDatabaseService>(),
                    provider.GetRequiredService<ILogger<PendingJobsViewModel>>()
                ));



            services.AddTransient<ActiveEmployeesViewModel>(provider =>
            new ActiveEmployeesViewModel(
                provider.GetRequiredService<IEmployeeDatabaseService>(),
                provider.GetRequiredService<ITabNavigationHelper>(),
                provider.GetRequiredService<IWorkRecordDatabaseService>(),
                provider.GetRequiredService<ILogger<ActiveEmployeesViewModel>>()
                ));

            services.AddTransient<ClientWorkRecordsDataViewModel>(provider=>
            new ClientWorkRecordsDataViewModel(
                provider.GetRequiredService<ITabNavigationHelper>(),
                provider.GetRequiredService<IClientDatabaseService>(),
                provider.GetRequiredService<IWorkRecordDatabaseService>(),
                provider.GetRequiredService<ILogger<ClientWorkRecordsDataViewModel>>()
                ));

        }

        private static void RegisterPages(IServiceCollection services)
        {
            // Register Pages
         
            services.AddSingleton<TestTabbedPage>();
            services.AddSingleton<ITabNavigationHelper>(provider =>provider.GetRequiredService<TestTabbedPage>());



            services.AddTransient<AddEmployee>();
            services.AddTransient<AddClient>();
            services.AddTransient<Dashboard>();
            services.AddTransient<Settings>();
            services.AddTransient<Profile>();
            services.AddTransient<EditEmployee>();
            services.AddTransient<EditClient>();
            services.AddTransient<DirectoryPage>();
            services.AddTransient<EditJobPage>();
            services.AddTransient<ActiveEmployees>();
            services.AddTransient<Isvar>();
            services.AddTransient<PendingJobs>();
            services.AddTransient<ActiveEmployees>();
            services.AddTransient<EmployeeWeeklyJobsList>();
            services.AddTransient<AddnewJob>();
            services.AddTransient<EmployeeJobsData>();
            services.AddTransient<ClientWorkRecordsData>();
            services.AddTransient<ClientJobsData>();



        }

}


}