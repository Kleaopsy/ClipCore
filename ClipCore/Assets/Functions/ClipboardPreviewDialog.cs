using ClipCore.Assets.Pages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ClipCore.Assets.Functions
{
    public class ClipboardPreviewDialog
    {
        private static readonly SemaphoreSlim _dialogSemaphore = new SemaphoreSlim(1, 1);
        static private double _maxWidth = 800;
        static private double _maxHeight = 800;

        public static void resizedDialog(double x, double y) {
            if (x >= 600)
                _maxWidth = 800;
            else
                 _maxWidth = x - 26;

            if (y >= 600)
                _maxHeight = 800;
            else
                _maxHeight = 400;
        }

        public static async Task<bool> ShowPreviewAsync(ClipBoard item, XamlRoot xamlRoot)
        {
            if (!await _dialogSemaphore.WaitAsync(0)) {
                return false;
            }
            try
            {
                if (ClipCoreWindow.Current != null)
                {
                    var size = ClipCoreWindow.Current.AppWindow.Size;

                    if (size.Height < 600 && size.Width < 600)
                    {
                        _maxWidth = size.Width - 26;
                        _maxHeight = 400;
                    }
                }

                var dialog = new ContentDialog
                {
                    Title = GetDialogTitle(item),
                    CloseButtonText = "Close",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = xamlRoot,
                    MaxWidth = _maxWidth,
                    MaxHeight = _maxHeight
                };


                bool copyClicked = false;

                dialog.PrimaryButtonText = "Copy";
                dialog.PrimaryButtonClick += async (s, args) =>
                {
                    CopyToClipboard(item);
                    copyClicked = true;
                };

                dialog.Content = await CreatePreviewContentAsync(item);
                dialog.CloseButtonText = LocalizationManager.Instance.Get("Close");
                dialog.PrimaryButtonText = LocalizationManager.Instance.Get("Copy");

                CenterDialog(dialog);

                await dialog.ShowAsync();
                return copyClicked;
            }
            finally
            {
                _dialogSemaphore.Release();
            }
        }

        private static void CenterDialog(ContentDialog dialog)
        {
            if (ClipCoreWindow.Current != null)
            {
                var size = ClipCoreWindow.Current.AppWindow.Size;

                double verticalMargin = Math.Max(50, (size.Height - dialog.MaxHeight) / 2);

                if (dialog.MaxWidth < size.Width - 100)
                {
                    double horizontalMargin = Math.Max(50, (size.Width - dialog.MaxWidth) / 2);
                    dialog.Margin = new Thickness(horizontalMargin, verticalMargin, horizontalMargin, verticalMargin);
                }
                else
                {
                    dialog.Margin = new Thickness(5, verticalMargin, 5, verticalMargin);
                }
            }
        }

        private static string GetDialogTitle(ClipBoard item)
        {
            var loc = LocalizationManager.Instance;

            return item.Type switch
            {
                ClipboardType.Text => loc.Get("TextPreview"),
                ClipboardType.Code => loc.Get("CodePreview"),
                ClipboardType.Image => loc.Get("ImagePreview"),
                ClipboardType.File => loc.Get("FilePreview"),
                ClipboardType.Html => loc.Get("HTMLPreview"),
                _ => loc.Get("Preview")
            };
        }

        private static async Task<UIElement> CreatePreviewContentAsync(ClipBoard item)
        {
            // Lazy load içeriği - HER ZAMAN
            if (item.Content == "[Lazy Load]" || string.IsNullOrEmpty(item.Content))
            {
                await ClipBoardManager.Instance.LoadContentAsync(item);
            }

            // Eğer hala boşsa
            if (string.IsNullOrEmpty(item.Content))
            {
                return CreateErrorPreview("Content could not be loaded");
            }

            switch (item.Type)
            {
                case ClipboardType.Text:
                    return CreateTextPreview(item);

                case ClipboardType.Code:
                    return CreateCodePreview(item);

                case ClipboardType.Image:
                    return await CreateImagePreviewAsync(item);

                case ClipboardType.Html:
                    return CreateHtmlPreview(item);

                case ClipboardType.File:
                    return CreateFilePreview(item);

                default:
                    return CreateTextPreview(item);
            }
        }

        private static UIElement CreateErrorPreview(string message)
        {
            var loc = LocalizationManager.Instance;

            var border = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray) { Opacity = 0.3 },
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(32),
                MinHeight = 200
            };

            var text = new TextBlock
            {
                Text = loc.Get("ContentNotLoaded"),
                FontSize = 14,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            border.Child = text;
            return border;
        }
        private static UIElement CreateTextPreview(ClipBoard item)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = _maxHeight,
                MaxWidth = _maxWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var textBlock = new TextBlock
            {
                Text = item.Content,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                IsTextSelectionEnabled = true,
                Padding = new Thickness(16)
            };

            scrollViewer.Content = textBlock;
            return scrollViewer;
        }

        private static UIElement CreateCodePreview(ClipBoard item)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = _maxHeight,
                MaxWidth = _maxWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.05 },
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(8)
            };

            var textBlock = new TextBlock
            {
                Text = item.Content,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.NoWrap
            };

            border.Child = textBlock;
            scrollViewer.Content = border;
            return scrollViewer;
        }

        private static async Task<UIElement> CreateImagePreviewAsync(ClipBoard item)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = _maxHeight,
                MaxWidth = _maxWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var stackPanel = new StackPanel
            {
                Spacing = 12,
                Padding = new Thickness(16)
            };

            var infoText = new TextBlock
            {
                Text = item.PreviewText,
                FontSize = 13,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(infoText);

            try
            {
                if (!string.IsNullOrEmpty(item.Content) && item.Content != "[Image Data]")
                {
                    byte[] imageBytes = Convert.FromBase64String(item.Content);

                    var image = new Image
                    {
                        MaxWidth = _maxHeight - 100,
                        MaxHeight = _maxHeight - 100,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    var bitmapImage = new BitmapImage();
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        var writer = new Windows.Storage.Streams.DataWriter(stream);
                        writer.WriteBytes(imageBytes);
                        await writer.StoreAsync();
                        writer.DetachStream();
                        stream.Seek(0);

                        await bitmapImage.SetSourceAsync(stream);
                    }

                    image.Source = bitmapImage;
                    stackPanel.Children.Add(image);
                }
                else
                {
                    AddImagePlaceholder(stackPanel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image preview error: {ex.Message}");
                AddImagePlaceholder(stackPanel);
            }

            scrollViewer.Content = stackPanel;
            return scrollViewer;
        }

        private static void AddImagePlaceholder(StackPanel stackPanel)
        {
            var imageBorder = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray) { Opacity = 0.3 },
                CornerRadius = new CornerRadius(8),
                Width = _maxHeight - 200,
                Height = _maxHeight - 200,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var placeholderStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8
            };

            var icon = new FontIcon
            {
                Glyph = "\uEB9F",
                FontSize = 48,
                Opacity = 0.5
            };

            var text = new TextBlock
            {
                Text = "Image Preview",
                FontSize = 16,
                Opacity = 0.5,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            placeholderStack.Children.Add(icon);
            placeholderStack.Children.Add(text);
            imageBorder.Child = placeholderStack;
            stackPanel.Children.Add(imageBorder);
        }

        private static UIElement CreateHtmlPreview(ClipBoard item)
        {
            var tabView = new TabView();

            // Raw HTML Tab
            var rawTab = new TabViewItem
            {
                Header = "Raw HTML"
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = _maxHeight,
                MaxWidth = _maxWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.05 },
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(8)
            };

            var textBlock = new TextBlock
            {
                Text = item.Content,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            scrollViewer.Content = border;
            rawTab.Content = scrollViewer;

            // Rendered Tab (placeholder)
            var renderedTab = new TabViewItem
            {
                Header = "Rendered (Preview)"
            };

            var renderedBorder = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray) { Opacity = 0.3 },
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(32),
                Margin = new Thickness(8),
                Height = 450
            };

            var placeholderText = new TextBlock
            {
                Text = "HTML rendering will be available in future updates",
                FontSize = 14,
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            renderedBorder.Child = placeholderText;
            renderedTab.Content = renderedBorder;

            tabView.TabItems.Add(rawTab);
            tabView.TabItems.Add(renderedTab);
            tabView.SelectedIndex = 0;

            return tabView;
        }

        private static UIElement CreateFilePreview(ClipBoard item)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = _maxHeight,
                MaxWidth = _maxWidth
            };

            var stackPanel = new StackPanel
            {
                Spacing = 16,
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Dosya yollarını ayır
            var filePaths = item.Content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            bool multipleFiles = filePaths.Length > 1;

            if (multipleFiles)
            {
                // Çoklu dosya görünümü
                var headerText = new TextBlock
                {
                    Text = $"{filePaths.Length} Files",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                stackPanel.Children.Add(headerText);

                foreach (var path in filePaths.Take(5)) // İlk 5 dosyayı göster
                {
                    var fileCard = CreateFileCard(path);
                    stackPanel.Children.Add(fileCard);
                }

                if (filePaths.Length > 5)
                {
                    var moreText = new TextBlock
                    {
                        Text = $"+ {filePaths.Length - 5} more files",
                        FontSize = 13,
                        Opacity = 0.6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 0)
                    };
                    stackPanel.Children.Add(moreText);
                }
            }
            else
            {
                // Tek dosya görünümü
                var iconBorder = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.LightBlue) { Opacity = 0.2 },
                    Width = 120,
                    Height = 120,
                    CornerRadius = new CornerRadius(12),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var fileIcon = new FontIcon
                {
                    Glyph = GetFileIcon(filePaths[0]),
                    FontSize = 64,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                iconBorder.Child = fileIcon;
                stackPanel.Children.Add(iconBorder);

                var fileName = new TextBlock
                {
                    Text = System.IO.Path.GetFileName(filePaths[0]),
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 400
                };
                stackPanel.Children.Add(fileName);

                var filePath = new TextBlock
                {
                    Text = filePaths[0],
                    FontSize = 12,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 500,
                    IsTextSelectionEnabled = true
                };
                stackPanel.Children.Add(filePath);
            }

            scrollViewer.Content = stackPanel;
            return scrollViewer;
        }

        private static Border CreateFileCard(string filePath)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.1 },
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 4),
                MinWidth = 400
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new FontIcon
            {
                Glyph = GetFileIcon(filePath),
                FontSize = 32,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameText = new TextBlock
            {
                Text = System.IO.Path.GetFileName(filePath),
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var pathText = new TextBlock
            {
                Text = filePath,
                FontSize = 11,
                Opacity = 0.6,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            textStack.Children.Add(nameText);
            textStack.Children.Add(pathText);
            Grid.SetColumn(textStack, 1);

            grid.Children.Add(icon);
            grid.Children.Add(textStack);
            card.Child = grid;

            return card;
        }

        public static string GetFileIcon(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".txt" or ".log" => "\uE8A5", // Document
                ".pdf" => "\uEA90", // PDF
                ".doc" or ".docx" => "\uF6F9", // Word
                ".xls" or ".xlsx" => "\uF71A", // Excel
                ".ppt" or ".pptx" => "\uF72E", // PowerPoint
                ".zip" or ".rar" or ".7z" => "\uF012", // Archive
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "\uEB9F", // Image
                ".mp4" or ".avi" or ".mkv" or ".mov" => "\uE8B2", // Video
                ".mp3" or ".wav" or ".flac" => "\uE8D6", // Audio
                ".exe" or ".msi" => "\uE756", // App
                ".cs" or ".cpp" or ".h" or ".py" or ".js" or ".html" or ".css" => "\uE943", // Code
                _ => "\uE8B7" // Generic file
            };
        }

        public static async void CopyToClipboard(ClipBoard item)
        {
            try
            {
                if (item.Content == "[Lazy Load]")
                    await ClipBoardManager.Instance.LoadContentAsync(item);

                var dataPackage = new DataPackage();

                if (item.Type == ClipboardType.Image && !string.IsNullOrEmpty(item.Content) && item.Content != "[Image Data]")
                {
                    byte[] imageBytes = Convert.FromBase64String(item.Content);

                    var stream = new InMemoryRandomAccessStream();

                    var writer = new DataWriter(stream.GetOutputStreamAt(0));
                    writer.WriteBytes(imageBytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();

                    stream.Seek(0);

                    var streamRef = RandomAccessStreamReference.CreateFromStream(stream);
                    dataPackage.SetBitmap(streamRef);
                }
                else if (item.Type == ClipboardType.File)
                {
                    var storageManager = new ClipboardStorageManager();
                    var indexItem =  ClipBoardManager.Instance._storageIndex.FirstOrDefault(x => x.Id == item.Id);
                    if (indexItem != null) { 
                    // Dosya yollarını ayır
                    var filePaths = item.Content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    var storageItems = new List<IStorageItem>();

                    foreach (var path in filePaths)
                    {
                        try
                        {
                            if (System.IO.File.Exists(path))
                            {
                                var file = await StorageFile.GetFileFromPathAsync(path);
                                storageItems.Add(file);
                            }
                            else if (System.IO.Directory.Exists(path))
                            {
                                var folder = await StorageFolder.GetFolderFromPathAsync(path);
                                storageItems.Add(folder);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"File access error for {path}: {ex.Message}");
                        }
                    }

                    if (storageItems.Count > 0)
                    {
                        dataPackage.SetStorageItems(storageItems);
                    }
                }
                }
                else
                {
                    dataPackage.SetText(item.Content);
                }

                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                if (dispatcherQueue.HasThreadAccess)
                {
                    Clipboard.SetContent(dataPackage);
                    Clipboard.Flush();
                }
                else
                {
                    await Task.Run(() =>
                    {
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            Clipboard.SetContent(dataPackage);
                            Clipboard.Flush();
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
            }
        }
    }
}