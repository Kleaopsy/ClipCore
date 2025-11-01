using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClipCore.Assets.Functions
{
    public class SettingsManager
    {
        private static SettingsManager? _instance;
        public static SettingsManager Instance => _instance ??= new SettingsManager();

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ClipCore",
            "settings.json"
        );

        private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "ClipCore";

        public AppSettings Settings { get; private set; } = new AppSettings();

        public event EventHandler? SettingsChanged;

        private SettingsManager()
        {
            _ = LoadSettingsAsync();
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(SettingsFilePath);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    Settings = new AppSettings();
                }

                // Apply language
                await LocalizationManager.Instance.LoadLanguageAsync(Settings.Language);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings load error: {ex.Message}");
                Settings = new AppSettings();
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Settings, options);
                await File.WriteAllTextAsync(SettingsFilePath, json);

                // Apply startup setting
                SetStartupRegistry(Settings.LaunchOnStartup);

                // Apply language
                await LocalizationManager.Instance.LoadLanguageAsync(Settings.Language);

                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}");
            }
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Startup registry error: {ex.Message}");
            }
        }

        public bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public class AppSettings
    {
        public string Language { get; set; } = "en-US";
        public bool LaunchOnStartup { get; set; } = false;
    }
}