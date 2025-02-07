//
//  MainWindow.xaml.cs
//  AuroraAssetEditor
//
//  Created by Swizzy on 08/05/2015
//  Copyright (c) 2015 Swizzy. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Threading;
using System.Collections.ObjectModel;
using System.Configuration;
using AuroraAssetEditor.Classes;
using AuroraAssetEditor.Models;
using AuroraAssetEditor.Controls;
using AuroraAssetEditor.Helpers;
using AuroraAssetEditor.Properties;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using Newtonsoft.Json;
using Image = System.Drawing.Image;
using WpfImage = System.Windows.Controls.Image;
using Size = System.Drawing.Size;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Diagnostics;

namespace AuroraAssetEditor {
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private const string AssetFileFilter =
            "Game Cover/Boxart Asset File(defaultFilename) (GC*.asset)|GC*.asset|Background Asset File(defaultFilename) (BK*.asset)|BK*.asset|Icon/Banner Asset File(defaultFilename) (GL*.asset)|GL*.asset|Screenshot Asset File(defaultFilename) (SS*.asset)|SS*.asset|Aurora Asset Files (*.asset)|*.asset|All Files(*)|*";

        private const string ImageFileFilter =
            "All Supported Images|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif;*.tiff;|BMP (*.bmp)|*.bmp|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|GIF (*.gif)|*.gif|TIFF (*.tif;*.tiff)|*.tiff;*.tif|PNG (*.png)|*.png|All Files|*";

        private readonly FtpAssetsControl _ftpAssetsControl;

        // Constructor with optional args for backward compatibility
        public MainWindow(IEnumerable<string> args = null) {
            InitializeComponent();
            Title = string.Format(Title, Assembly.GetExecutingAssembly().GetName().Version.Major,
                                Assembly.GetExecutingAssembly().GetName().Version.Minor,
                                Assembly.GetExecutingAssembly().GetName().Version.Build);

            // Initialize AssetCache
            Classes.AssetCache.Initialize();

            // Initialize Xbox360DB
            Classes.Xbox360DB.Initialize();

            // Initialize FTP control
            _ftpAssetsControl = new FtpAssetsControl(this);
            FtpAssetsContainer.Content = _ftpAssetsControl;

            // Set default assets path
            var defaultPath = @"c:\Users\larst\OneDrive\EMULATOR-AND-SYSTEM DATA\XBOX360\Tools\AuroraAssetEditor\Temp\# Completed";
            if (Directory.Exists(defaultPath))
            {
                AssetPathTextBox.Text = defaultPath;
            }

            // Load last used path from settings
            var lastPath = Settings.Default.LastPath;
            if (!string.IsNullOrEmpty(lastPath))
            {
                AssetPathTextBox.Text = lastPath;
            }

            // Auto-load local assets if path exists
            if (!string.IsNullOrWhiteSpace(AssetPathTextBox.Text) && Directory.Exists(AssetPathTextBox.Text))
            {
                LoadAssetsButton_Click(null, null);
            }

            GlobalState.GameChanged += OnGameChanged;
        }

        private void OnGameChanged()
        {
            Dispatcher.Invoke(() =>
            {
                GameSelector.Visibility = GlobalState.CurrentGame.IsGameSelected ? Visibility.Visible : Visibility.Hidden;
                GameSelector.Header = GlobalState.CurrentGame.Title;
                GameTitleIdMenu.Header = GlobalState.CurrentGame.TitleId;
                GameDbIdMenu.Header = GlobalState.CurrentGame.DbId;
            });
        }

