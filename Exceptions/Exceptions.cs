using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Exceptions
{
    public class DataConstraintException : Exception
    {
        public DataConstraintException() { }
        public DataConstraintException(string message) : base(message) { }
        public DataConstraintException(string message, Exception inner) : base(message, inner) { }
        // Add this new method:
        public string GetConstraintMessage()
        {
            if (Message.Contains("Phone", StringComparison.OrdinalIgnoreCase))
                return "Phone number already exists in our system";
            if (Message.Contains("Email", StringComparison.OrdinalIgnoreCase))
                return "Email address is already registered";
            if (Message.Contains("Unique", StringComparison.OrdinalIgnoreCase))
                return "This value must be unique";

            return "Invalid data entered. Please check your input.";
        }
    }
    public class DatabaseInitializationException : Exception
    {
        public DatabaseInitializationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
    public class DatabaseOperationException : Exception
    {
        public DatabaseOperationException() { }
        public DatabaseOperationException(string message) : base(message) { }
        public DatabaseOperationException(string message, Exception inner) : base(message, inner) { }
    }
    public class DatabaseValidationException : Exception
    {
        public DatabaseValidationException(string message) : base(message) { }
    }
}