using ClipCore.Assets.Functions;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using static System.Net.Mime.MediaTypeNames;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace ClipCore.Assets.Pages
{
    public sealed partial class Homepage : Page
    {
        private ClipBoardManager _clipboardManager;
        private ObservableCollection<ClipBoard> _filteredItems;
        private ClipboardType? _activeTypeFilter = null;
        private bool _showFavoritesOnly = false;
        private string _currentSearchQuery = string.Empty;
        private bool _isGridView = false;
        private bool _isPageLoaded = false; 
        private LocalizationManager? _localizationManager;

        public Homepage()
        {
            this.InitializeComponent();
            _clipboardManager = ClipBoardManager.Instance;
            _localizationManager = LocalizationManager.Instance;
            _filteredItems = new ObservableCollection<ClipBoard>();

            // Subscribe to clipboard changes
            _clipboardManager.ClipboardItemAdded += OnClipboardItemAdded;

            // Subscribe to language changes
            _localizationManager.LanguageChanged += OnLanguageChanged;

            this.Loaded += Homepage_Loaded;
        }

        private async void Homepage_Loaded(object sender, RoutedEventArgs e)
        {
            await _clipboardManager.EnsureStorageLoadedAsync();


            // First Load
            RefreshFilteredItems();
            ClipboardItemsControl.ItemsSource = _filteredItems;
            ClipboardGridControl.ItemsSource = _filteredItems;

            // Stats Update
            UpdateStats();

            // Finished loading
            _isPageLoaded = true;
        }

        ~Homepage()
        {
            if (_clipboardManager != null)
            {
                _clipboardManager.ClipboardItemAdded -= OnClipboardItemAdded;
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateUILanguage();
        }

        // Homepage_Loaded sonrasına ekle
        private void UpdateUILanguage()
        {
            var loc = _localizationManager;

            // Update header
            if (!_showFavoritesOnly)
                homepageTitle.Text = loc?.Get("ClipboardHistory");
            else
                homepageTitle.Text = loc?.Get("Favorites");

            FilterByTypeText.Text = loc?.Get("FilterByType");

            // Update stats
            UpdateStats();

            // Update filter buttons (flyout içindeki textleri güncelle)
            // FilterFlyout'taki TextBlock'lar için manuel update gerekli
        }

        private void OnClipboardItemAdded(object? sender, ClipBoard newItem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshFilteredItems();
                UpdateStats();
            });
        }

        #region Filter Methods

        private void LayoutView_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isPageLoaded)
                return;
            if (sender is RadioButton radioButton)
            {
                _isGridView = radioButton == LayoutGrid;
                SwitchViewMode(_isGridView);
            }
        }
        private void SwitchViewMode(bool isGridView)
        {
            if (isGridView)
            {
                AnimateViewTransition(ClipboardItemsControl, ClipboardGridControl);
            }
            else
            {
                AnimateViewTransition(ClipboardGridControl, ClipboardItemsControl);
            }
        }

        private async void AnimateViewTransition(UIElement hideElement, UIElement showElement)
        {
            var fadeOutStoryboard = new Storyboard();
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };
            Storyboard.SetTarget(fadeOutAnimation, hideElement);
            Storyboard.SetTargetProperty(fadeOutAnimation, "Opacity");
            fadeOutStoryboard.Children.Add(fadeOutAnimation);

            fadeOutStoryboard.Begin();
            await Task.Delay(150);

            // Görünürlüğü değiştir
            hideElement.Visibility = Visibility.Collapsed;
            showElement.Visibility = Visibility.Visible;

            // Fade in animasyonu
            var fadeInStoryboard = new Storyboard();
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };
            Storyboard.SetTarget(fadeInAnimation, showElement);
            Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");
            fadeInStoryboard.Children.Add(fadeInAnimation);

            fadeInStoryboard.Begin();
        }

        private void FilterToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                if (button == FilterAll)
                {
                    _activeTypeFilter = null;
                    UnselectOtherTypeFilters(FilterAll);
                }
                else if (button == FilterText)
                {
                    _activeTypeFilter = ClipboardType.Text;
                    UnselectOtherTypeFilters(FilterText);
                }
                else if (button == FilterImage)
                {
                    _activeTypeFilter = ClipboardType.Image;
                    UnselectOtherTypeFilters(FilterImage);
                }
                else if (button == FilterFiles)
                {
                    _activeTypeFilter = ClipboardType.File;
                    UnselectOtherTypeFilters(FilterFiles);
                }
                else if (button == FilterCode)
                {
                    _activeTypeFilter = ClipboardType.Code;
                    UnselectOtherTypeFilters(FilterCode);
                }
                /*
                else if (button == FilterFavorites)
                {
                    _showFavoritesOnly = button.IsChecked == true;
                }
                */

                if (button.IsChecked == false)
                {
                    _activeTypeFilter = null;
                    UnselectOtherTypeFiltersAll();
                }
                RefreshFilteredItems();
                UpdateStats();
            }
        }

        public static async void ToggleFavoritesFilter(Homepage homepage, bool enable)
        {
            if (homepage == null)
                return;
            homepage._showFavoritesOnly = enable;

            var loc = LocalizationManager.Instance;
            if (!enable)
                homepage.homepageTitle.Text = loc.Get("ClipboardHistory");
            else
                homepage.homepageTitle.Text = loc.Get("Favorites");

            homepage.RefreshFilteredItems();
            homepage.UpdateStats();
        }

        private void UnselectOtherTypeFiltersAll()
        {

            FilterAll.IsChecked = true;
            FilterText.IsChecked = false;
            FilterImage.IsChecked = false;
            FilterFiles.IsChecked = false;
            FilterCode.IsChecked = false;
        }
        private void UnselectOtherTypeFilters(ToggleButton selectedButton)
        {
            if (selectedButton != FilterAll) FilterAll.IsChecked = false;
            if (selectedButton != FilterText) FilterText.IsChecked = false;
            if (selectedButton != FilterImage) FilterImage.IsChecked = false;
            if (selectedButton != FilterFiles) FilterFiles.IsChecked = false;
            if (selectedButton != FilterCode) FilterCode.IsChecked = false;
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e) { }

        #endregion

        #region Search Methods

        public void SearchClipboards(string query)
        {
            _currentSearchQuery = query?.Trim() ?? string.Empty;
            RefreshFilteredItems();
            UpdateStats();
        }

        public void ClearSearch()
        {
            _currentSearchQuery = string.Empty;
            RefreshFilteredItems();
            UpdateStats();
        }

        private bool MatchesSearch(ClipBoard item)
        {
            if (string.IsNullOrWhiteSpace(_currentSearchQuery))
                return true;

            var query = _currentSearchQuery.ToLower();

            if (!string.IsNullOrEmpty(item.PreviewText) &&
                item.PreviewText.ToLower().Contains(query))
                return true;

            if (!string.IsNullOrEmpty(item.Content) &&
                item.Content.ToLower().Contains(query))
                return true;

            if (item.Type.ToString().ToLower().Contains(query))
                return true;

            return false;
        }

        #endregion

        #region Filter and Refresh Logic

        private void RefreshFilteredItems()
        {
            _filteredItems.Clear();

            var items = _clipboardManager.ClipboardHistory.AsEnumerable();

            if (_activeTypeFilter.HasValue)
            {
                items = items.Where(x => x.Type == _activeTypeFilter.Value);
            }

            if (_showFavoritesOnly)
            {
                items = items.Where(x => x.IsFavorite);
            }

            if (!string.IsNullOrWhiteSpace(_currentSearchQuery))
            {
                items = items.Where(x => MatchesSearch(x));
            }

            foreach (var item in items)
            {
                _filteredItems.Add(item);
            }
        }

        private void UpdateStats()
        {
            int totalItems = _filteredItems.Count;
            int favoriteCount = _filteredItems.Count(x => x.IsFavorite);

            var loc = _localizationManager;
            StatsText.Text = $"{totalItems} {loc?.Get("Items")} · {favoriteCount} {loc?.Get("FavoritesCount")}";
        }

        #endregion

        #region Action Handlers
        private async void ThumbnailImage_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Image image && image.DataContext is ClipBoard item)
            {
                // Eğer zaten yüklenmişse skip et
                if (image.Source != null)
                    return;

                var thumbnail = await LoadImageThumbnailAsync(item);
                if (thumbnail != null)
                {
                    image.Source = thumbnail;
                }
            }
        }

        private async void ClipboardCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var originalSource = e.OriginalSource as FrameworkElement;

            while (originalSource != null)
            {
                if (originalSource is Button)
                {
                    return;
                }
                originalSource = VisualTreeHelper.GetParent(originalSource) as FrameworkElement;
            }

            if (sender is Border border)
            {
                var item = FindClipboardItem(border);
                if (item != null)
                {
                    var copiedBool = await ClipboardPreviewDialog.ShowPreviewAsync(item, this.XamlRoot);
                    if (copiedBool)
                    {
                        await ShowCopyNotification();
                    }
                }
            }
        }

        private ClipBoard? FindClipboardItem(DependencyObject element)
        {
            while (element != null)
            {
                if (element is FrameworkElement fe && fe.DataContext is ClipBoard item)
                {
                    return item;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }
        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ClipBoard item)
            {

                _clipboardManager.ToggleFavorite(item);

                if (_showFavoritesOnly && !item.IsFavorite)
                {
                    RefreshFilteredItems();
                }

                UpdateStats();

                ClipboardItemsControl.ItemsSource = null;
                ClipboardItemsControl.ItemsSource = _filteredItems;
            }
        }
        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ClipBoard item)
            {
                //await ClipboardPreviewDialog.ShowPreviewAsync(item, this.XamlRoot);

                ClipboardPreviewDialog.CopyToClipboard(item);
                await ShowCopyNotification();
            }
        }

        private async Task ShowCopyNotification()
        {
            if (CopyNotificationBorder.Visibility == Visibility.Visible)
                return;

            CopyNotificationBorder.Visibility = Visibility.Visible;
            CopyNotificationBorder.Opacity = 0;

            var fadeIn = new Storyboard();
            var fadeInAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeInAnim, CopyNotificationBorder);
            Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            fadeIn.Children.Add(fadeInAnim);
            fadeIn.Begin();

            await Task.Delay(200);

            await Task.Delay(2000);

            var fadeOut = new Storyboard();
            var fadeOutAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOutAnim, CopyNotificationBorder);
            Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
            fadeOut.Children.Add(fadeOutAnim);
            fadeOut.Completed += (s, e) => { CopyNotificationBorder.Visibility = Visibility.Collapsed; };
            fadeOut.Begin();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ClipBoard item)
            {

                _clipboardManager.RemoveClipboard(item);
                _filteredItems.Remove(item);
                UpdateStats();
            }
        }

        #endregion

        #region Public Helper Methods

        public void ResetFilters()
        {
            FilterAll.IsChecked = true;
            FilterText.IsChecked = false;
            FilterImage.IsChecked = false;
            FilterCode.IsChecked = false;
            //FilterFavorites.IsChecked = false;

            _activeTypeFilter = null;
            _showFavoritesOnly = false;
            _currentSearchQuery = string.Empty;

            RefreshFilteredItems();
            UpdateStats();
        }

        public void RefreshList()
        {
            RefreshFilteredItems();
            UpdateStats();
        }

        #endregion

        #region Image Thumbnail Methods


        private Dictionary<int, BitmapImage> _thumbnailCache = new Dictionary<int, BitmapImage>();
        private async Task<BitmapImage?> LoadImageThumbnailAsync(ClipBoard item)
        {
            try
            {
                // Cache kontrolü
                if (_thumbnailCache.TryGetValue(item.Id, out var cachedImage))
                    return cachedImage;

                // Lazy load içeriği
                if (item.Content == "[Lazy Load]" || string.IsNullOrEmpty(item.Content))
                {
                    await _clipboardManager.LoadContentAsync(item);
                }

                if (string.IsNullOrEmpty(item.Content))
                    return null;

                byte[] imageBytes = Convert.FromBase64String(item.Content);

                var bitmapImage = new BitmapImage();

                // Thumbnail boyutunda yükle (performans için)
                bitmapImage.DecodePixelWidth = 120; // Maksimum genişlik
                bitmapImage.DecodePixelHeight = 120; // Maksimum yükseklik

                using (var stream = new InMemoryRandomAccessStream())
                {
                    var writer = new Windows.Storage.Streams.DataWriter(stream);
                    writer.WriteBytes(imageBytes);
                    await writer.StoreAsync();
                    writer.DetachStream();
                    stream.Seek(0);

                    await bitmapImage.SetSourceAsync(stream);
                }

                // Cache'e ekle (max 50 resim)
                if (_thumbnailCache.Count > 50)
                {
                    var oldestKey = _thumbnailCache.Keys.First();
                    _thumbnailCache.Remove(oldestKey);
                }
                _thumbnailCache[item.Id] = bitmapImage;

                return bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail load error: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