        private static void SaveFileError(string file, Exception ex) {
            SaveError(ex);
            MessageBox.Show(string.Format("ERROR: There was an error while trying to process {0}{1}See Error.log for more information", file, Environment.NewLine), "ERROR", MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }

        private void LoadAuroraAsset(string filename) {
            try {
                var titleId = GlobalState.CurrentGame?.TitleId ?? "unknown";
                var selectedFolder = LocalAssetList.SelectedItem as FolderInfo;
                if (selectedFolder?.CachedAssets == null) return;

                // Get the asset type from the filename
                var assetType = Path.GetFileName(filename).Substring(0, 2).ToUpper();
                var assetInfo = selectedFolder.CachedAssets.FirstOrDefault(a => 
                    Path.GetFileName(a.Value.FilePath).StartsWith(assetType, StringComparison.OrdinalIgnoreCase));

                if (assetInfo.Value.Hash == null) return;

                Application.Current.Dispatcher.Invoke(() => {
                    try {
                        // Ensure base directories exist
                        Directory.CreateDirectory(Path.Combine("localassets", "thumbs", titleId));
                        Directory.CreateDirectory(Path.Combine("localassets", "fullsize", titleId));

                        var assetBytes = File.ReadAllBytes(filename);
                        var asset = new AuroraAsset.AssetFile(assetBytes);

                        switch (assetType)
                        {
                            case "GC":
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Boxart);
                                if (asset.HasBoxArt)
                                {
                                    using (var image = asset.GetBoxart())
                                    {
                                        if (image != null)
                                        {
                                            AssetCache.CacheImage(image, assetInfo.Value.Hash, titleId, "Boxart");
                                            var thumbPath = AssetCache.GetThumbPath(titleId, "Boxart", assetInfo.Value.Hash);
                                            LocalAssetArtwork.BoxartSource = new BitmapImage(new Uri(thumbPath, UriKind.Relative));
                                        }
                                    }
                                }
                                break;

                            case "BK":
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Background);
                                if (asset.HasBackground)
                                {
                                    using (var image = asset.GetBackground())
                                    {
                                        if (image != null)
                                        {
                                            AssetCache.CacheImage(image, assetInfo.Value.Hash, titleId, "Background");
                                            var thumbPath = AssetCache.GetThumbPath(titleId, "Background", assetInfo.Value.Hash);
                                            LocalAssetArtwork.BackgroundSource = new BitmapImage(new Uri(thumbPath, UriKind.Relative));
                                        }
                                    }
                                }
                                break;

                            case "GL":
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Banner);
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Icon);
                                if (asset.HasIconBanner)
                                {
                                    using (var bannerImage = asset.GetBanner())
                                    {
                                        if (bannerImage != null)
                                        {
                                            AssetCache.CacheImage(bannerImage, assetInfo.Value.Hash, titleId, "Banner");
                                            var thumbPath = AssetCache.GetThumbPath(titleId, "Banner", assetInfo.Value.Hash);
                                            LocalAssetArtwork.BannerSource = new BitmapImage(new Uri(thumbPath, UriKind.Relative));
                                        }
                                    }

                                    using (var iconImage = asset.GetIcon())
                                    {
                                        if (iconImage != null)
                                        {
                                            AssetCache.CacheImage(iconImage, assetInfo.Value.Hash, titleId, "Icon");
                                            var thumbPath = AssetCache.GetThumbPath(titleId, "Icon", assetInfo.Value.Hash);
                                            LocalAssetArtwork.IconSource = new BitmapImage(new Uri(thumbPath, UriKind.Relative));
                                        }
                                    }
                                }
                                break;

                            case "SS":
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Screenshot1);
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Screenshot2);
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Screenshot3);
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Screenshot4);
                                LocalAssetArtwork.ClearType(AssetArtwork.ImageType.Screenshot5);

                                if (asset.HasScreenshots)
                                {
                                    var screenshots = asset.GetScreenshots();
                                    for (var i = 0; i < screenshots.Length && i < 5; i++)
                                    {
                                        if (screenshots[i] != null)
                                        {
                                            using (var screenshot = screenshots[i])
                                            {
                                                var screenshotNum = i + 1;
                                                AssetCache.CacheImage(screenshot, assetInfo.Value.Hash, titleId, $"Screenshot{screenshotNum}");
                                                var thumbPath = AssetCache.GetThumbPath(titleId, $"Screenshot{screenshotNum}", assetInfo.Value.Hash);
                                                var source = new BitmapImage(new Uri(thumbPath, UriKind.Relative));
                                                
                                                switch (i)
                                                {
                                                    case 0: LocalAssetArtwork.Screenshot1Source = source; break;
                                                    case 1: LocalAssetArtwork.Screenshot2Source = source; break;
                                                    case 2: LocalAssetArtwork.Screenshot3Source = source; break;
                                                    case 3: LocalAssetArtwork.Screenshot4Source = source; break;
                                                    case 4: LocalAssetArtwork.Screenshot5Source = source; break;
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }

                        // Update cache status
                        selectedFolder.CachedAssets[assetInfo.Key] = (assetInfo.Value.FilePath, assetInfo.Value.Hash, true);
                    }
                    catch (Exception ex)
                    {
                        SaveError(ex);
                    }
                });
            }
            catch(Exception ex) {
                SaveFileError(filename, ex);
            }
        }

        private void ClearLocalControls()
        {
            LocalAssetArtwork.ClearAll();
        }

        private ImageSource ConvertToImageSource(Image img)
        {
            if (img == null) return null;

            try
            {
                // First save the image to a temporary file to ensure we have a stable source
                var tempFile = Path.Combine(Path.GetTempPath(), $"auroraasset_{Guid.NewGuid()}.png");
                img.Save(tempFile, ImageFormat.Png);

                // Create and load the bitmap on the UI thread
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(tempFile);
                bi.EndInit();
                bi.Freeze(); // Make it thread-safe

                // Clean up temp file
                try { File.Delete(tempFile); } catch { }

                return bi;
            }
            catch (Exception ex)
            {
                                 SaveError(ex);
                return null;
            }
        }

        internal void Reset()
        {
            ClearLocalControls();
        }

        private void LoadAssetsButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug.WriteLine("\n=== Starting LoadAssetsButton_Click ===");
            if (string.IsNullOrWhiteSpace(AssetPathTextBox.Text))
            {
                // Debug.WriteLine("Error: Empty asset path");
                MessageBox.Show("Please enter a valid path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(AssetPathTextBox.Text))
            {
                // Debug.WriteLine($"Error: Directory does not exist: {AssetPathTextBox.Text}");
                MessageBox.Show("Directory does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Debug.WriteLine($"Processing path: {AssetPathTextBox.Text}");

            // Save path to settings on UI thread
            Settings.Default.LastPath = AssetPathTextBox.Text;
            Settings.Default.Save();
            // Debug.WriteLine("Saved path to settings");

            // Store the path we're going to process
            var pathToProcess = AssetPathTextBox.Text;

            // Clear current items and show loading indicator
            // Debug.WriteLine("Clearing current items and showing loading indicator");
            if (LocalAssetList != null)
            {
                LocalAssetList.SetItems(null);
                // Debug.WriteLine("Cleared LocalAssetList items");
                }
                else
                {
                // Debug.WriteLine("Warning: LocalAssetList is null");
                }

            if (ListViewBusyIndicator != null)
            {
                ListViewBusyIndicator.Visibility = Visibility.Visible;
                // Debug.WriteLine("Set ListViewBusyIndicator to visible");
            }
            else
        {
                // Debug.WriteLine("Warning: ListViewBusyIndicator is null");
        }

            // Start background work
            // Debug.WriteLine("Starting background work");
            Task.Run(() => 
        {
            try
            {
                    // Debug.WriteLine("Background task started");
                    // Create list to store results
                    var results = new List<FolderInfo>();
                    
                    // Get all directories
                    // Debug.WriteLine("Getting directories");
                    var folders = Directory.GetDirectories(pathToProcess);
                    // Debug.WriteLine($"Found {folders.Length} folders to process");
                    
                    // Process each folder
                    foreach (var folder in folders)
                    {
                        // Debug.WriteLine($"\nProcessing folder: {folder}");
                        var dirInfo = new DirectoryInfo(folder);
                        var titleId = ExtractTitleId(dirInfo);
                        // Debug.WriteLine($"Extracted TitleId: {titleId}");
                        
                        // Create folder info without cached assets first
                        var folderInfo = new FolderInfo
                        {
                            GameName = dirInfo.Name,
                            TitleId = titleId,
                            Path = dirInfo.FullName
                        };
                        // Debug.WriteLine($"Created FolderInfo - GameName: {folderInfo.GameName}, TitleId: {folderInfo.TitleId}");

                        // Add to results first
                        results.Add(folderInfo);

                        // Then try to cache assets if we have a title ID
                        if (!string.IsNullOrEmpty(titleId))
                        {
                            try
                            {
                                // Debug.WriteLine("Checking folder cache");
                                folderInfo.CachedAssets = AssetCache.CheckFolderCache(dirInfo.FullName, titleId);
                                if (folderInfo.CachedAssets != null)
                                {
                                    // Debug.WriteLine($"Found {folderInfo.CachedAssets.Count} cached assets");
                                }
                                else
                                {
                                    // Debug.WriteLine("No cached assets found");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Debug.WriteLine($"Error checking folder cache: {ex.Message}");
                                SaveError(ex);
                            }
                        }
                    }

                    // Return to UI thread to update interface
                    // Debug.WriteLine("\nReturning to UI thread to update interface");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // Debug.WriteLine("On UI thread");
                            if (LocalAssetList != null)
                            {
                                // Debug.WriteLine("LocalAssetList is not null");
                                if (results != null && results.Any())
                                {
                                    // Debug.WriteLine($"Setting {results.Count} items to LocalAssetList");
                                    LocalAssetList.SetItems(results);
                                    // Debug.WriteLine("Items set successfully");
                                    LocalAssetList.RefreshView();
                                    // Debug.WriteLine("View refreshed");
                        }
                        else
                        {
                                    // Debug.WriteLine("Warning: No results to set");
                        }
                    }
                    else
                    {
                                // Debug.WriteLine("Error: LocalAssetList is null on UI thread");
                }
            }
            catch (Exception ex)
            {
                            // Debug.WriteLine($"Error on UI thread: {ex.Message}");
                            MessageBox.Show($"Error setting items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        finally
                        {
                            if (ListViewBusyIndicator != null)
                            {
                                ListViewBusyIndicator.Visibility = Visibility.Collapsed;
                                // Debug.WriteLine("Hidden ListViewBusyIndicator");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Debug.WriteLine($"Error in background task: {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error loading folders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        if (ListViewBusyIndicator != null)
                        {
                            ListViewBusyIndicator.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            });
            // Debug.WriteLine("=== Finished LoadAssetsButton_Click method ===\n");
        }

        private void LocalTitleFilterChanged(object sender, TextChangedEventArgs e)
        {
            // Filtering is now handled by AssetListControl
        }

        private void LocalTitleIdFilterChanged(object sender, TextChangedEventArgs e)
        {
            // Filtering is now handled by AssetListControl
        }

        private void FolderListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LocalAssetList.SelectedItem is FolderInfo selectedFolder)
            {
                // Debug.WriteLine($"\n=== FolderListView_SelectionChanged ===");
                // Debug.WriteLine($"Selected folder: {selectedFolder.GameName}");
                // Debug.WriteLine($"TitleId: {selectedFolder.TitleId}");

                var newGame = new Game
                {
                    Title = selectedFolder.GameName,
                    TitleId = selectedFolder.TitleId,
                    IsGameSelected = true
                };

                GlobalState.CurrentGame = newGame;

                // Query game data and update AssetLocalData
                var result = Xbox360DB.GetGameDataByTitleID(selectedFolder.TitleId);
                if (result.Success)
                {
                    LocalAssetData.UpdateData(
                        result.Data.TitleID,
                        result.Data.GameName,
                        result.Data.ReleaseDate,
                        result.Data.Developer,
                        result.Data.Publisher,
                        result.Data.Description,
                        result.Data.Variants
                    );
                }
                else
                {
                    // If no data found in cache, try to load it from the database
                    Xbox360DB.LoadAllGameData();
                    result = Xbox360DB.GetGameDataByTitleID(selectedFolder.TitleId);
                    if (result.Success)
                    {
                        LocalAssetData.UpdateData(
                            result.Data.TitleID,
                            result.Data.GameName,
                            result.Data.ReleaseDate,
                            result.Data.Developer,
                            result.Data.Publisher,
                            result.Data.Description,
                            result.Data.Variants
                        );
                    }
                    else
                    {
                        LocalAssetData.Clear();
                    }
                }

                // Clear current images
                LocalAssetArtwork.ClearAll();

                // Load thumbnails if they exist
                if (selectedFolder.CachedAssets != null)
                {
                    // Debug.WriteLine($"Found {selectedFolder.CachedAssets.Count} cached assets");
                    foreach (var asset in selectedFolder.CachedAssets)
                    {
                        var filename = Path.GetFileName(asset.Value.FilePath);
                        var assetType = filename.Substring(0, 2).ToUpper();
                        var titleId = selectedFolder.TitleId;
                        var hash = asset.Value.Hash;
                        var hasCache = asset.Value.HasCache;

                        // Debug.WriteLine($"\nProcessing asset: {filename}");
                        // Debug.WriteLine($"Asset type: {assetType}");
                        // Debug.WriteLine($"Hash: {hash}");
                        // Debug.WriteLine($"Has cache: {hasCache}");

                        if (!hasCache)
                        {
                            // Debug.WriteLine("Skipping - no cached version available");
                            continue;
                        }

                        switch (assetType)
                        {
                            case "GC":
                                var boxartPath = AssetCache.GetThumbPath(titleId, "Boxart", hash);
                                // Debug.WriteLine($"Boxart path: {boxartPath}");
                                if (File.Exists(boxartPath))
                                {
                                    // Debug.WriteLine("Boxart file exists, setting image");
                                    LocalAssetArtwork.SetDirectImage(boxartPath, Controls.AssetArtwork.ImageType.Boxart);
                                }
                                break;

                            case "BK":
                                var backgroundPath = AssetCache.GetThumbPath(titleId, "Background", hash);
                                // Debug.WriteLine($"Background path: {backgroundPath}");
                                if (File.Exists(backgroundPath))
                                {
                                    // Debug.WriteLine("Background file exists, setting image");
                                    LocalAssetArtwork.SetDirectImage(backgroundPath, Controls.AssetArtwork.ImageType.Background);
                                }
                                break;

                            case "GL":
                                var bannerPath = AssetCache.GetThumbPath(titleId, "Banner", hash);
                                var iconPath = AssetCache.GetThumbPath(titleId, "Icon", hash);
                                // Debug.WriteLine($"Banner path: {bannerPath}");
                                // Debug.WriteLine($"Icon path: {iconPath}");
                                
                                if (File.Exists(bannerPath))
                                {
                                    // Debug.WriteLine("Banner file exists, setting image");
                                    LocalAssetArtwork.SetDirectImage(bannerPath, Controls.AssetArtwork.ImageType.Banner);
                                }
                                
                                if (File.Exists(iconPath))
                                {
                                    // Debug.WriteLine("Icon file exists, setting image");
                                    LocalAssetArtwork.SetDirectImage(iconPath, Controls.AssetArtwork.ImageType.Icon);
                                }
                                break;

                            case "SS":
                                for (int i = 1; i <= 5; i++)
                                {
                                    var screenshotPath = AssetCache.GetThumbPath(titleId, $"Screenshot{i}", hash);
                                    // Debug.WriteLine($"Screenshot {i} path: {screenshotPath}");
                                    if (File.Exists(screenshotPath))
                                    {
                                        // Debug.WriteLine($"Screenshot {i} file exists, setting image");
                                        var imageType = (Controls.AssetArtwork.ImageType)Enum.Parse(
                                            typeof(Controls.AssetArtwork.ImageType), 
                                            $"Screenshot{i}");
                                        LocalAssetArtwork.SetDirectImage(screenshotPath, imageType);
                                    }
                                }
                                break;
                        }
                    }
                }
                else
                {
                    // Debug.WriteLine("No cached assets found");
                }
            }
            else
            {
                LocalAssetData.Clear();
                LocalAssetArtwork.ClearAll();
            }
        }

        private string ExtractTitleId(DirectoryInfo dirInfo)
        {
            // Try to find a title ID anywhere in the name
            var match = System.Text.RegularExpressions.Regex.Match(dirInfo.Name, @"[^A-Fa-f0-9]([A-Fa-f0-9]{5,8})[^A-Fa-f0-9]");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpper();
            }

            return "";
        }

        private void EditMenuOpened(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void TestColoring_Click(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void CopyTitleIdToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalState.CurrentGame?.TitleId != null)
            {
                Clipboard.SetText(GlobalState.CurrentGame.TitleId);
            }
        }

        private void CopyDbIdToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalState.CurrentGame?.DbId != null)
            {
                Clipboard.SetText(GlobalState.CurrentGame.DbId);
            }
        }

        private void ClearCurrentGame_Click(object sender, RoutedEventArgs e)
        {
            GlobalState.CurrentGame = new Game { IsGameSelected = false };
            ClearLocalControls();
        }

        private void CreateNewOnClick(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void LoadAssetOnClick(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void ExitOnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            GlobalState.GameChanged -= OnGameChanged;
        }

        internal static void SaveError(Exception ex) 
        { 
            File.AppendAllText("error.log", string.Format("[{0}]:{2}{1}{2}", DateTime.Now, ex, Environment.NewLine)); 
        }

        internal void UpdateFtpGames(List<FtpGameInfo> ftpGames)
        {
            // Update FTP games list and refresh UI
            Dispatcher.Invoke(() => {
                LocalAssetList?.RefreshView();
            });
        }

        internal void UpdateMatchingColors()
        {
            // Update matching colors in the ListView
            Dispatcher.Invoke(() => {
                LocalAssetList?.RefreshView();
            });
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header != null && LocalAssetList != null)
            {
                var view = CollectionViewSource.GetDefaultView(LocalAssetList.ItemsSource);
                var binding = header.Column.DisplayMemberBinding as Binding;
                var propertyName = binding?.Path.Path;
                
                if (!string.IsNullOrEmpty(propertyName))
                {
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(propertyName, ListSortDirection.Ascending));
                }
            }
		}
	}

	public class FolderInfo : INotifyPropertyChanged, IAssetItem
    {
        private System.Windows.Media.Brush _backgroundColor = System.Windows.Media.Brushes.Transparent;
        private string _gameName;

        public string GameName 
        { 
            get => _gameName;
            set
            {
                // Remove TitleID portion from the game name if present
                if (value != null)
                {
                    var titleIdPattern = @"\s*\([A-Fa-f0-9]{5,8}\)\s*";
                    _gameName = System.Text.RegularExpressions.Regex.Replace(value, titleIdPattern, "");
                }
                else
                {
                    _gameName = value;
                }
                OnPropertyChanged(nameof(GameName));
            }
        }
        public string TitleId { get; set; }
        public string Path { get; set; }

        // Store cache information for quick access
        public Dictionary<string, (string FilePath, string Hash, bool HasCache)> CachedAssets { get; set; }

        public System.Windows.Media.Brush BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    OnPropertyChanged(nameof(BackgroundColor));
                }
            }
        }

        // IAssetItem implementation
        string IAssetItem.Title => GameName;

        // Asset status properties
        public string Assets => CachedAssets?.Count(a => System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("GC", StringComparison.OrdinalIgnoreCase) ||
                                                       System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("BK", StringComparison.OrdinalIgnoreCase) ||
                                                       System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("GL", StringComparison.OrdinalIgnoreCase) ||
                                                       System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("SS", StringComparison.OrdinalIgnoreCase)).ToString() ?? "0";

        public string Boxart => CachedAssets?.Count(a => System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("GC", StringComparison.OrdinalIgnoreCase)).ToString() ?? "0";
        public string Back => CachedAssets?.Count(a => System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("BK", StringComparison.OrdinalIgnoreCase)).ToString() ?? "0";
        public string Banner => CachedAssets?.Count(a => System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("GL", StringComparison.OrdinalIgnoreCase)).ToString() ?? "0";
        public string Icon => CachedAssets?.Count(a => System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("GL", StringComparison.OrdinalIgnoreCase)).ToString() ?? "0";
        public string Screens => CachedAssets?.Count(a => System.IO.Path.GetFileName(a.Value.FilePath).StartsWith("SS", StringComparison.OrdinalIgnoreCase)).ToString() ?? "0";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FtpGameInfo
    {
        public string Title { get; set; }
        public string TitleId { get; set; }
        public string DatabaseId { get; set; }
    }
}
