using ClipCore.Assets.Functions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ClipCore.Assets.Pages
{
    public sealed partial class Settings : Page
    {
        private bool _hasUnsavedChanges = false;
        private SettingsManager _settingsManager;
        private LocalizationManager _localizationManager;
        private bool _isInitializing = true;

        public Settings()
        {
            this.InitializeComponent();
            _settingsManager = SettingsManager.Instance;
            _localizationManager = LocalizationManager.Instance;

            this.Loaded += Settings_Loaded;
        }

        private void Settings_Loaded(object sender, RoutedEventArgs e)
        {
            _ = InitializeSettingsAsync();
        }

        private async Task InitializeSettingsAsync()
        {
            _isInitializing = true;

            await _settingsManager.LoadSettingsAsync();

            LoadSettings();
            UpdateUILanguage();

            // Subscribe to language changes
            _localizationManager.LanguageChanged += OnLanguageChanged;

            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Load language options
            LanguageComboBox.ItemsSource = _localizationManager.AvailableLanguages;

            // Select current language - INDEX kullan, SelectedItem yerine
            var currentLangIndex = _localizationManager.AvailableLanguages
                .FindIndex(l => l.Code == _settingsManager.Settings.Language);

            if (currentLangIndex >= 0)
            {
                LanguageComboBox.SelectedIndex = currentLangIndex;
            }
            else
            {
                // Fallback: İngilizce'yi seç
                LanguageComboBox.SelectedIndex = 0;
            }

            // Load startup setting
            LaunchOnStartupToggle.IsOn = _settingsManager.Settings.LaunchOnStartup;

            _hasUnsavedChanges = false;
        }

        private void UpdateUILanguage()
        {
            var loc = _localizationManager;

            SettingsTitle.Text = loc.Get("SettingsTitle");
            LanguageSettingsTitle.Text = loc.Get("LanguageSettings");
            SelectLanguageText.Text = loc.Get("SelectLanguage");
            StartupSettingsTitle.Text = loc.Get("StartupSettings");
            LaunchOnStartupToggle.Header = loc.Get("LaunchOnStartup");
            SaveButton.Content = loc.Get("SaveChanges");
            SuccessText.Text = loc.Get("ChangesSaved");
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateUILanguage();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (LanguageComboBox.SelectedItem is LanguageOption selectedLanguage)
            {
                if (_settingsManager.Settings.Language != selectedLanguage.Code)
                {
                    _settingsManager.Settings.Language = selectedLanguage.Code;
                    MarkAsChanged();
                }
            }
        }

        private void LaunchOnStartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsManager.Settings.LaunchOnStartup != LaunchOnStartupToggle.IsOn)
            {
                _settingsManager.Settings.LaunchOnStartup = LaunchOnStartupToggle.IsOn;
                MarkAsChanged();
            }
        }

        private void MarkAsChanged()
        {
            if (!_hasUnsavedChanges)
            {
                _hasUnsavedChanges = true;
                ShowSaveButton();
            }
        }

        private void ShowSaveButton()
        {
            SaveButtonContainer.Visibility = Visibility.Visible;

            var fadeIn = new Storyboard();

            var fadeInAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(fadeInAnim, SaveButtonContainer);
            Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            fadeIn.Children.Add(fadeInAnim);

            var slideUp = new DoubleAnimation
            {
                From = 50,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(slideUp, SaveButtonContainer);
            Storyboard.SetTargetProperty(slideUp, "(UIElement.RenderTransform).(TranslateTransform.Y)");
            fadeIn.Children.Add(slideUp);

            SaveButtonContainer.RenderTransform = new TranslateTransform();
            fadeIn.Begin();
        }

        private async void HideSaveButton()
        {
            var fadeOut = new Storyboard();

            var fadeOutAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            Storyboard.SetTarget(fadeOutAnim, SaveButtonContainer);
            Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
            fadeOut.Children.Add(fadeOutAnim);

            fadeOut.Completed += (s, e) => { SaveButtonContainer.Visibility = Visibility.Collapsed; };
            fadeOut.Begin();

            await Task.Delay(200);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;

            await _settingsManager.SaveSettingsAsync();

            _hasUnsavedChanges = false;
            HideSaveButton();
            await ShowSuccessNotification();

            SaveButton.IsEnabled = true;
        }

        private async Task ShowSuccessNotification()
        {
            SuccessNotification.Visibility = Visibility.Visible;
            SuccessNotification.Opacity = 0;

            var fadeIn = new Storyboard();
            var fadeInAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(fadeInAnim, SuccessNotification);
            Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            fadeIn.Children.Add(fadeInAnim);
            fadeIn.Begin();

            await Task.Delay(2500);

            var fadeOut = new Storyboard();
            var fadeOutAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            Storyboard.SetTarget(fadeOutAnim, SuccessNotification);
            Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
            fadeOut.Children.Add(fadeOutAnim);
            fadeOut.Completed += (s, e) => { SuccessNotification.Visibility = Visibility.Collapsed; };
            fadeOut.Begin();
        }
    }
}