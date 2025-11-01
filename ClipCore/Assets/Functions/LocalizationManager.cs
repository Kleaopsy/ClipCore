using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClipCore.Assets.Functions
{
    public class LocalizationManager
    {
        private static LocalizationManager? _instance;
        public static LocalizationManager Instance => _instance ??= new LocalizationManager();

        private Dictionary<string, string> _translations = new Dictionary<string, string>();
        private string _currentLanguage = "en-US";

        public event EventHandler? LanguageChanged;

        public string CurrentLanguage => _currentLanguage;

        public List<LanguageOption> AvailableLanguages => new List<LanguageOption>
        {
            new LanguageOption { Code = "en-US", Name = "English", Flag = "🇺🇸" },
            new LanguageOption { Code = "fr-FR", Name = "Français", Flag = "🇫🇷" },
            new LanguageOption { Code = "de-DE", Name = "Deutsch", Flag = "🇩🇪" },
            new LanguageOption { Code = "es-ES", Name = "Español", Flag = "🇪🇸" },
            new LanguageOption { Code = "tr-TR", Name = "Türkçe", Flag = "🇹🇷" }
        };

        private LocalizationManager()
        {
            _ = LoadLanguageAsync(_currentLanguage);
        }

        public async Task LoadLanguageAsync(string languageCode)
        {
            try
            {
                var languageFile = Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets", "Languages", $"{languageCode}.json"
                );

                if (!File.Exists(languageFile))
                {
                    System.Diagnostics.Debug.WriteLine($"Language file not found: {languageFile}");

                    // FALLBACK: İngilizce'yi dene
                    if (languageCode != "en-US")
                    {
                        await LoadLanguageAsync("en-US");
                    }
                    return;
                }

                var json = await File.ReadAllTextAsync(languageFile);

                if (string.IsNullOrWhiteSpace(json))
                {
                    System.Diagnostics.Debug.WriteLine($"Language file is empty: {languageFile}");
                    return;
                }

                var newTranslations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (newTranslations == null || newTranslations.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"No translations loaded from: {languageFile}");
                    return;
                }

                _translations = newTranslations;
                _currentLanguage = languageCode;
                LanguageChanged?.Invoke(this, EventArgs.Empty);

                System.Diagnostics.Debug.WriteLine($"Language loaded successfully: {languageCode} ({_translations.Count} keys)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Language load error: {ex.Message}");

                // FALLBACK: İngilizce'yi dene
                if (languageCode != "en-US")
                {
                    await LoadLanguageAsync("en-US");
                }
            }
        }

        public string Get(string key)
        {
            if (_translations.TryGetValue(key, out var value))
                return value;

            System.Diagnostics.Debug.WriteLine($"Translation key not found: {key}");

            // FALLBACK: Key'in kendisini döndür (geliştirme aşamasında yararlı)
            return $"[{key}]"; // Boş yerine [KeyName] göster
        }

        // Shorthand alias
        public string T(string key) => Get(key);
    }

    public class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;

        // EKLE BUNU
        public override bool Equals(object? obj)
        {
            return obj is LanguageOption other && Code == other.Code;
        }

        public override int GetHashCode()
        {
            return Code.GetHashCode();
        }
    }
}