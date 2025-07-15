using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Helpers
{
    public interface ITabNavigationHelper
    {
        void SwitchToTab(int index);
        Task NavigateToAsync(Page page);
    }
}
