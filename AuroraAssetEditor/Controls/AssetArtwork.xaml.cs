using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuroraAssetEditor.Classes;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using AuroraAssetEditor;
using AuroraAssetEditor.Controls;

namespace AuroraAssetEditor.Controls
{
    public partial class AssetArtwork : UserControl
    {
        public static readonly DependencyProperty ImageSpacingProperty =
            DependencyProperty.Register("ImageSpacing", typeof(Thickness), typeof(AssetArtwork),
                new PropertyMetadata(new Thickness(5)));

        public static readonly DependencyProperty BannerSourceProperty =
            DependencyProperty.Register("BannerSource", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty IconSourceProperty =
            DependencyProperty.Register("IconSource", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty BoxartSourceProperty =
            DependencyProperty.Register("BoxartSource", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty BackgroundSourceProperty =
            DependencyProperty.Register("BackgroundSource", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Screenshot1SourceProperty =
            DependencyProperty.Register("Screenshot1Source", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Screenshot2SourceProperty =
            DependencyProperty.Register("Screenshot2Source", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Screenshot3SourceProperty =
            DependencyProperty.Register("Screenshot3Source", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Screenshot4SourceProperty =
            DependencyProperty.Register("Screenshot4Source", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty Screenshot5SourceProperty =
            DependencyProperty.Register("Screenshot5Source", typeof(ImageSource), typeof(AssetArtwork),
                new PropertyMetadata(null, OnImageSourceChanged));

        private readonly Dictionary<ImageType, List<string>> _imagePaths = new Dictionary<ImageType, List<string>>();
        private readonly Dictionary<ImageType, int> _currentIndices = new Dictionary<ImageType, int>();

        public Thickness ImageSpacing
        {
            get => (Thickness)GetValue(ImageSpacingProperty);
            set => SetValue(ImageSpacingProperty, value);
        }

        public ImageSource BannerSource
        {
            get => (ImageSource)GetValue(BannerSourceProperty);
            set => SetValue(BannerSourceProperty, value);
        }

        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        public ImageSource BoxartSource
        {
            get => (ImageSource)GetValue(BoxartSourceProperty);
            set => SetValue(BoxartSourceProperty, value);
        }

        public ImageSource BackgroundSource
        {
            get => (ImageSource)GetValue(BackgroundSourceProperty);
            set => SetValue(BackgroundSourceProperty, value);
        }

        public ImageSource Screenshot1Source
        {
            get => (ImageSource)GetValue(Screenshot1SourceProperty);
            set => SetValue(Screenshot1SourceProperty, value);
        }

        public ImageSource Screenshot2Source
        {
            get => (ImageSource)GetValue(Screenshot2SourceProperty);
            set => SetValue(Screenshot2SourceProperty, value);
        }

        public ImageSource Screenshot3Source
        {
            get => (ImageSource)GetValue(Screenshot3SourceProperty);
            set => SetValue(Screenshot3SourceProperty, value);
        }

        public ImageSource Screenshot4Source
        {
            get => (ImageSource)GetValue(Screenshot4SourceProperty);
            set => SetValue(Screenshot4SourceProperty, value);
        }

        public ImageSource Screenshot5Source
        {
            get => (ImageSource)GetValue(Screenshot5SourceProperty);
            set => SetValue(Screenshot5SourceProperty, value);
        }

        public enum ImageType
        {
            Banner,
            Icon,
            Boxart,
            Background,
            Screenshot1,
            Screenshot2,
            Screenshot3,
            Screenshot4,
            Screenshot5
        }

        public enum NavigationStyleType
        {
            Default,    // Current style with rounded black buttons
            Minimal,    // Small transparent buttons with just arrows
            Modern      // Flat buttons with text and hover effects
        }

        public static readonly DependencyProperty NavigationStyleProperty =
            DependencyProperty.Register("NavigationStyle", typeof(NavigationStyleType), typeof(AssetArtwork),
                new PropertyMetadata(NavigationStyleType.Default, OnNavigationStyleChanged));

        public NavigationStyleType NavigationStyle
        {
            get => (NavigationStyleType)GetValue(NavigationStyleProperty);
            set => SetValue(NavigationStyleProperty, value);
        }

        private static void OnNavigationStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (AssetArtwork)d;
            var newStyle = (NavigationStyleType)e.NewValue;

            // Update all navigation buttons to use the new style
            foreach (var type in Enum.GetValues(typeof(ImageType)))
            {
                var navigation = control.FindName($"{type}Navigation") as StackPanel;
                if (navigation == null) continue;

                string resourceKey;
                if (newStyle == NavigationStyleType.Minimal)
                    resourceKey = "MinimalNavigationButtonStyle";
                else if (newStyle == NavigationStyleType.Modern)
                    resourceKey = "ModernNavigationButtonStyle";
                else
                    resourceKey = "NavigationButtonStyle";

                string textResourceKey;
                if (newStyle == NavigationStyleType.Minimal)
                    textResourceKey = "MinimalNavigationTextStyle";
                else if (newStyle == NavigationStyleType.Modern)
                    textResourceKey = "ModernNavigationTextStyle";
                else
                    textResourceKey = "NavigationTextStyle";

                foreach (var child in navigation.Children)
                {
                    if (child is Button button)
                    {
                        button.Style = control.FindResource(resourceKey) as Style;
                    }
                    else if (child is TextBlock textBlock)
                    {
                        textBlock.Style = control.FindResource(textResourceKey) as Style;
                    }
                }
            }
        }

        public AssetArtwork()
        {
            InitializeComponent();
            
            // Enable drag and drop
            MainGrid.AllowDrop = true;
            MainGrid.DragEnter += OnDragEnter;
            MainGrid.Drop += OnDrop;

            // Initialize image collections
            foreach (ImageType type in Enum.GetValues(typeof(ImageType)))
            {
                if (type < ImageType.Screenshot1) // Only for non-screenshot types
                {
                    _imagePaths[type] = new List<string>();
                    _currentIndices[type] = 0;
                }
            }
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (AssetArtwork)d;
            var propertyName = e.Property.Name;
            var imageName = propertyName.Replace("Source", "Image");
            var image = control.FindName(imageName) as Image;
            if (image != null)
            {
                image.Source = e.NewValue as ImageSource;
            }
        }

        private void OnPreviousImage(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            var type = (ImageType)Enum.Parse(typeof(ImageType), button.Tag.ToString());
            NavigateImage(type, -1);
        }

        private void OnNextImage(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            var type = (ImageType)Enum.Parse(typeof(ImageType), button.Tag.ToString());
            NavigateImage(type, 1);
        }

        private void NavigateImage(ImageType type, int direction)
        {
            if (!_imagePaths.ContainsKey(type) || _imagePaths[type].Count <= 1) return;

            var newIndex = _currentIndices[type] + direction;
            if (newIndex < 0) newIndex = _imagePaths[type].Count - 1;
            if (newIndex >= _imagePaths[type].Count) newIndex = 0;

            _currentIndices[type] = newIndex;
            LoadImageAtCurrentIndex(type);
            UpdateCounter(type);
        }

        private void LoadImageAtCurrentIndex(ImageType type)
        {
            if (!_imagePaths.ContainsKey(type) || !_imagePaths[type].Any()) return;

            var path = _imagePaths[type][_currentIndices[type]];
            SetDirectImage(path, type);
        }

        private void UpdateCounter(ImageType type)
        {
            var counter = FindName($"{type}Counter") as TextBlock;
            var navigation = FindName($"{type}Navigation") as StackPanel;
            
            if (counter != null && navigation != null)
            {
                if (_imagePaths[type].Count > 1)
                {
                    counter.Text = $"{_currentIndices[type] + 1}/{_imagePaths[type].Count}";
                    navigation.Visibility = Visibility.Visible;
                }
                else
                {
                    navigation.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void AddImage(string path, ImageType type)
        {
            if (type >= ImageType.Screenshot1) return; // Don't handle multiple screenshots

            if (!_imagePaths.ContainsKey(type))
            {
                _imagePaths[type] = new List<string>();
                _currentIndices[type] = 0;
            }

            if (!_imagePaths[type].Contains(path))
            {
                _imagePaths[type].Add(path);
                // Always show the first image when adding new ones
                _currentIndices[type] = 0;
                LoadImageAtCurrentIndex(type);
                UpdateCounter(type);
            }
        }

        public void ClearAll()
        {
            foreach (var type in _imagePaths.Keys.ToList())
            {
                _imagePaths[type].Clear();
                _currentIndices[type] = 0;
                UpdateCounter(type);
            }

            BannerSource = null;
            IconSource = null;
            BoxartSource = null;
            BackgroundSource = null;
            Screenshot1Source = null;
            Screenshot2Source = null;
            Screenshot3Source = null;
            Screenshot4Source = null;
            Screenshot5Source = null;
        }

        public void SetDirectImage(string imagePath, ImageType type)
        {
            try
            {
                if (!File.Exists(imagePath)) return;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath);
                bitmap.EndInit();
                bitmap.Freeze(); // Make it thread-safe

                // Update UI on the UI thread
                Dispatcher.Invoke(() =>
                {
                    switch (type)
                    {
                        case ImageType.Banner:
                            BannerSource = bitmap;
                            break;
                        case ImageType.Icon:
                            IconSource = bitmap;
                            break;
                        case ImageType.Boxart:
                            BoxartSource = bitmap;
                            break;
                        case ImageType.Background:
                            BackgroundSource = bitmap;
                            break;
                        case ImageType.Screenshot1:
                            Screenshot1Source = bitmap;
                            break;
                        case ImageType.Screenshot2:
                            Screenshot2Source = bitmap;
                            break;
                        case ImageType.Screenshot3:
                            Screenshot3Source = bitmap;
                            break;
                        case ImageType.Screenshot4:
                            Screenshot4Source = bitmap;
                            break;
                        case ImageType.Screenshot5:
                            Screenshot5Source = bitmap;
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
        }

        public void SetAssetImage(string assetPath, ImageType type)
        {
            try
            {
                if (!File.Exists(assetPath)) return;

                var assetBytes = File.ReadAllBytes(assetPath);
                var asset = new AuroraAsset.AssetFile(assetBytes);

                System.Drawing.Image image = null;
                switch (type)
                {
                    case ImageType.Banner:
                        image = asset.HasIconBanner ? asset.GetBanner() : null;
                        break;
                    case ImageType.Icon:
                        image = asset.HasIconBanner ? asset.GetIcon() : null;
                        break;
                    case ImageType.Boxart:
                        image = asset.HasBoxArt ? asset.GetBoxart() : null;
                        break;
                    case ImageType.Background:
                        image = asset.HasBackground ? asset.GetBackground() : null;
                        break;
                    case ImageType.Screenshot1:
                    case ImageType.Screenshot2:
                    case ImageType.Screenshot3:
                    case ImageType.Screenshot4:
                    case ImageType.Screenshot5:
                        if (asset.HasScreenshots)
                        {
                            var screenshots = asset.GetScreenshots();
                            var index = (int)type - (int)ImageType.Screenshot1;
                            if (screenshots.Length > index)
                            {
                                image = screenshots[index];
                            }
                        }
                        break;
                }

                if (image != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze(); // Make it thread-safe

                        // Update UI on the UI thread
                        Dispatcher.Invoke(() =>
                        {
                            switch (type)
                            {
                                case ImageType.Banner:
                                    BannerSource = bitmap;
                                    break;
                                case ImageType.Icon:
                                    IconSource = bitmap;
                                    break;
                                case ImageType.Boxart:
                                    BoxartSource = bitmap;
                                    break;
                                case ImageType.Background:
                                    BackgroundSource = bitmap;
                                    break;
                                case ImageType.Screenshot1:
                                    Screenshot1Source = bitmap;
                                    break;
                                case ImageType.Screenshot2:
                                    Screenshot2Source = bitmap;
                                    break;
                                case ImageType.Screenshot3:
                                    Screenshot3Source = bitmap;
                                    break;
                                case ImageType.Screenshot4:
                                    Screenshot4Source = bitmap;
                                    break;
                                case ImageType.Screenshot5:
                                    Screenshot5Source = bitmap;
                                    break;
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
        }

        public void SetImage(string path, ImageType type)
        {
            if (string.IsNullOrEmpty(path)) return;

            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            if (extension == ".asset")
            {
                SetAssetImage(path, type);
            }
            else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
            {
                if (type < ImageType.Screenshot1) // For non-screenshot types
                {
                    AddImage(path, type);
                }
                else // For screenshots, just set directly
                {
                    SetDirectImage(path, type);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                var loadingOverlay = FindName("LoadingOverlay") as Border;
                
                // Show loading overlay on UI thread immediately
                Dispatcher.Invoke(() => 
                {
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Visibility = Visibility.Visible;
                    }
                });

                // Process files on background thread
                Task.Run(() =>
                {
                    try 
                    {
                        ProcessDroppedFiles(paths);
                    }
                    finally 
                    {
                        // Hide overlay on UI thread when done
                        Dispatcher.Invoke(() => 
                        {
                            if (loadingOverlay != null)
                            {
                                loadingOverlay.Visibility = Visibility.Collapsed;
                            }
                        });
                    }
                });
            }
        }

        private void ProcessDroppedFiles(string[] paths)
        {
            // Group files by type to handle multiple files of the same type
            var filesByType = new Dictionary<ImageType?, List<string>>();

            // Process all paths (files and folders)
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    // If it's a directory, search recursively for image files
                    var imageFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f => {
                            var ext = Path.GetExtension(f).ToLowerInvariant();
                            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".asset";
                        });

                    foreach (var file in imageFiles)
                    {
                        ProcessSingleFile(file, filesByType);
                    }
                }
                else if (File.Exists(path))
                {
                    // If it's a file, process it directly
                    ProcessSingleFile(path, filesByType);
                }
            }

            // Now process each type - but update UI elements on the UI thread
            Dispatcher.Invoke(() =>
            {
                foreach (var typeGroup in filesByType)
                {
                    var type = typeGroup.Key.Value;
                    var typeFiles = typeGroup.Value;

                    // Handle screenshots differently - only take the last file for each screenshot position
                    if (type >= ImageType.Screenshot1)
                    {
                        // Clear the specific screenshot
                        switch (type)
                        {
                            case ImageType.Screenshot1:
                                Screenshot1Source = null;
                                break;
                            case ImageType.Screenshot2:
                                Screenshot2Source = null;
                                break;
                            case ImageType.Screenshot3:
                                Screenshot3Source = null;
                                break;
                            case ImageType.Screenshot4:
                                Screenshot4Source = null;
                                break;
                            case ImageType.Screenshot5:
                                Screenshot5Source = null;
                                break;
                        }

                        // Take only the last file for this screenshot position
                        if (typeFiles.Any())
                        {
                            var file = typeFiles.Last();
                            SetImage(file, type);
                        }
                    }
                    else // Handle non-screenshot types
                    {
                        _imagePaths[type].Clear();
                        _currentIndices[type] = 0;
                        
                        // Clear the image source
                        switch (type)
                        {
                            case ImageType.Banner:
                                BannerSource = null;
                                break;
                            case ImageType.Icon:
                                IconSource = null;
                                break;
                            case ImageType.Boxart:
                                BoxartSource = null;
                                break;
                            case ImageType.Background:
                                BackgroundSource = null;
                                break;
                        }

                        // Sort files to ensure consistent ordering
                        typeFiles.Sort();

                        // Process the new files
                        foreach (var file in typeFiles)
                        {
                            SetImage(file, type);
                        }

                        // Ensure we're showing the first image
                        if (_imagePaths[type].Any())
                        {
                            _currentIndices[type] = 0;
                            LoadImageAtCurrentIndex(type);
                            UpdateCounter(type);
                        }
                    }
                }
            });
        }

        private void ProcessSingleFile(string file, Dictionary<ImageType?, List<string>> filesByType)
        {
            var extension = Path.GetExtension(file)?.ToLowerInvariant();
            if (extension == ".asset" || extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
            {
                var filename = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                var type = DetermineImageType(filename);
                if (type.HasValue)
                {
                    if (!filesByType.ContainsKey(type))
                    {
                        filesByType[type] = new List<string>();
                    }
                    filesByType[type].Add(file);
                }
            }
        }

        private ImageType? DetermineImageType(string filename)
        {
            // Screenshot patterns - strict matching for exact screenshot1-5 files
            if (Regex.IsMatch(filename, @"^screenshot[_-]?[1-5]$", RegexOptions.IgnoreCase))
            {
                var number = filename[filename.Length - 1] - '0';
                return (ImageType)(ImageType.Screenshot1 + (number - 1));
            }

            // Common naming patterns for other types
            var patterns = new[]
            {
                // Banner patterns
                new { Pattern = @"(^|[^a-z])banner([^a-z]|$)", Type = ImageType.Banner },
                new { Pattern = @"^gl", Type = ImageType.Banner },
                
                // Icon patterns
                new { Pattern = @"(^|[^a-z])icon([^a-z]|$)", Type = ImageType.Icon },
                
                // Boxart patterns
                new { Pattern = @"(^|[^a-z])(boxart|cover)([^a-z]|$)", Type = ImageType.Boxart },
                new { Pattern = @"^gc", Type = ImageType.Boxart },
                
                // Background patterns
                new { Pattern = @"(^|[^a-z])(background|bg)([^a-z]|$)", Type = ImageType.Background },
                new { Pattern = @"^bk", Type = ImageType.Background }
            };

            // Try to match each pattern
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(filename, pattern.Pattern, RegexOptions.IgnoreCase))
                {
                    return pattern.Type;
                }
            }

            return null;
        }

        private DateTime _lastClick = DateTime.MinValue;
        private Border _lastClickedBorder = null;

        private bool IsAssetThumbnail(ImageSource source)
        {
            if (source is BitmapImage bitmapImage && bitmapImage.UriSource != null)
            {
                string uriString = bitmapImage.UriSource.ToString();
                return uriString.Contains("thumbs/local/") || uriString.Contains("thumbs\\local\\");
            }
            return false;
        }

        private ImageSource GetFullSizeAssetImage(string thumbPath, string imageType)
        {
            try
            {
                // Convert relative path to absolute if needed
                if (!Path.IsPathRooted(thumbPath))
                {
                    thumbPath = Path.GetFullPath(thumbPath);
                }

                // Extract title ID and asset type from thumb path (format: thumbs/local/[titleId]/[type]-[hash].png)
                var pathParts = thumbPath.Split(new[] { Path.DirectorySeparatorChar, '/' }, StringSplitOptions.RemoveEmptyEntries);
                var titleIdIndex = Array.IndexOf(pathParts, "local") + 1;
                if (titleIdIndex <= 0 || titleIdIndex >= pathParts.Length) return null;
                
                var titleId = pathParts[titleIdIndex];
                var fileNameParts = Path.GetFileNameWithoutExtension(pathParts.Last()).Split('-');
                if (fileNameParts.Length < 2) return null;
                
                var hash = fileNameParts[1];

                // Find the original asset file in the game folder
                var gameFolder = Path.Combine(
                    Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thumbPath))), // Go up 3 levels from thumbs/local/[titleId]
                    titleId);

                if (!Directory.Exists(gameFolder)) return null;

                string assetPattern = "";
                switch (imageType)
                {
                    case "Banner":
                    case "Icon":
                        assetPattern = "GL*.asset";
                        break;
                    case "Boxart":
                        assetPattern = "GC*.asset";
                        break;
                    case "Background":
                        assetPattern = "BK*.asset";
                        break;
                    case "Screenshot1":
                    case "Screenshot2":
                    case "Screenshot3":
                    case "Screenshot4":
                    case "Screenshot5":
                        assetPattern = "SS*.asset";
                        break;
                }

                var assetFiles = Directory.GetFiles(gameFolder, assetPattern);
                foreach (var assetFile in assetFiles)
                {
                    var assetBytes = File.ReadAllBytes(assetFile);
                    var asset = new AuroraAsset.AssetFile(assetBytes);

                    System.Drawing.Image image = null;
                    switch (imageType)
                    {
                        case "Banner":
                            image = asset.HasIconBanner ? asset.GetBanner() : null;
                            break;
                        case "Icon":
                            image = asset.HasIconBanner ? asset.GetIcon() : null;
                            break;
                        case "Boxart":
                            image = asset.HasBoxArt ? asset.GetBoxart() : null;
                            break;
                        case "Background":
                            image = asset.HasBackground ? asset.GetBackground() : null;
                            break;
                        case "Screenshot1":
                        case "Screenshot2":
                        case "Screenshot3":
                        case "Screenshot4":
                        case "Screenshot5":
                            if (asset.HasScreenshots)
                            {
                                var screenshots = asset.GetScreenshots();
                                var index = int.Parse(imageType.Substring(10)) - 1;
                                if (screenshots.Length > index)
                                {
                                    image = screenshots[index];
                                }
                            }
                            break;
                    }

                    if (image != null)
                    {
                        using (var ms = new MemoryStream())
                        {
                            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            ms.Position = 0;
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            return bitmap;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
            return null;
        }

        private List<ImageSource> GetFullSizeScreenshots(string thumbPath)
        {
            try
            {
                // Extract title ID from thumb path
                var pathParts = thumbPath.Split('/');
                var titleId = pathParts[pathParts.Length - 2];

                // Find the original screenshots asset file
                var gameFolder = Path.Combine(
                    Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thumbPath))), // Go up 3 levels
                    titleId);

                var screenshotFiles = Directory.GetFiles(gameFolder, "SS*.asset");
                foreach (var assetFile in screenshotFiles)
                {
                    var assetBytes = File.ReadAllBytes(assetFile);
                    var asset = new AuroraAsset.AssetFile(assetBytes);

                    if (asset.HasScreenshots)
                    {
                        var screenshots = asset.GetScreenshots();
                        var result = new List<ImageSource>();

                        foreach (var screenshot in screenshots)
                        {
                            if (screenshot != null)
                            {
                                using (var ms = new MemoryStream())
                                {
                                    screenshot.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                    ms.Position = 0;
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = ms;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                    result.Add(bitmap);
                                }
                            }
                        }

                        if (result.Any())
                        {
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
            return null;
        }

        private void OnImageContainerMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                var now = DateTime.Now;
                if (border == _lastClickedBorder && (now - _lastClick).TotalMilliseconds < 500) // Double-click detected
                {
                    if (border.Tag is string imageType)
                    {
                        ImageSource imageSource = null;
                        List<ImageSource> imageSources = null;
                        string title = imageType;
                        int currentIndex = 0;

                        // Handle screenshots differently
                        if (imageType.StartsWith("Screenshot"))
                        {
                            // Create a list of all available screenshot sources
                            imageSources = new List<ImageSource>();
                            
                            // Check if we're viewing thumbnails
                            bool usingThumbs = false;
                            string thumbPath = null;
                            
                            // Add each non-null screenshot to the list and check if they're thumbnails
                            if (Screenshot1Source != null)
                            {
                                if (imageType == "Screenshot1")
                                {
                                    currentIndex = imageSources.Count;
                                    if (IsAssetThumbnail(Screenshot1Source))
                                    {
                                        usingThumbs = true;
                                        var uri = ((BitmapImage)Screenshot1Source).UriSource;
                                        thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                    }
                                }
                                imageSources.Add(Screenshot1Source);
                            }
                            if (Screenshot2Source != null)
                            {
                                if (imageType == "Screenshot2")
                                {
                                    currentIndex = imageSources.Count;
                                    if (IsAssetThumbnail(Screenshot2Source))
                                    {
                                        usingThumbs = true;
                                        var uri = ((BitmapImage)Screenshot2Source).UriSource;
                                        thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                    }
                                }
                                imageSources.Add(Screenshot2Source);
                            }
                            if (Screenshot3Source != null)
                            {
                                if (imageType == "Screenshot3")
                                {
                                    currentIndex = imageSources.Count;
                                    if (IsAssetThumbnail(Screenshot3Source))
                                    {
                                        usingThumbs = true;
                                        var uri = ((BitmapImage)Screenshot3Source).UriSource;
                                        thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                    }
                                }
                                imageSources.Add(Screenshot3Source);
                            }
                            if (Screenshot4Source != null)
                            {
                                if (imageType == "Screenshot4")
                                {
                                    currentIndex = imageSources.Count;
                                    if (IsAssetThumbnail(Screenshot4Source))
                                    {
                                        usingThumbs = true;
                                        var uri = ((BitmapImage)Screenshot4Source).UriSource;
                                        thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                    }
                                }
                                imageSources.Add(Screenshot4Source);
                            }
                            if (Screenshot5Source != null)
                            {
                                if (imageType == "Screenshot5")
                                {
                                    currentIndex = imageSources.Count;
                                    if (IsAssetThumbnail(Screenshot5Source))
                                    {
                                        usingThumbs = true;
                                        var uri = ((BitmapImage)Screenshot5Source).UriSource;
                                        thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                    }
                                }
                                imageSources.Add(Screenshot5Source);
                            }

                            // If using thumbnails, get full-size images from asset
                            if (usingThumbs && thumbPath != null)
                            {
                                var fullSizeScreenshots = GetFullSizeScreenshots(thumbPath);
                                if (fullSizeScreenshots != null && fullSizeScreenshots.Any())
                                {
                                    imageSources = fullSizeScreenshots;
                                }
                            }
                            
                            title = "Screenshots";
                        }
                        else // Handle other image types
                        {
                            switch (imageType)
                            {
                                case "Banner":
                                    if (_imagePaths.ContainsKey(ImageType.Banner) && _imagePaths[ImageType.Banner].Any())
                                    {
                                        imageSources = _imagePaths[ImageType.Banner].Select(path => 
                                        {
                                            var bitmap = new BitmapImage(new Uri(path));
                                            bitmap.Freeze();
                                            return (ImageSource)bitmap;
                                        }).ToList();
                                        currentIndex = _currentIndices[ImageType.Banner];
                                    }
                                    else
                                    {
                                        imageSource = BannerSource;
                                        if (IsAssetThumbnail(imageSource))
                                        {
                                            var uri = ((BitmapImage)imageSource).UriSource;
                                            var thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                            var fullSize = GetFullSizeAssetImage(thumbPath, imageType);
                                            if (fullSize != null) imageSource = fullSize;
                                        }
                                    }
                                    break;
                                case "Icon":
                                    if (_imagePaths.ContainsKey(ImageType.Icon) && _imagePaths[ImageType.Icon].Any())
                                    {
                                        imageSources = _imagePaths[ImageType.Icon].Select(path => 
                                        {
                                            var bitmap = new BitmapImage(new Uri(path));
                                            bitmap.Freeze();
                                            return (ImageSource)bitmap;
                                        }).ToList();
                                        currentIndex = _currentIndices[ImageType.Icon];
                                    }
                                    else
                                    {
                                        imageSource = IconSource;
                                        if (IsAssetThumbnail(imageSource))
                                        {
                                            var uri = ((BitmapImage)imageSource).UriSource;
                                            var thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                            var fullSize = GetFullSizeAssetImage(thumbPath, imageType);
                                            if (fullSize != null) imageSource = fullSize;
                                        }
                                    }
                                    break;
                                case "Boxart":
                                    if (_imagePaths.ContainsKey(ImageType.Boxart) && _imagePaths[ImageType.Boxart].Any())
                                    {
                                        imageSources = _imagePaths[ImageType.Boxart].Select(path => 
                                        {
                                            var bitmap = new BitmapImage(new Uri(path));
                                            bitmap.Freeze();
                                            return (ImageSource)bitmap;
                                        }).ToList();
                                        currentIndex = _currentIndices[ImageType.Boxart];
                                    }
                                    else
                                    {
                                        imageSource = BoxartSource;
                                        if (IsAssetThumbnail(imageSource))
                                        {
                                            var uri = ((BitmapImage)imageSource).UriSource;
                                            var thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                            var fullSize = GetFullSizeAssetImage(thumbPath, imageType);
                                            if (fullSize != null) imageSource = fullSize;
                                        }
                                    }
                                    break;
                                case "Background":
                                    if (_imagePaths.ContainsKey(ImageType.Background) && _imagePaths[ImageType.Background].Any())
                                    {
                                        imageSources = _imagePaths[ImageType.Background].Select(path => 
                                        {
                                            var bitmap = new BitmapImage(new Uri(path));
                                            bitmap.Freeze();
                                            return (ImageSource)bitmap;
                                        }).ToList();
                                        currentIndex = _currentIndices[ImageType.Background];
                                    }
                                    else
                                    {
                                        imageSource = BackgroundSource;
                                        if (IsAssetThumbnail(imageSource))
                                        {
                                            var uri = ((BitmapImage)imageSource).UriSource;
                                            var thumbPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
                                            var fullSize = GetFullSizeAssetImage(thumbPath, imageType);
                                            if (fullSize != null) imageSource = fullSize;
                                        }
                                    }
                                    break;
                            }
                        }

                        if (imageSources != null && imageSources.Any())
                        {
                            var viewer = new AuroraAssetEditor.ImageViewerWindow(imageSources, title, currentIndex, NavigationStyle);
                            viewer.Owner = Window.GetWindow(this);
                            viewer.ShowDialog();
                        }
                        else if (imageSource != null)
                        {
                            var viewer = new AuroraAssetEditor.ImageViewerWindow(imageSource, title, NavigationStyle);
                            viewer.Owner = Window.GetWindow(this);
                            viewer.ShowDialog();
                        }
                    }
                    _lastClick = DateTime.MinValue; // Reset after double-click
                    _lastClickedBorder = null;
                }
                else
                {
                    _lastClick = now;
                    _lastClickedBorder = border;
                }
            }
        }
    }
} 