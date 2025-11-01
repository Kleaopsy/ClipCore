using ClipCore.Assets.Functions;
using ClipCore.Assets.Pages;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;
using Windows.UI.Popups;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;

namespace ClipCore
{
    public sealed partial class ClipCoreWindow : Window
    {
        public static new ClipCoreWindow? Current { get; private set; }
        public static int desiredWidth = 570;
        private TrayIconManager _trayIconManager;
        private bool centered;
        private Homepage _homepage;
        private LocalizationManager _localizationManager;

        public ClipCoreWindow()
        {
            Current = this;
            InitializeComponent();

            _localizationManager = LocalizationManager.Instance;
            _localizationManager.LanguageChanged += OnLanguageChanged;

            // Apply Tray Icon
            _trayIconManager = new TrayIconManager();
            string iconPath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "Assets", "Images", "Logos", "ClipCoreIcon32.ico"
            );
            _trayIconManager.InitializeTrayIcon(this, iconPath, "ClipCore");

            ClipBoardManager.Instance.StartMonitoring();

            // Apply Custom Styles
            this.Activated += ClipCoreWindow_Activated;
            StyleFunctions.WindowCustom(this, AppTitleBar);

            // Apply Resize Styles
            AppWindow.Changed += (sender, args) =>
            {
                if (AppWindow.Size.Width >= desiredWidth && isOpenedFirstTime)
                {
                    isOpenedFirstTime = false;
                    DesignFunctions.StackPanelToggleAnimation(mainSplitView, mainStackPanel);
                    Navigations.AnimateIndicator(homeButton, indicatorBorder);
                }
                this.SizeChanged += ClipCoreWindow_SizeChanged;
            };
            AppWindow.Closing += (sender, args) =>
            {
                this.SizeChanged -= ClipCoreWindow_SizeChanged;
                _trayIconManager.RemoveTrayIcon();
                ClipBoardManager.Instance.StopMonitoring();
            };

            // Apply Content
            _homepage = new Homepage();
            contentFrame.Navigate(typeof(Homepage));
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateUILanguage();
        }

        private void UpdateUILanguage()
        {
            var loc = _localizationManager;
            
            HomepageText.Text = loc.Get("Homepage");
            FavoritesText.Text = loc.Get("Favorites");
            SettingsText.Text = loc.Get("Settings");
            ExitText.Text = loc.Get("Exit");

            FlyoutSearchBox.Text = loc.Get("Search");
            SearchBox.Text = loc.Get("Search");
        }

        private void UpdateTrayIcon()
        {
            //_trayIconManager.UpdateIcon(@"Assets/new-icon.ico");
            _trayIconManager.UpdateTooltip("ClipCore - 5 öðe kopyalandý");
        }

        private void ClipCoreWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(@"Assets\Images\Logos\ClipCoreIcon256.ico");

            if (this.centered is false)
            {
                DesignFunctions.Center(this);
                centered = true;
            }
        }

        private void ClipCoreWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            DesignFunctions.Resize(this, appIcon, AppTitleBar, NavigationBtn, SearchIconBtn, SearchBox, mainSplitView);
        }

        bool isOpenedFirstTime = true;
        private void NavigationBtn_Click(object sender, RoutedEventArgs e)
        {
            DesignFunctions.StackPanelToggleAnimation(mainSplitView, mainStackPanel);
            if (isOpenedFirstTime)
            {
                isOpenedFirstTime = false;
                Navigations.AnimateIndicator(homeButton, indicatorBorder);
            }
        }

        public async void HomeButton_Click(object? sender, RoutedEventArgs? e)
        {
            contentFrame.Navigate(typeof(Homepage));
            Navigations.AnimateIndicator(homeButton, indicatorBorder);

            if (contentFrame.Content is Homepage homepage)
            {
                Homepage.ToggleFavoritesFilter(homepage, false);
            }

            await Task.Delay(350);
            if (!IsScreenLarge())
                mainSplitView.IsPaneOpen = false;
        }

        static public async void HomepageSwitch(ClipCoreWindow windowInstance)
        {
            windowInstance.HomeButton_Click(null, null);
        }

        private async void FavoritesButton_Click(object? sender, RoutedEventArgs? e)
        {
            contentFrame.Navigate(typeof(Homepage));
            Navigations.AnimateIndicator(favoritesButton, indicatorBorder);

            if (contentFrame.Content is Homepage homepage)
            {
                Homepage.ToggleFavoritesFilter(homepage, true);
            }

            await Task.Delay(350);
            if (!IsScreenLarge())
                mainSplitView.IsPaneOpen = false;
        }

        static public async void FavoritesPageSwitch(ClipCoreWindow windowInstance)
        {
            windowInstance.FavoritesButton_Click(null, null);
        }


        private async void SettingsButton_Click(object? sender, RoutedEventArgs? e)
        {
            contentFrame.Navigate(typeof(Settings));
            Navigations.AnimateIndicator(settingsButton, indicatorBorder);
            await Task.Delay(350);
            if (!IsScreenLarge())
                mainSplitView.IsPaneOpen = false;
        }

        static public async void SettingsPageSwitch(ClipCoreWindow windowInstance)
        {
            windowInstance.SettingsButton_Click(null, null);
        }

        private async void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var loc = _localizationManager;

            ContentDialog exitDialog = new ContentDialog
            {
                Title = loc.Get("ExitApplication"),
                Content = loc.Get("ExitConfirmation"),
                PrimaryButtonText = loc.Get("Yes"),
                CloseButtonText = loc.Get("No"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = mainSplitView.XamlRoot
            };

            var result = await exitDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (!IsScreenLarge())
                    mainSplitView.IsPaneOpen = false;
                await Task.Delay(500);
                Application.Current.Exit();
            }
        }

        private bool IsScreenLarge()
        {
            var appWindow = this.AppWindow;
            return (appWindow.Size.Width >= 490) ? true : false;
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            {
                string searchText = sender.Text;

                if (sender.Name == "SearchBox")
                {
                    FlyoutSearchBox.Text = sender.Text;
                }
                else
                {
                    SearchBox.Text = sender.Text;
                }

                if (contentFrame.Content is Homepage homepage) {
                    homepage.SearchClipboards(searchText);
                }
            }
        }
    }
}
