using System;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace ResearchApp.Behaviors
{
      public class DoubleTapBehavior : Behavior<View>
        {
            public static readonly BindableProperty CommandProperty =
                BindableProperty.Create(
                    nameof(Command),
                    typeof(ICommand),
                    typeof(DoubleTapBehavior));

            public ICommand Command
            {
                get => (ICommand)GetValue(CommandProperty);
                set => SetValue(CommandProperty, value);
            }

            protected override void OnAttachedTo(View bindable)
            {
                base.OnAttachedTo(bindable);
                AttachGesture(bindable);
            }

            private void AttachGesture(View view)
            {
                var tapGesture = new TapGestureRecognizer
                {
                    NumberOfTapsRequired = 2,
                    Buttons = ButtonsMask.Primary // Support mouse left-click
                };
                tapGesture.Tapped += OnDoubleTapped;
                view.GestureRecognizers.Add(tapGesture);
            }

            private void OnDoubleTapped(object sender, EventArgs e)
            {
                if (Command?.CanExecute(null) == true && sender is View view)
                {
                    Command.Execute(view.BindingContext); // Pass the tapped item
                }
            }

            protected override void OnDetachingFrom(View bindable)
            {
                base.OnDetachingFrom(bindable);
                if (bindable.GestureRecognizers.FirstOrDefault(x => x is TapGestureRecognizer tgr && tgr.NumberOfTapsRequired == 2)
                    is TapGestureRecognizer tapGesture)
                {
                    tapGesture.Tapped -= OnDoubleTapped;
                }
            }
        }
    }

