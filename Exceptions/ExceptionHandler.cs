using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ResearchApp.DataStorage;
using ResearchApp.Models;
using System.Diagnostics;
using System.Threading.Tasks;
using static Microsoft.Maui.ApplicationModel.Permissions;
using System.Net;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.Globalization;
using Windows.Media.Protection.PlayReady;
using SQLite;
using ResearchApp.Exceptions;

namespace ResearchApp.Exceptions
{
    public static class ExceptionHandler
    {
        public static async Task HandleDatabaseException(
            this Exception ex,
            ILogger logger,
            Page page)
        {
            switch (ex)
            {
                case DataConstraintException constraintEx:
                    await HandleConstraint(constraintEx, page);
                    break;
                case DatabaseOperationException dbEx:
                    logger.LogError(dbEx, "Database operation failed");
                    await page.DisplayAlert("Error", dbEx.Message, "OK");
                    break;
                default:
                    logger.LogError(ex, "Unexpected error");
                    await page.DisplayAlert("Error", "An unexpected error occurred", "OK");
                    break;
            }
        }

        private static async Task HandleConstraint(DataConstraintException ex, Page page)
        {
            string message = ex.GetConstraintMessage();
            await page.DisplayAlert("Validation Error", message, "OK");
        }
  
    } 
}
