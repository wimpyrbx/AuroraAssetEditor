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
                        switch (assetType)
                        {
                            case "GC":
                                var localBoxart = FindName("local_boxart") as WpfImage;
                                if (localBoxart != null)
                                {
                                    if (assetInfo.Value.HasCache)
                                    {
                                        localBoxart.Source = AssetCache.LoadImageFromCache(
                                            AssetCache.GetCachePath(titleId, "boxart", assetInfo.Value.Hash));
                                    }
                                    else
                                    {
                                        ProcessAndCacheAsset(filename, titleId, "boxart", assetInfo.Value.Hash, localBoxart);
                                        selectedFolder.CachedAssets[assetInfo.Key] = 
                                            (assetInfo.Value.FilePath, assetInfo.Value.Hash, true);
                                    }
                                }
                                break;

                            case "BK":
                                var localBackground = FindName("local_background") as WpfImage;
                                if (localBackground != null)
                                {
                                    if (assetInfo.Value.HasCache)
                                    {
                                        localBackground.Source = AssetCache.LoadImageFromCache(
                                            AssetCache.GetCachePath(titleId, "background", assetInfo.Value.Hash));
                                    }
                                    else
                                    {
                                        ProcessAndCacheAsset(filename, titleId, "background", assetInfo.Value.Hash, localBackground);
                                        selectedFolder.CachedAssets[assetInfo.Key] = 
                                            (assetInfo.Value.FilePath, assetInfo.Value.Hash, true);
                                    }
                                }
                                break;

                            case "GL":
                                var localBanner = FindName("local_banner") as WpfImage;
                                var localIcon = FindName("local_icon") as WpfImage;
                                if (localBanner != null || localIcon != null)
                                {
                                    if (assetInfo.Value.HasCache)
                                    {
                                        if (localBanner != null)
                                        {
                                            localBanner.Source = AssetCache.LoadImageFromCache(
                                                AssetCache.GetCachePath(titleId, "banner", assetInfo.Value.Hash));
                                        }
                                        if (localIcon != null)
                                        {
                                            localIcon.Source = AssetCache.LoadImageFromCache(
                                                AssetCache.GetCachePath(titleId, "icon", assetInfo.Value.Hash));
                                        }
                                    }
                                    else
                                    {
                                        ProcessAndCacheIconBanner(filename, titleId, assetInfo.Value.Hash, localBanner, localIcon);
                                        selectedFolder.CachedAssets[assetInfo.Key] = 
                                            (assetInfo.Value.FilePath, assetInfo.Value.Hash, true);
                                    }
                                }
                                break;

                            case "SS":
                                if (assetInfo.Value.HasCache)
                                {
                                    for (var i = 1; i <= 5; i++)
                                    {
                                        var imageControl = FindName($"local_screenshot{i}") as WpfImage;
                                        if (imageControl != null)
                                        {
                                            imageControl.Source = AssetCache.LoadImageFromCache(
                                                AssetCache.GetCachePath(titleId, $"screenshot{i}", assetInfo.Value.Hash));
                                        }
                                    }
                                }
                                else
                                {
                                    ProcessAndCacheScreenshots(filename, titleId, assetInfo.Value.Hash);
                                    selectedFolder.CachedAssets[assetInfo.Key] = 
                                        (assetInfo.Value.FilePath, assetInfo.Value.Hash, true);
                                }
                                break;
                        }
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

        private void ProcessAndCacheAsset(string filename, string titleId, string assetType, string hash, WpfImage imageControl)
        {
            var assetBytes = File.ReadAllBytes(filename);
            var asset = new AuroraAsset.AssetFile(assetBytes);
            Image image = null;

            switch (assetType)
            {
                case "boxart": image = asset.HasBoxArt ? asset.GetBoxart() : null; break;
                case "background": image = asset.HasBackground ? asset.GetBackground() : null; break;
            }

            if (image != null)
            {
                using (image)
                {
                    AssetCache.CacheImage(image, hash, titleId, assetType);
                    imageControl.Source = AssetCache.LoadImageFromCache(
                        AssetCache.GetCachePath(titleId, assetType, hash));
                }
            }
        }

        private void ProcessAndCacheIconBanner(string filename, string titleId, string hash, WpfImage bannerControl, WpfImage iconControl)
        {
            var assetBytes = File.ReadAllBytes(filename);
            var asset = new AuroraAsset.AssetFile(assetBytes);

            if (asset.HasIconBanner)
            {
                if (bannerControl != null)
                {
                    using (var bannerImage = asset.GetBanner())
                    {
                        if (bannerImage != null)
                        {
                            AssetCache.CacheImage(bannerImage, hash, titleId, "banner");
                            bannerControl.Source = AssetCache.LoadImageFromCache(
                                AssetCache.GetCachePath(titleId, "banner", hash));
                        }
                    }
                }

                if (iconControl != null)
                {
                    using (var iconImage = asset.GetIcon())
                    {
                        if (iconImage != null)
                        {
                            AssetCache.CacheImage(iconImage, hash, titleId, "icon");
                            iconControl.Source = AssetCache.LoadImageFromCache(
                                AssetCache.GetCachePath(titleId, "icon", hash));
                        }
                    }
                }
            }
        }

        private void ProcessAndCacheScreenshots(string filename, string titleId, string hash)
        {
            var assetBytes = File.ReadAllBytes(filename);
            var asset = new AuroraAsset.AssetFile(assetBytes);

            if (asset.HasScreenshots)
            {
                var screenshots = asset.GetScreenshots();
                for (var i = 0; i < screenshots.Length && i < 5; i++)
                {
                    if (screenshots[i] != null)
                    {
                        using (var screenshot = screenshots[i])
                        {
                            AssetCache.CacheImage(screenshot, hash, titleId, $"screenshot{i + 1}");
                            var imageControl = FindName($"local_screenshot{i + 1}") as WpfImage;
                            if (imageControl != null)
                            {
                                imageControl.Source = AssetCache.LoadImageFromCache(
                                    AssetCache.GetCachePath(titleId, $"screenshot{i + 1}", hash));
                            }
                        }
                    }
                }
            }
        }

        private void ClearLocalControls()
        {
            // Clear all image controls
            var localBoxart = FindName("local_boxart") as WpfImage;
            if (localBoxart != null) localBoxart.Source = null;

            var localBackground = FindName("local_background") as WpfImage;
            if (localBackground != null) localBackground.Source = null;

            var localBanner = FindName("local_banner") as WpfImage;
            if (localBanner != null) localBanner.Source = null;

            var localIcon = FindName("local_icon") as WpfImage;
            if (localIcon != null) localIcon.Source = null;

            // Clear screenshot controls
            for (int i = 1; i <= 5; i++)
            {
                var imageControl = FindName($"local_screenshot{i}") as WpfImage;
                if (imageControl != null)
                {
                    imageControl.Source = null;
                }
            }
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
            Debug.WriteLine("\n=== Starting LoadAssetsButton_Click ===");
            if (string.IsNullOrWhiteSpace(AssetPathTextBox.Text))
            {
                Debug.WriteLine("Error: Empty asset path");
                MessageBox.Show("Please enter a valid path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(AssetPathTextBox.Text))
            {
                Debug.WriteLine($"Error: Directory does not exist: {AssetPathTextBox.Text}");
                MessageBox.Show("Directory does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Debug.WriteLine($"Processing path: {AssetPathTextBox.Text}");

            // Save path to settings on UI thread
            Settings.Default.LastPath = AssetPathTextBox.Text;
            Settings.Default.Save();
            Debug.WriteLine("Saved path to settings");

            // Store the path we're going to process
            var pathToProcess = AssetPathTextBox.Text;

            // Clear current items and show loading indicator
            Debug.WriteLine("Clearing current items and showing loading indicator");
            if (LocalAssetList != null)
            {
                LocalAssetList.SetItems(null);
                Debug.WriteLine("Cleared LocalAssetList items");
            }
            else
            {
                Debug.WriteLine("Warning: LocalAssetList is null");
            }

            if (ListViewBusyIndicator != null)
            {
                ListViewBusyIndicator.Visibility = Visibility.Visible;
                Debug.WriteLine("Set ListViewBusyIndicator to visible");
            }
            else
            {
                Debug.WriteLine("Warning: ListViewBusyIndicator is null");
            }

            // Start background work
            Debug.WriteLine("Starting background work");
            Task.Run(() => 
            {
                try
                {
                    Debug.WriteLine("Background task started");
                    // Create list to store results
                    var results = new List<FolderInfo>();
                    
                    // Get all directories
                    Debug.WriteLine("Getting directories");
                    var folders = Directory.GetDirectories(pathToProcess);
                    Debug.WriteLine($"Found {folders.Length} folders to process");
                    
                    // Process each folder
                    foreach (var folder in folders)
                    {
                        Debug.WriteLine($"\nProcessing folder: {folder}");
                        var dirInfo = new DirectoryInfo(folder);
                        var titleId = ExtractTitleId(dirInfo);
                        Debug.WriteLine($"Extracted TitleId: {titleId}");
                        
                        // Create folder info without cached assets first
                        var folderInfo = new FolderInfo
                        {
                            GameName = dirInfo.Name,
                            TitleId = titleId,
                            Path = dirInfo.FullName
                        };
                        Debug.WriteLine($"Created FolderInfo - GameName: {folderInfo.GameName}, TitleId: {folderInfo.TitleId}");

                        // Add to results first
                        results.Add(folderInfo);

                        // Then try to cache assets if we have a title ID
                        if (!string.IsNullOrEmpty(titleId))
                        {
                            try
                            {
                                Debug.WriteLine("Checking folder cache");
                                folderInfo.CachedAssets = AssetCache.CheckFolderCache(dirInfo.FullName, titleId);
                                if (folderInfo.CachedAssets != null)
                                {
                                    Debug.WriteLine($"Found {folderInfo.CachedAssets.Count} cached assets");
                                }
                                else
                                {
                                    Debug.WriteLine("No cached assets found");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error checking folder cache: {ex.Message}");
                                SaveError(ex);
                            }
                        }
                    }

                    // Return to UI thread to update interface
                    Debug.WriteLine("\nReturning to UI thread to update interface");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            Debug.WriteLine("On UI thread");
                            if (LocalAssetList != null)
                            {
                                Debug.WriteLine("LocalAssetList is not null");
                                if (results != null && results.Any())
                                {
                                    Debug.WriteLine($"Setting {results.Count} items to LocalAssetList");
                                    LocalAssetList.SetItems(results);
                                    Debug.WriteLine("Items set successfully");
                                    LocalAssetList.RefreshView();
                                    Debug.WriteLine("View refreshed");
                                }
                                else
                                {
                                    Debug.WriteLine("Warning: No results to set");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("Error: LocalAssetList is null on UI thread");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error on UI thread: {ex.Message}");
                            MessageBox.Show($"Error setting items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        finally
                        {
                            if (ListViewBusyIndicator != null)
                            {
                                ListViewBusyIndicator.Visibility = Visibility.Collapsed;
                                Debug.WriteLine("Hidden ListViewBusyIndicator");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in background task: {ex.Message}");
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
            Debug.WriteLine("=== Finished LoadAssetsButton_Click method ===\n");
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
                var newGame = new Game
                {
                    Title = selectedFolder.GameName,
                    TitleId = selectedFolder.TitleId,
                    IsGameSelected = true
                };

                GlobalState.CurrentGame = newGame;

                // Load assets from cache if available
                if (selectedFolder.CachedAssets != null)
                {
                    // Create a copy of the values to avoid modification during enumeration
                    var assetsToLoad = selectedFolder.CachedAssets.Values.ToList();
                    foreach (var asset in assetsToLoad)
                    {
                        LoadAuroraAsset(asset.FilePath);
                    }
                }
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

        public string GameName { get; set; }
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
