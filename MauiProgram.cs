using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using ResearchApp.DataStorage;
using ResearchApp.ViewModels;
using ResearchApp.Views;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ResearchApp.EFDatabase;

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
            builder.Services.AddLogging(configure =>
            {
                configure.AddDebug(); // Log to debug output
                                        // Add other providers as needed
            });

            builder.Services.AddDbContext<ContactsDatabase>(); 

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
                    new EmployeeDatabaseService(Constants.EmployeesDatabasePath));

                // Register ClientDatabaseService
                services.AddSingleton<IClientDatabaseService>(provider =>
                    new ClientDatabaseService(Constants.ClientsDatabasePath,provider.GetRequiredService<ILogger<ClientDatabaseService>>()));
                // Register JobDatabaseService
                services.AddSingleton<IJobDatabaseService>(provider =>
                    new JobDatabaseService(Constants.ClientsDatabasePath));

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
        }

        private static void RegisterPages(IServiceCollection services)
        {
            // Register Pages
            services.AddTransient<AddEmployee>();
            services.AddTransient<AddClient>();
            services.AddTransient<Dashboard>();
            services.AddTransient<Settings>();
            services.AddTransient<Profile>();
            services.AddTransient<EditEmployee>();
            services.AddTransient<EditClient>();
            services.AddTransient<DirectoryPage>();

        }

    }


}