using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResearchApp.Behaviors
{
    public class NumericValidationBehavior : Behavior<Entry>
    {
        public Style ValidStyle { get; set; }
        public Style InvalidStyle { get; set; }
        public decimal MinimumValue { get; set; } = decimal.MinValue;
        public decimal MaximumValue { get; set; } = decimal.MaxValue;

        protected override void OnAttachedTo(Entry entry)
        {
            entry.TextChanged += OnEntryTextChanged;
            base.OnAttachedTo(entry);
        }

        protected override void OnDetachingFrom(Entry entry)
        {
            entry.TextChanged -= OnEntryTextChanged;
            base.OnDetachingFrom(entry);
        }

        private void OnEntryTextChanged(object sender, TextChangedEventArgs args)
        {
            if (sender is Entry entry)
            {
                if (decimal.TryParse(args.NewTextValue, out decimal value) &&
                    value >= MinimumValue &&
                    value <= MaximumValue)
                {
                    entry.Style = ValidStyle;
                }
                else
                {
                    entry.Style = InvalidStyle;
                }
            }
        }
    }
}
