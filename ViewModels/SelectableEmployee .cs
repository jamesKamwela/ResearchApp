using CommunityToolkit.Mvvm.ComponentModel;
using ResearchApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.ViewModels
{
    public class SelectableEmployee : ObservableObject
    {
        public Employee Employee { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SelectableEmployee(Employee employee)
        {
            Employee = employee;
        }
    }
}
