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
using System.Windows.Threading;
using AuroraAssetEditor.Helpers;
using System.Diagnostics;
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
        private readonly Dictionary<ImageType, List<string>> _originalAssetPaths = new Dictionary<ImageType, List<string>>();
        private readonly Dictionary<ImageType, int> _currentIndices = new Dictionary<ImageType, int>();
        private DateTime _lastClick = DateTime.MinValue;
        private Border _lastClickedBorder = null;
        private Dictionary<ImageType, string> _currentImagePaths = new Dictionary<ImageType, string>();
        private readonly Dictionary<ImageType, Border> _imageBorders = new Dictionary<ImageType, Border>();

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

            // Initialize image collections for all types
            foreach (ImageType type in Enum.GetValues(typeof(ImageType)))
            {
                _imagePaths[type] = new List<string>();
                _originalAssetPaths[type] = new List<string>();
                _currentIndices[type] = 0;

                // Store reference to border for each type
                var image = FindName($"{type}Image") as Image;
                var border = image?.Parent as Border;
                if (border != null)
                {
                    _imageBorders[type] = border;
                }
            }

            // Ensure base directories exist
            Directory.CreateDirectory("localassets/thumbs");
            Directory.CreateDirectory("localassets/fullsize");
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
                
                // Update border color when image source changes
                var type = (ImageType)Enum.Parse(typeof(ImageType), imageName.Replace("Image", ""));
                control.UpdateBorderColor(type, e.NewValue as ImageSource);
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
            _currentImagePaths.Clear();
            foreach (var type in _imagePaths.Keys.ToList())
            {
                ClearType(type);
            }
        }

        public void SetDirectImage(string imagePath, ImageType type)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    return;
                }

                // Store the current image path
                _currentImagePaths[type] = imagePath;

                // Create URI - if path is relative, keep it relative
                Uri uri;
                if (Path.IsPathRooted(imagePath))
                {
                    uri = new Uri(imagePath, UriKind.Absolute);
                }
                else
                {
                    uri = new Uri(imagePath, UriKind.Relative);
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = uri;
                bitmap.EndInit();
                bitmap.Freeze(); // Make it thread-safe

                // Update UI on the UI thread
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Store the bitmap before setting the source
                        ImageSource oldSource = GetImageSourceForType(type);
                        
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

                        // Explicitly update border color after setting the source
                        UpdateBorderColor(type, bitmap);
                    }
                    catch (Exception ex)
                    {
                        MainWindow.SaveError(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
        }

        private string ExtractFullSizeImage(string assetPath, ImageType type, string titleId)
        {
            try
            {
                // Create assets directory if it doesn't exist
                var assetsDir = Path.Combine("localassets", "fullsize", titleId);
                Directory.CreateDirectory(assetsDir);

                // Check if we already have the extracted image
                var outputPath = Path.Combine(assetsDir, $"{type}.png");
                if (File.Exists(outputPath))
                {
                    return outputPath;
                }

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
                    image.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
            return null;
        }

        private string CreateThumbnail(System.Drawing.Image image, ImageType type, string titleId)
        {
            try
            {
                var thumbsDir = Path.Combine("localassets", "thumbs", titleId);
                Directory.CreateDirectory(thumbsDir);
                var thumbPath = Path.Combine(thumbsDir, $"{type}.png");

                using (var thumbnail = new System.Drawing.Bitmap(image))
                {
                    System.Drawing.Image resizedImage;
                    
                    if (type == ImageType.Boxart)
                    {
                        // Apply smoothing for boxart
                        using (var smoothed = new System.Drawing.Bitmap(thumbnail.Width, thumbnail.Height))
                        {
                            using (var g = System.Drawing.Graphics.FromImage(smoothed))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                g.DrawImage(thumbnail, 0, 0, thumbnail.Width, thumbnail.Height);
                            }
                            resizedImage = new System.Drawing.Bitmap(smoothed, new System.Drawing.Size(64, 64));
                        }
                    }
                    else if (type == ImageType.Icon)
                    {
                        resizedImage = new System.Drawing.Bitmap(thumbnail, new System.Drawing.Size(128, 128));
                    }
                    else
                    {
                        resizedImage = new System.Drawing.Bitmap(thumbnail, new System.Drawing.Size(64, 64));
                    }

                    using (resizedImage)
                    {
                        resizedImage.Save(thumbPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }

                return thumbPath;
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
                return null;
            }
        }

        public void SetAssetImage(string assetPath, ImageType type)
        {
            try
            {
                if (!File.Exists(assetPath)) return;

                // Store the original asset path for later use
                if (!_originalAssetPaths.ContainsKey(type))
                {
                    _originalAssetPaths[type] = new List<string>();
                }
                if (!_originalAssetPaths[type].Contains(assetPath))
                {
                    _originalAssetPaths[type].Add(assetPath);
                }

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
                    var titleId = GlobalState.CurrentGame?.TitleId ?? "unknown";
                    var hash = AssetCache.CalculateFileHash(assetPath);
                    var assetType = type.ToString();

                    // Ensure directories exist
                    Directory.CreateDirectory(Path.Combine("localassets", "fullsize", titleId));
                    Directory.CreateDirectory(Path.Combine("localassets", "thumbs", titleId));

                    // Extract full-size image
                    var fullSizePath = AssetCache.GetFullsizePath(titleId, assetType, hash);
                    if (!File.Exists(fullSizePath))
                    {
                        using (var fullSizeImage = new System.Drawing.Bitmap(image))
                        {
                            fullSizeImage.Save(fullSizePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }

                    // Create thumbnail
                    var thumbPath = AssetCache.GetThumbPath(titleId, assetType, hash);
                    if (!File.Exists(thumbPath))
                    {
                        using (var thumbnail = new System.Drawing.Bitmap(image))
                        {
                            System.Drawing.Image resizedImage;
                            
                            if (type == ImageType.Boxart)
                            {
                                // Apply smoothing for boxart
                                using (var smoothed = new System.Drawing.Bitmap(thumbnail.Width, thumbnail.Height))
                                {
                                    using (var g = System.Drawing.Graphics.FromImage(smoothed))
                                    {
                                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                        g.DrawImage(thumbnail, 0, 0, thumbnail.Width, thumbnail.Height);
                                    }
                                    resizedImage = new System.Drawing.Bitmap(smoothed, new System.Drawing.Size(64, 64));
                                }
                            }
                            else if (type == ImageType.Icon)
                            {
                                resizedImage = new System.Drawing.Bitmap(thumbnail, new System.Drawing.Size(128, 128));
                            }
                            else
                            {
                                resizedImage = new System.Drawing.Bitmap(thumbnail, new System.Drawing.Size(64, 64));
                            }

                            using (resizedImage)
                            {
                                resizedImage.Save(thumbPath, System.Drawing.Imaging.ImageFormat.Png);
                            }
                        }
                    }

                    // Load the thumbnail for display
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(thumbPath, UriKind.Relative);
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
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
        }

        public void SetImage(string path, ImageType type)
        {
            if (string.IsNullOrEmpty(path)) return;

            // Clear existing images of this type first
            ClearType(type);

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
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            var loadingOverlay = FindName("LoadingOverlay") as Border;

            // Get the drop target
            var dropTarget = e.OriginalSource as FrameworkElement;
            ImageType? targetType = null;

            // Find the Border control that was the target
            while (dropTarget != null && !(dropTarget is Border))
            {
                dropTarget = VisualTreeHelper.GetParent(dropTarget) as FrameworkElement;
            }

            if (dropTarget is Border border && border.Tag is string imageTypeStr)
            {
                targetType = (ImageType)Enum.Parse(typeof(ImageType), imageTypeStr);
            }

            // Ensure loading overlay is shown immediately
            if (loadingOverlay != null)
            {
                loadingOverlay.Dispatcher.Invoke(() => 
                {
                    loadingOverlay.Visibility = Visibility.Visible;
                }, System.Windows.Threading.DispatcherPriority.Send);
            }

            // Process files on background thread
            Task.Run(() =>
            {
                try 
                {
                    // If it's a single file and we have a target container, try direct placement
                    if (paths.Length == 1 && targetType.HasValue && IsValidImageFile(paths[0]))
                    {
                        if (IsImageMatchingContainerSize(paths[0], targetType.Value))
                        {
                            // Process single file for target container
                            ProcessSingleFileForTarget(paths[0], targetType.Value);
                        }
                        else
                        {
                            // Fall back to normal processing if resolution doesn't match
                            ProcessDroppedFiles(paths);
                        }
                    }
                    else
                    {
                        // Multiple files or no target - use normal processing
                        ProcessDroppedFiles(paths);
                    }
                }
                finally 
                {
                    // Hide overlay on UI thread when done
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Dispatcher.Invoke(() => 
                        {
                            loadingOverlay.Visibility = Visibility.Collapsed;
                        });
                    }
                }
            });
        }

        private bool IsValidImageFile(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp";
        }

        private bool IsImageMatchingContainerSize(string imagePath, ImageType targetType)
        {
            try
            {
                using (var image = System.Drawing.Image.FromFile(imagePath))
                {
                    // Define expected aspect ratios and size ranges for each type
                    switch (targetType)
                    {
                        case ImageType.Banner:
                            return IsAspectRatioMatching(image, 35, 8, 0.1f); // Banner is 35:8
                        case ImageType.Icon:
                            return IsAspectRatioMatching(image, 1, 1, 0.1f); // Icon is square
                        case ImageType.Boxart:
                            return IsAspectRatioMatching(image, 45, 30, 0.1f); // Boxart is 45:30
                        case ImageType.Background:
                            return IsAspectRatioMatching(image, 48, 27, 0.1f); // Background is 48:27
                        case ImageType.Screenshot1:
                        case ImageType.Screenshot2:
                        case ImageType.Screenshot3:
                        case ImageType.Screenshot4:
                        case ImageType.Screenshot5:
                            return IsAspectRatioMatching(image, 16, 9, 0.1f); // Screenshots are 16:9
                        default:
                            return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
                return false;
            }
        }

        private bool IsAspectRatioMatching(System.Drawing.Image image, float expectedWidth, float expectedHeight, float tolerance)
        {
            float expectedRatio = expectedWidth / expectedHeight;
            float actualRatio = (float)image.Width / image.Height;
            return Math.Abs(actualRatio - expectedRatio) <= tolerance;
        }

        private void ProcessSingleFileForTarget(string file, ImageType targetType)
        {
            Dispatcher.Invoke(() =>
            {
                // Clear existing images of this type
                ClearType(targetType);

                // Set the image directly
                SetImage(file, targetType);
                
                // Update border color
                UpdateBorderColor(targetType, GetImageSourceForType(targetType));
            });
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

                    // Clear existing images of this type
                    ClearType(type);

                    // Handle screenshots differently - only take the last file for each screenshot position
                    if (type >= ImageType.Screenshot1)
                    {
                        // Take only the last file for this screenshot position
                        if (typeFiles.Any())
                        {
                            var file = typeFiles.Last();
                            SetImage(file, type);
                            // Update border color after setting the image
                            UpdateBorderColor(type, GetImageSourceForType(type));
                        }
                    }
                    else // Handle non-screenshot types that support navigation
                    {
                        // Ensure the type exists in the dictionary
                        if (!_imagePaths.ContainsKey(type))
                        {
                            _imagePaths[type] = new List<string>();
                            _currentIndices[type] = 0;
                        }

                        // Sort files to ensure consistent ordering
                        typeFiles.Sort();

                        // Process each file for this type
                        foreach (var file in typeFiles)
                        {
                            var extension = Path.GetExtension(file)?.ToLowerInvariant();
                            if (extension == ".asset")
                            {
                                SetAssetImage(file, type);
                            }
                            else
                            {
                                if (!_imagePaths[type].Contains(file))
                                {
                                    _imagePaths[type].Add(file);
                                }
                            }
                        }

                        // Update navigation and display first image if we have any images
                        if (_imagePaths[type].Any())
                        {
                            _currentIndices[type] = 0;
                            LoadImageAtCurrentIndex(type);
                            UpdateCounter(type);
                            // Update border color after loading the image
                            UpdateBorderColor(type, GetImageSourceForType(type));
                        }
                    }
                }
            });
        }

        private ImageSource GetImageSourceForType(ImageType type)
        {
            switch (type)
            {
                case ImageType.Banner: return BannerSource;
                case ImageType.Icon: return IconSource;
                case ImageType.Boxart: return BoxartSource;
                case ImageType.Background: return BackgroundSource;
                case ImageType.Screenshot1: return Screenshot1Source;
                case ImageType.Screenshot2: return Screenshot2Source;
                case ImageType.Screenshot3: return Screenshot3Source;
                case ImageType.Screenshot4: return Screenshot4Source;
                case ImageType.Screenshot5: return Screenshot5Source;
                default: return null;
            }
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

        private bool IsImageThumbnail(ImageSource source)
        {
            if (source == null) return true; // Consider null (no image) as valid state
            if (source is BitmapImage bitmapImage && bitmapImage.UriSource != null)
            {
                string uriString = bitmapImage.UriSource.ToString();
                
                // Check if the path is relative or absolute
                string fullPath;
                if (bitmapImage.UriSource.IsAbsoluteUri)
                {
                    fullPath = bitmapImage.UriSource.LocalPath;
                }
                else
                {
                    fullPath = Path.GetFullPath(uriString);
                }

                // Normalize path for comparison
                fullPath = fullPath.Replace('\\', '/');
                
                // Image is considered a thumbnail if:
                // 1. It's in the localassets folder AND
                // 2. It's in the thumbs subfolder
                return fullPath.Contains("/localassets/") && fullPath.Contains("/thumbs/");
            }
            return false;
        }

        private void UpdateBorderColor(ImageType type, ImageSource source)
        {
            if (_imageBorders.TryGetValue(type, out var border))
            {
                border.BorderBrush = IsImageThumbnail(source) ? 
                    (SolidColorBrush)FindResource("DefaultBorderBrush") : 
                    new SolidColorBrush(Colors.Orange);
            }
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
                        var type = (ImageType)Enum.Parse(typeof(ImageType), imageType);
                        string imagePath = null;

                        // Get the current image source
                        ImageSource currentSource = null;
                        switch (type)
                        {
                            case ImageType.Banner: currentSource = BannerSource; break;
                            case ImageType.Icon: currentSource = IconSource; break;
                            case ImageType.Boxart: currentSource = BoxartSource; break;
                            case ImageType.Background: currentSource = BackgroundSource; break;
                            case ImageType.Screenshot1: currentSource = Screenshot1Source; break;
                            case ImageType.Screenshot2: currentSource = Screenshot2Source; break;
                            case ImageType.Screenshot3: currentSource = Screenshot3Source; break;
                            case ImageType.Screenshot4: currentSource = Screenshot4Source; break;
                            case ImageType.Screenshot5: currentSource = Screenshot5Source; break;
                        }

                        if (currentSource is BitmapImage bitmapImage && bitmapImage.UriSource != null)
                        {
                            // Get the current path, handling both absolute and relative URIs
                            string currentPath;
                            if (bitmapImage.UriSource.IsAbsoluteUri)
                            {
                                currentPath = bitmapImage.UriSource.LocalPath;
                            }
                            else
                            {
                                currentPath = bitmapImage.UriSource.OriginalString;
                            }

                            // Debug.WriteLine($"Current image path: {currentPath}");

                            // Check if this is a thumbnail path
                            if (currentPath.Contains("/thumbs/") || currentPath.Contains("\\thumbs\\"))
                            {
                                // Get the hash from the filename
                                var filename = Path.GetFileNameWithoutExtension(currentPath);
                                var hashPart = filename.Split('-').LastOrDefault();
                                
                                if (!string.IsNullOrEmpty(hashPart))
                                {
                                    // Get the titleId from the path
                                    var pathParts = currentPath.Split(new[] { "thumbs" }, StringSplitOptions.RemoveEmptyEntries);
                                    if (pathParts.Length > 1)
                                    {
                                        var titleIdPart = pathParts[1].Trim('\\', '/').Split('\\', '/')[0];
                                        
                                        // Use AssetCache to get the full-size path
                                        imagePath = AssetCache.GetFullsizePath(titleIdPart, type.ToString(), hashPart);
                                        // Debug.WriteLine($"Using cached full-size path: {imagePath}");
                                    }
                                }
                            }
                            else if (_currentImagePaths.TryGetValue(type, out string dragDropPath))
                            {
                                // For drag-dropped images, use the original path
                                imagePath = dragDropPath;
                                // Debug.WriteLine($"Using drag-dropped image path: {imagePath}");
                            }

                            if (!string.IsNullOrEmpty(imagePath))
                            {
                                // Ensure we have a full path
                                if (!Path.IsPathRooted(imagePath))
                                {
                                    imagePath = Path.GetFullPath(imagePath);
                                }

                                // Convert thumbs path to fullsize path
                                if (imagePath.Contains("\\thumbs\\") || imagePath.Contains("/thumbs/"))
                                {
                                    imagePath = imagePath.Replace("\\thumbs\\", "\\fullsize\\")
                                                       .Replace("/thumbs/", "/fullsize/");
                                }

                                // Debug.WriteLine($"Final image path: {imagePath}");

                                if (File.Exists(imagePath))
                                {
                                    // Debug.WriteLine("Loading full-size image");
                                    using (var image = System.Drawing.Image.FromFile(imagePath))
                                    {
                                        // Calculate dimensions maintaining aspect ratio with max width of 1200
                                        int width = image.Width;
                                        int height = image.Height;

                                        if (width > 1200)
                                        {
                                            double scale = 1200.0 / width;
                                            width = 1200;
                                            height = (int)(height * scale);
                                        }

                                        var bitmap = new BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                                        bitmap.EndInit();
                                        bitmap.Freeze();

                                        var viewer = new AuroraAssetEditor.ImageViewerWindow(bitmap, type.ToString(), NavigationStyle)
                                        {
                                            Owner = Window.GetWindow(this),
                                            Width = width,
                                            Height = height,
                                            WindowStyle = WindowStyle.None,
                                            ResizeMode = ResizeMode.NoResize
                                        };
                                        viewer.ShowDialog();
                                    }
                                }
                                else
                                {
                                    // Debug.WriteLine($"Full-size image not found: {imagePath}");
                                }
                            }
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

        public void ClearType(ImageType type)
        {
            Dispatcher.Invoke(() =>
            {
                // Clear the current image paths for this type
                if (_currentImagePaths.ContainsKey(type))
                {
                    _currentImagePaths.Remove(type);
                }

                // Clear image collections
                if (_imagePaths.ContainsKey(type))
                {
                    _imagePaths[type].Clear();
                }
                if (_originalAssetPaths.ContainsKey(type))
                {
                    _originalAssetPaths[type].Clear();
                }
                if (_currentIndices.ContainsKey(type))
                {
                    _currentIndices[type] = 0;
                }

                ImageSource nullSource = null;
                switch (type)
                {
                    case ImageType.Banner:
                        BannerSource = nullSource;
                        UpdateCounter(type);
                        break;
                    case ImageType.Icon:
                        IconSource = nullSource;
                        UpdateCounter(type);
                        break;
                    case ImageType.Boxart:
                        BoxartSource = nullSource;
                        UpdateCounter(type);
                        break;
                    case ImageType.Background:
                        BackgroundSource = nullSource;
                        UpdateCounter(type);
                        break;
                    case ImageType.Screenshot1:
                        Screenshot1Source = nullSource;
                        break;
                    case ImageType.Screenshot2:
                        Screenshot2Source = nullSource;
                        break;
                    case ImageType.Screenshot3:
                        Screenshot3Source = nullSource;
                        break;
                    case ImageType.Screenshot4:
                        Screenshot4Source = nullSource;
                        break;
                    case ImageType.Screenshot5:
                        Screenshot5Source = nullSource;
                        break;
                }
                UpdateBorderColor(type, nullSource);
            });
        }
    }
} 