using SQLite;

namespace ResearchApp
{
    public static class Constants
    {
        // Database file names
        public const string EmployeesDatabaseFilename = "Employees.db3";
        public const string ClientsDatabaseFilename = "Clients.db3";

        // Database paths
        public static string EmployeesDatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, EmployeesDatabaseFilename);

        public static string ClientsDatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, ClientsDatabaseFilename);

        // SQLite connection flags
        public const SQLiteOpenFlags Flags =
            SQLiteOpenFlags.ReadWrite |       // Read and write access
            SQLiteOpenFlags.Create |          // Create the database if it doesn't exist
            SQLiteOpenFlags.SharedCache;      // Enable shared cache for better performance
    }
}