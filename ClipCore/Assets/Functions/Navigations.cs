using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClipCore.Assets.Functions
{
    class Navigations
    {
        private static Button? _lastSelectedButton;

        /// <summary>
        /// </summary>
        /// <param name="targetButton">
        /// <param name="indicatorBorder">
        public static void AnimateIndicator(Button targetButton, Border indicatorBorder)
        {
            if (targetButton == null || indicatorBorder == null)
                return;

            if (targetButton.Parent is StackPanel stackPanel)
            {
                targetButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var generalTransform = targetButton.TransformToVisual(stackPanel);

                Point point = generalTransform.TransformPoint(new Point(0, 0));

                indicatorBorder.Height = targetButton.ActualHeight > 0 ? targetButton.ActualHeight/ 2 : 20;

                indicatorBorder.Visibility = Visibility.Visible;

                if (indicatorBorder.RenderTransform == null || !(indicatorBorder.RenderTransform is CompositeTransform))
                {
                    indicatorBorder.RenderTransform = new CompositeTransform();
                }

                CompositeTransform? transform = indicatorBorder.RenderTransform as CompositeTransform;
                DoubleAnimation animation = new DoubleAnimation
                {
                    To = point.Y,
                    Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard storyboard = new Storyboard();
                storyboard.Children.Add(animation);

                Storyboard.SetTarget(animation, transform);
                Storyboard.SetTargetProperty(animation, "TranslateY");

                storyboard.Begin();

                if (_lastSelectedButton != null)
                {
                    _lastSelectedButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                }
                targetButton.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ListViewItemBackgroundSelected"];
                _lastSelectedButton = targetButton;
            }
        }
    }
}
