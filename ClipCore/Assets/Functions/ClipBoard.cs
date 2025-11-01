using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ClipCore.Assets.Functions
{
    public enum ClipboardType
    {
        Text,
        Image,
        File,
        Html,
        Code
    }

    public class ClipBoard : INotifyPropertyChanged
    {
        private bool _isFavorite;

        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public ClipboardType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Icon { get; set; } = string.Empty;
        public bool IsImageType => Type == ClipboardType.Image;

        public ClipBoard()
        {
            Timestamp = DateTime.Now;
            IsFavorite = false;
        }

        public string FormattedTimestamp => FormatTimestamp(Timestamp);

        private string FormatTimestamp(DateTime dt)
        {
            var loc = LocalizationManager.Instance;
            var diff = DateTime.Now - dt;

            if (diff.TotalMinutes < 1)
                return loc.Get("JustNow");
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes}{loc.Get("MinutesAgo")}";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours}{loc.Get("HoursAgo")}";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}{loc.Get("DaysAgo")}";

            return dt.ToString("MMM dd, yyyy");
        }

        public string TypeIcon
        {
            get
            {
                if (Type == ClipboardType.File && !string.IsNullOrEmpty(Content))
                {
                    var firstPath = Content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstPath))
                        return ClipboardPreviewDialog.GetFileIcon(firstPath);
                }

                return Type switch
                {
                    ClipboardType.Text => "\uE8A5",
                    ClipboardType.Image => "\uEB9F",
                    ClipboardType.File => "\uE8B7",
                    ClipboardType.Html => "\uE774",
                    ClipboardType.Code => "\uE943",
                    _ => "\uE8A5"
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ClipBoardManager
    {
        private static ClipBoardManager? _instance;
        public static ClipBoardManager Instance => _instance ??= new ClipBoardManager();

        public ObservableCollection<ClipBoard> ClipboardHistory { get; set; }
        private int _nextId = 1;
        private bool _isMonitoring = false;
        private string _lastClipboardContent = string.Empty;

        // Event to notify when new clipboard item is added
        public event EventHandler<ClipBoard>? ClipboardItemAdded;


        public ClipboardStorageManager _storageManager;
        public List<ClipboardStorageManager.ClipboardIndex> _storageIndex;

        private Task? _loadingTask;

        private ClipBoardManager()
        {
            ClipboardHistory = new ObservableCollection<ClipBoard>();
            _storageManager = new ClipboardStorageManager();
            _storageIndex = new List<ClipboardStorageManager.ClipboardIndex>();

            //LoadSampleData();
            _loadingTask = LoadHistoryFromStorageAsync();
        }

        public async Task EnsureStorageLoadedAsync()
        {
            if (_loadingTask != null)
            {
                await _loadingTask;
                _loadingTask = null;
            }
        }

        private async Task LoadHistoryFromStorageAsync()
        {
            try
            {
                _storageIndex = await _storageManager.LoadIndexAsync();

                await _storageManager.CleanupOldItemsAsync(_storageIndex);

                // SADECE EN YENİ 50 KAYIT (50 yerine)
                var recentItems = _storageIndex
                    .OrderByDescending(x => x.Timestamp)
                    .Take(50)
                    .ToList();

                foreach (var indexItem in recentItems)
                {
                    var clipboardItem = new ClipBoard
                    {
                        Id = indexItem.Id,
                        Timestamp = indexItem.Timestamp,
                        Type = indexItem.Type,
                        PreviewText = indexItem.PreviewText,
                        IsFavorite = indexItem.IsFavorite,
                        Content = string.Empty
                    };

                    ClipboardHistory.Add(clipboardItem);
                }

                if (ClipboardHistory.Count > 0)
                {
                    _nextId = _storageIndex.Max(x => x.Id) + 1; // Index'ten al (History'den değil)
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load history error: {ex.Message}");
            }
        }

        public async Task<string> LoadContentAsync(ClipBoard item)
        {
            if (!string.IsNullOrEmpty(item.Content) && item.Content != "[Lazy Load]")
                return item.Content;

            var indexItem = _storageIndex.FirstOrDefault(x => x.Id == item.Id);
            if (indexItem != null)
            {
                item.Content = await _storageManager.ReadContentAsync(indexItem.ContentPath, item.Type);
            }

            return item.Content;
        }

        private void LoadSampleData()
        {
            ClipboardHistory.Add(new ClipBoard
            {
                Id = _nextId++,
                Type = ClipboardType.Text,
                Content = "Hello World! This is a sample clipboard text that demonstrates how the UI will look with actual content.",
                PreviewText = "Hello World! This is a sample clipboard text...",
                Timestamp = DateTime.Now.AddMinutes(-5),
                IsFavorite = true
            });

            ClipboardHistory.Add(new ClipBoard
            {
                Id = _nextId++,
                Type = ClipboardType.Code,
                Content = "public void Initialize() {\n    Console.WriteLine(\"Sample Code\");\n}",
                PreviewText = "public void Initialize() { ...",
                Timestamp = DateTime.Now.AddHours(-2),
                IsFavorite = false
            });

            ClipboardHistory.Add(new ClipBoard
            {
                Id = _nextId++,
                Type = ClipboardType.Image,
                Content = "[Image Data]",
                PreviewText = "Screenshot_2024_10_08.png",
                Timestamp = DateTime.Now.AddHours(-5),
                IsFavorite = false
            });

            ClipboardHistory.Add(new ClipBoard
            {
                Id = _nextId++,
                Type = ClipboardType.File,
                Content = "C:\\Documents\\report.pdf",
                PreviewText = "report.pdf",
                Timestamp = DateTime.Now.AddDays(-1),
                IsFavorite = true
            });

            ClipboardHistory.Add(new ClipBoard
            {
                Id = _nextId++,
                Type = ClipboardType.Html,
                Content = "<div><h1>Sample HTML</h1><p>Content here</p></div>",
                PreviewText = "<div><h1>Sample HTML</h1>...",
                Timestamp = DateTime.Now.AddDays(-2),
                IsFavorite = false
            });
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            Clipboard.ContentChanged += Clipboard_ContentChanged;
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            Clipboard.ContentChanged -= Clipboard_ContentChanged;
        }

        private async void Clipboard_ContentChanged(object? sender, object e)
        {
            try
            {
                DataPackageView dataPackageView = Clipboard.GetContent();

                // File Control
                if (dataPackageView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await dataPackageView.GetStorageItemsAsync();

                    if (items.Count > 0)
                    {
                        var filePaths = new List<string>();
                        bool allFilesValid = true;

                        foreach (var item in items)
                        {
                            // ClipCore klasörü kontrolü - ÖNCE BU
                            if (_storageManager.IsClipCoreFile(item.Path))
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping ClipCore file: {item.Path}");
                                return; // Tamamen ignore et
                            }

                            // 1GB kontrolü
                            if (!_storageManager.IsFileSizeValid(item.Path))
                            {
                                System.Diagnostics.Debug.WriteLine($"File too large: {item.Name}");
                                allFilesValid = false;
                                break;
                            }

                            filePaths.Add(item.Path);
                        }

                        if (!allFilesValid || filePaths.Count == 0)
                            return;

                        // Storage'a kaydet (dosyaları kopyalayarak)
                        var (contentPath, preview) = await _storageManager.SaveFileReferencesAsync(_nextId, filePaths);

                        if (string.IsNullOrEmpty(contentPath))
                            return;

                        // Duplicate kontrolü
                        if (_storageIndex.Any(x => x.PreviewText == preview))
                            return;

                        var newItem = new ClipBoard
                        {
                            Id = _nextId++,
                            Type = ClipboardType.File,
                            Content = "[Lazy Load]",
                            PreviewText = preview,
                            Timestamp = DateTime.Now,
                            IsFavorite = false
                        };

                        var indexItem = new ClipboardStorageManager.ClipboardIndex
                        {
                            Id = newItem.Id,
                            Timestamp = newItem.Timestamp,
                            Type = newItem.Type,
                            ContentPath = contentPath,
                            PreviewText = preview,
                            IsFavorite = false
                        };

                        _storageIndex.Insert(0, indexItem);
                        await _storageManager.SaveIndexAsync(_storageIndex);

                        AddClipboard(newItem);
                        ClipboardItemAdded?.Invoke(this, newItem);

                        return;
                    }
                }

                // TEXT İŞLEMLERİ
                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    string text = await dataPackageView.GetTextAsync();

                    if (string.IsNullOrWhiteSpace(text) || text == _lastClipboardContent)
                        return;

                    // Duplicate kontrolü
                    if (_storageIndex.Any(x => x.Type == ClipboardType.Text && x.PreviewText == GetPreviewText(text)))
                    {
                        _lastClipboardContent = text;
                        return;
                    }

                    _lastClipboardContent = text;
                    ClipboardType type = DetectContentType(text);

                    var newItem = new ClipBoard
                    {
                        Id = _nextId++,
                        Type = type,
                        Content = "[Lazy Load]",
                        PreviewText = GetPreviewText(text),
                        Timestamp = DateTime.Now,
                        IsFavorite = false
                    };

                    // Storage'a kaydet
                    var contentPath = await _storageManager.SaveTextContentAsync(newItem.Id, text);

                    var indexItem = new ClipboardStorageManager.ClipboardIndex
                    {
                        Id = newItem.Id,
                        Timestamp = newItem.Timestamp,
                        Type = newItem.Type,
                        ContentPath = contentPath,
                        PreviewText = newItem.PreviewText,
                        IsFavorite = false
                    };

                    _storageIndex.Insert(0, indexItem);
                    await _storageManager.SaveIndexAsync(_storageIndex);

                    AddClipboard(newItem);
                    ClipboardItemAdded?.Invoke(this, newItem);
                }
                // IMAGE İŞLEMLERİ
                else if (dataPackageView.Contains(StandardDataFormats.Bitmap))
                {
                    try
                    {
                        var bitmap = await dataPackageView.GetBitmapAsync();
                        var stream = await bitmap.OpenReadAsync();

                        using var memStream = new System.IO.MemoryStream();
                        await stream.AsStreamForRead().CopyToAsync(memStream);
                        var imageBytes = memStream.ToArray();

                        // 1GB kontrolü
                        if (imageBytes.Length > 1024 * 1024 * 1024)
                        {
                            System.Diagnostics.Debug.WriteLine("Image too large");
                            return;
                        }

                        // Basit timestamp-based preview (hash gereksiz artık)
                        var previewText = $"Image_{DateTime.Now:yyyyMMdd_HHmmss}";

                        // Duplicate kontrolü (sadece son 5 saniyedeki aynı timestamp'e bak)
                        var recentDuplicates = _storageIndex
                            .Where(x => x.Type == ClipboardType.Image &&
                                   x.Timestamp > DateTime.Now.AddSeconds(-5) &&
                                   x.PreviewText == previewText)
                            .ToList();

                        if (recentDuplicates.Any())
                        {
                            System.Diagnostics.Debug.WriteLine("Duplicate image detected (within 5 seconds)");
                            return;
                        }

                        var newItem = new ClipBoard
                        {
                            Id = _nextId++,
                            Type = ClipboardType.Image,
                            Content = "[Lazy Load]",
                            PreviewText = previewText,
                            Timestamp = DateTime.Now,
                            IsFavorite = false
                        };

                        // Storage'a kaydet (LOSSLESS PNG)
                        var contentPath = await _storageManager.SaveImageContentAsync(newItem.Id, imageBytes);

                        if (string.IsNullOrEmpty(contentPath))
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to save image");
                            return;
                        }

                        var indexItem = new ClipboardStorageManager.ClipboardIndex
                        {
                            Id = newItem.Id,
                            Timestamp = newItem.Timestamp,
                            Type = newItem.Type,
                            ContentPath = contentPath,
                            PreviewText = newItem.PreviewText,
                            IsFavorite = false
                        };

                        _storageIndex.Insert(0, indexItem);
                        await _storageManager.SaveIndexAsync(_storageIndex);

                        AddClipboard(newItem);
                        ClipboardItemAdded?.Invoke(this, newItem);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Image processing error: {ex.Message}");
                    }
                }
                // HTML İŞLEMLERİ
                else if (dataPackageView.Contains(StandardDataFormats.Html))
                {
                    string html = await dataPackageView.GetHtmlFormatAsync();

                    var preview = GetPreviewText(html);
                    if (_storageIndex.Any(x => x.PreviewText == preview))
                        return;

                    var newItem = new ClipBoard
                    {
                        Id = _nextId++,
                        Type = ClipboardType.Html,
                        Content = "[Lazy Load]",
                        PreviewText = preview,
                        Timestamp = DateTime.Now,
                        IsFavorite = false
                    };

                    // Storage'a kaydet
                    var contentPath = await _storageManager.SaveTextContentAsync(newItem.Id, html);

                    var indexItem = new ClipboardStorageManager.ClipboardIndex
                    {
                        Id = newItem.Id,
                        Timestamp = newItem.Timestamp,
                        Type = newItem.Type,
                        ContentPath = contentPath,
                        PreviewText = newItem.PreviewText,
                        IsFavorite = false
                    };

                    _storageIndex.Insert(0, indexItem);
                    await _storageManager.SaveIndexAsync(_storageIndex);

                    AddClipboard(newItem);
                    ClipboardItemAdded?.Invoke(this, newItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard monitoring error: {ex.Message}");
            }
        }
        private ClipboardType DetectContentType(string text)
        {
            // Kod pattern'lerini kontrol et
            var codePatterns = new[]
            {
                "public ", "private ", "protected ",
                "class ", "interface ", "enum ",
                "void ", "int ", "string ",
                "function ", "const ", "let ", "var ",
                "def ", "import ", "from ",
                "using ", "namespace ",
                "{", "}", "(", ")",
                "=>", "->", "==", "!=",
                "if (", "for (", "while ("
            };

            int codeIndicators = codePatterns.Count(pattern => text.Contains(pattern));

            // Eğer 3 veya daha fazla kod pattern'i varsa kod olarak işaretle
            if (codeIndicators >= 3)
                return ClipboardType.Code;

            // HTML kontrolü
            if (text.Contains("<") && text.Contains(">") &&
                (text.Contains("</") || text.Contains("/>") || text.Contains("<!DOCTYPE")))
                return ClipboardType.Html;

            return ClipboardType.Text;
        }

        private string GetPreviewText(string content, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            // Yeni satırları ve fazla boşlukları temizle
            string cleaned = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();

            if (cleaned.Length <= maxLength)
                return cleaned;

            return cleaned.Substring(0, maxLength) + "...";
        }

        public void AddClipboard(ClipBoard item)
        {
            // En üste ekle
            ClipboardHistory.Insert(0, item);
        }

        public async void ToggleFavorite(ClipBoard item)
        {
            item.IsFavorite = !item.IsFavorite;

            // Index'i güncelle
            var indexItem = _storageIndex.FirstOrDefault(x => x.Id == item.Id);
            if (indexItem != null)
            {
                indexItem.IsFavorite = item.IsFavorite;
                await _storageManager.SaveIndexAsync(_storageIndex);
            }
        }

        public async void RemoveClipboard(ClipBoard item)
        {
            ClipboardHistory.Remove(item);

            var indexItem = _storageIndex.FirstOrDefault(x => x.Id == item.Id);
            if (indexItem != null)
            {
                try
                {
                    var contentPath = indexItem.ContentPath;
                    var fullContentPath = Path.GetFullPath(contentPath);

                    // Kök yolların tam halleri (sonuna separator ekliyoruz karşılaştırma için)
                    var filesRoot = Path.GetFullPath(ClipboardStorageManager.FilesFolderPath) + Path.DirectorySeparatorChar;
                    var dataRoot = Path.GetFullPath(ClipboardStorageManager.DataFolderPath) + Path.DirectorySeparatorChar;
                    var imagesRoot = Path.GetFullPath(ClipboardStorageManager.ImagesFolderPath) + Path.DirectorySeparatorChar;

                    // Eğer contentPath bir klasörse: yalnızca Files kökü altındaysa sil
                    if (Directory.Exists(fullContentPath))
                    {
                        if (fullContentPath.StartsWith(filesRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.Delete(fullContentPath, true);
                        }
                        else
                        {
                            // Data/Images gibi uygulama köklerini kazara silmemek için başka klasörleri silme
                            System.Diagnostics.Debug.WriteLine($"Skipping directory delete (not under Files): {fullContentPath}");
                        }
                    }
                    // Eğer contentPath bir dosyaysa
                    else if (File.Exists(fullContentPath))
                    {
                        // Eğer dosya Files kökü altındaysa, parent klasörü (ör. Files/{id}) sil
                        if (fullContentPath.StartsWith(filesRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            var parentDir = Path.GetDirectoryName(fullContentPath);
                            if (!string.IsNullOrEmpty(parentDir) && parentDir.StartsWith(filesRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                Directory.Delete(parentDir, true);
                            }
                        }
                        else
                        {
                            // Data veya Images içindeyse sadece dosyayı sil
                            File.Delete(fullContentPath);
                        }
                    }
                    else
                    {
                        // Path mevcut değilse: ör. index'te eski bir entry veya farklı format
                        // filesRoot altında olup olmadığını parent üzerinden kontrol edip sil
                        var parent = Path.GetDirectoryName(fullContentPath);
                        if (!string.IsNullOrEmpty(parent) && parent.StartsWith(filesRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(parent))
                        {
                            Directory.Delete(parent, true);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Path not found or not deletable: {fullContentPath}");
                        }
                    }

                    _storageIndex.Remove(indexItem);
                    await _storageManager.SaveIndexAsync(_storageIndex);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clipboard remove error: {ex.Message}");
                }
            }
        }

        public void ClearHistory()
        {
            ClipboardHistory.Clear();
            _nextId = 1;
        }

        public int GetFavoritesCount()
        {
            return ClipboardHistory.Count(x => x.IsFavorite);
        }

        public IEnumerable<ClipBoard> SearchClipboards(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return ClipboardHistory;

            var lowerQuery = query.ToLower();

            return ClipboardHistory.Where(item =>
                (!string.IsNullOrEmpty(item.PreviewText) && item.PreviewText.ToLower().Contains(lowerQuery)) ||
                (!string.IsNullOrEmpty(item.Content) && item.Content.ToLower().Contains(lowerQuery)) ||
                item.Type.ToString().ToLower().Contains(lowerQuery)
            );
        }
    }
}