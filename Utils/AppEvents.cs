using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Utils
{
    public static class AppEvents
    {
        public static event Action ContactsChanged;

        public static void NotifyContactsChanged()
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI ||
                DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            {
                ContactsChanged?.Invoke();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() => ContactsChanged?.Invoke());
            }
        }
    }
}
