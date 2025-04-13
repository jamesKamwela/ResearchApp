namespace ResearchApp;
    using System;
using System.IO;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using ResearchApp.DataStorage;

    public partial class App : Application
    {
        
            public App()
        {
            InitializeComponent();
        //MainPage = new AppShell();
    }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    protected override void OnStart()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {exception}");
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {args.Exception}");
        };
    }
}
