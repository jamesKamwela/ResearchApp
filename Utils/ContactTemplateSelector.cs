using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResearchApp.Models;   

namespace ResearchApp.Utils
{
    public class ContactTemplateSelector : DataTemplateSelector
    {
        public DataTemplate EmployeeTemplate { get; set; }
        public DataTemplate ClientTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is EmployeeDisplayModel)
            {
                return EmployeeTemplate;
            }
            else if (item is ClientDisplayModel)
            {
                return ClientTemplate;
            }
            return null;
        }
    }
}
