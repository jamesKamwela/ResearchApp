using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ResearchApp.Behaviors
{
    namespace ResearchApp.Behaviors
    {
        public class SelectionChangedBehavior : Behavior<CollectionView>
        {
            public static readonly BindableProperty CommandProperty =
                BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SelectionChangedBehavior));

            public ICommand Command
            {
                get => (ICommand)GetValue(CommandProperty);
                set => SetValue(CommandProperty, value);
            }

            protected override void OnAttachedTo(CollectionView bindable)
            {
                base.OnAttachedTo(bindable);
                bindable.SelectionChanged += OnSelectionChanged;
            }

            protected override void OnDetachingFrom(CollectionView bindable)
            {
                base.OnDetachingFrom(bindable);
                bindable.SelectionChanged -= OnSelectionChanged;
            }

            private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (Command?.CanExecute(e.CurrentSelection.FirstOrDefault()) == true)
                {
                    Command.Execute(e.CurrentSelection.FirstOrDefault());
                }
            }
        }
    }
}