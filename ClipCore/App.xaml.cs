using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Runtime.Versioning;
using Microsoft.UI.Windowing;
using ClipCore.Assets.Functions;
using WinUIEx;

namespace ClipCore
{
    public partial class App : Application
    {
        private Window? _window;
        private FrameworkElement? _rootElement;

        public App()
        {
            InitializeComponent();
        }

        [SupportedOSPlatform("windows10.0.17763.0")]
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new ClipCoreWindow(); 
            string[] cmdArgs = Environment.GetCommandLineArgs();
            var appWindow = _window.AppWindow;

            if (appWindow.TitleBar != null)
            {
                StyleFunctions.TitleBarCustomColors(_window);

                appWindow.Changed += (sender, args) => {
                    if (args.DidPresenterChange && _window != null)
                    {
                        StyleFunctions.TitleBarCustomColors(_window);
                    }
                };
            }

            _window.Activated += (sender, args) => {
                if (_window?.Content is FrameworkElement rootElement)
                {
                    _rootElement = rootElement;
                    rootElement.ActualThemeChanged += OnThemeChanged;
                }
            };

            _window.Closed += (sender, args) => {
                if (_rootElement != null)
                {
                    _rootElement.ActualThemeChanged -= OnThemeChanged;
                    _rootElement = null;
                }
                _window = null;
            };

            await SettingsManager.Instance.LoadSettingsAsync();

            _window.Activate();
            if (cmdArgs.Contains("--startupp")) {
                _window.AppWindow.Hide();
            }
        }

        private async void OnThemeChanged(FrameworkElement sender, object args)
        {
            if (_window != null)
            {
                await System.Threading.Tasks.Task.Delay(100);
                if (_window != null) // Double check after delay
                {
                    StyleFunctions.TitleBarCustomColors(_window);
                }
            }
        }
    }
}