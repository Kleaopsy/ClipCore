using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Reflection;

namespace ClipCore.Assets.Functions
{
    public static class CursorHelper
    {
        private static readonly PropertyInfo? ProtectedCursorProperty;

        static CursorHelper()
        {
            ProtectedCursorProperty = typeof(UIElement).GetProperty("ProtectedCursor",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static readonly DependencyProperty CursorProperty =
            DependencyProperty.RegisterAttached(
                "Cursor",
                typeof(InputSystemCursorShape),
                typeof(CursorHelper),
                new PropertyMetadata(InputSystemCursorShape.Arrow, OnCursorChanged)
            );

        public static InputSystemCursorShape GetCursor(DependencyObject obj)
        {
            return (InputSystemCursorShape)obj.GetValue(CursorProperty);
        }

        public static void SetCursor(DependencyObject obj, InputSystemCursorShape value)
        {
            obj.SetValue(CursorProperty, value);
        }

        private static void SetProtectedCursor(UIElement element, InputCursor cursor)
        {
            ProtectedCursorProperty?.SetValue(element, cursor);
        }

        private static void OnCursorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                var cursorShape = (InputSystemCursorShape)e.NewValue;

                element.PointerEntered -= Element_PointerEntered;
                element.PointerExited -= Element_PointerExited;

                element.PointerEntered += Element_PointerEntered;
                element.PointerExited += Element_PointerExited;

                void Element_PointerEntered(object sender, PointerRoutedEventArgs args)
                {
                    if (sender is UIElement uiElement && uiElement.XamlRoot?.Content != null)
                    {
                        var cursor = InputSystemCursor.Create(cursorShape);
                        SetProtectedCursor(uiElement.XamlRoot.Content, cursor);
                    }
                }

                void Element_PointerExited(object sender, PointerRoutedEventArgs args)
                {
                    if (sender is UIElement uiElement && uiElement.XamlRoot?.Content != null)
                    {
                        var cursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                        SetProtectedCursor(uiElement.XamlRoot.Content, cursor);
                    }
                }
            }
        }
    }
}