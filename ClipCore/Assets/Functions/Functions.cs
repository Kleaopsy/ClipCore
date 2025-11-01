using ClipCore.Assets.Functions;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Security.Cryptography.Core;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;

namespace ClipCore.Assets.Functions
{
    public static class StyleFunctions
    {
        public static void WindowCustom(Window window, Grid appTitleBar)
        {
            WindowSize(window);
            TitleBarCustomButtons(window, appTitleBar);
            TitleBarCustomColors(window);
        }
        private static void WindowSize(Window window) {
            var appWindow = AppWindow.GetFromWindowId(
                Win32Interop.GetWindowIdFromWindow(
                    WindowNative.GetWindowHandle(window)
                )
            );

            appWindow.Resize(new Windows.Graphics.SizeInt32(350, 550));
            appWindow.SetIcon("Assets/Tiles/GalleryIcon.ico");
            appWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            OverlappedPresenter presenter = OverlappedPresenter.Create();
            presenter.PreferredMinimumWidth = 350;
            presenter.PreferredMinimumHeight = 500;

            appWindow.SetPresenter(presenter);
        }
        private static void TitleBarCustomButtons(Window window, Grid appTitleBar)
        {
            // Extend the application content into the title bar area.
            window.ExtendsContentIntoTitleBar = true;

            // Set the custom XAML Grid as the draggable title bar.
            window.SetTitleBar(appTitleBar);

            var appWindow = AppWindow.GetFromWindowId(
                Win32Interop.GetWindowIdFromWindow(
                    WindowNative.GetWindowHandle(window)
                )
            );

            if (appWindow != null)
            {
                // Get the system buttons placeholder to define the draggable area.
                var systemButtonsPlaceholder = appTitleBar.FindName("SystemButtonsPlaceholder") as Grid;

                if (systemButtonsPlaceholder != null)
                {
                    // Create a rectangle for the draggable area.
                    var dragRects = new RectInt32[]
                    {
                        new RectInt32(0, 0, (int)appTitleBar.ActualWidth - (int)systemButtonsPlaceholder.ActualWidth, (int)appTitleBar.ActualHeight)
                    };

                    // Set the draggable areas for the title bar.
                    appWindow.TitleBar.SetDragRectangles(dragRects);
                }
            }
        }
        public static void TitleBarCustomColors(Window window)
        {
            // Title Bar Customization
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var appWindow = AppWindow.GetFromWindowId(
                    Win32Interop.GetWindowIdFromWindow(
                        WindowNative.GetWindowHandle(window)
                    )
                );

                if (appWindow.TitleBar != null)
                {
                    // System Theme
                    var theme = Application.Current.RequestedTheme;

                    if (theme == ApplicationTheme.Dark)
                    {
                        // Dark
                        appWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 32, 32, 32);
                        appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                        appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 45, 45, 45);
                        appWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 32, 32, 32);
                        appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                    }
                    else
                    {
                        // Light
                        appWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 243, 243, 243);
                        appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                        appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 233, 233, 233);
                        appWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 243, 243, 243);
                        appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                    }
                }
            }
        }
    }
    public static class DesignFunctions
    {
        public static void Resize(Window window, Image appIcon, Grid appTitleBar, Button navigationBtn, Button searchIconBtn, AutoSuggestBox searchBox, SplitView mainSplitView)
        {
            AppWindowTitleBarResize(window, appIcon, appTitleBar, navigationBtn, searchIconBtn, searchBox, mainSplitView);
            ClipboardPreviewDialog.resizedDialog(window.AppWindow.Size.Width, window.AppWindow.Size.Height);
        }
        private static void AppWindowTitleBarResize(Window window, Image appIcon, Grid appTitleBar, Button navigationBtn, Button searchIconBtn, AutoSuggestBox searchBox, SplitView mainSplitView) {
            var appWindow = AppWindow.GetFromWindowId(
                Win32Interop.GetWindowIdFromWindow(
                    WindowNative.GetWindowHandle(window)
                )
            );

            if (appWindow.Size.Width >= ClipCoreWindow.desiredWidth)
            {
                appIcon.Visibility = Visibility.Visible;
                searchBox.Visibility = Visibility.Visible;
                navigationBtn.Visibility = Visibility.Collapsed;
                searchIconBtn.Visibility = Visibility.Collapsed;

                mainSplitView.DisplayMode = SplitViewDisplayMode.Inline;
                mainSplitView.IsPaneOpen = true;
            }
            else
            {
                appIcon.Visibility = Visibility.Collapsed;
                searchBox.Visibility = Visibility.Collapsed;
                navigationBtn.Visibility = Visibility.Visible;
                searchIconBtn.Visibility = Visibility.Visible;

                mainSplitView.DisplayMode = SplitViewDisplayMode.Overlay;
                mainSplitView.IsPaneOpen = false;
            }
        }
        public static void StackPanelToggleAnimation(SplitView mainSplitView, Grid mainStackPanel)
        {
            if (!mainSplitView.IsPaneOpen)
            {
                // Açılış animasyonu
                var menuAnimation = (Storyboard)Application.Current.Resources["MenuSlideInAnimation"];
                menuAnimation.Stop(); // Stop any previous animation
                Storyboard.SetTarget(menuAnimation, mainStackPanel);
                mainSplitView.IsPaneOpen = true;
                menuAnimation.Begin();
            }
            else
            {
                // Kapanış animasyonu
                var menuAnimation = (Storyboard)Application.Current.Resources["MenuSlideOutAnimation"];
                menuAnimation.Stop(); // Stop any previous animation
                Storyboard.SetTarget(menuAnimation, mainStackPanel);

                // evert the IsPaneOpen state after the animation completes
                EventHandler<object>? handler = null;
                handler = (s, e) =>
                {
                    mainSplitView.IsPaneOpen = false;
                    menuAnimation.Completed -= handler; // Clean your event handler
                };
                menuAnimation.Completed += handler;

                menuAnimation.Begin();
            }
        }
        public static void Center(Window window)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            if (AppWindow.GetFromWindowId(windowId) is AppWindow appWindow &&
                DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest) is DisplayArea displayArea)
            {
                PointInt32 CenteredPosition = appWindow.Position;
                CenteredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                CenteredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(CenteredPosition);
            }
        }
    }
}
