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
using AuroraAssetEditor.Classes;
using AuroraAssetEditor.Models;
using AuroraAssetEditor.Controls;
using AuroraAssetEditor.Helpers;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using Image = System.Drawing.Image;
using WpfImage = System.Windows.Controls.Image;
using Size = System.Drawing.Size;

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
        private List<FtpGameInfo> _ftpGames = new List<FtpGameInfo>();

        public MainWindow(IEnumerable<string> args) {
            InitializeComponent();
            var ver = Assembly.GetAssembly(typeof(MainWindow)).GetName().Version;
            Title = string.Format(Title, ver.Major, ver.Minor, ver.Build);
            Icon = App.WpfIcon;
            
            // Initialize asset cache
            AssetCache.Initialize();
            
            DataContext = GlobalState.CurrentGame;
            GlobalState.GameChanged += OnGameChanged;  // Ensure the UI gets updated when the game changes

            // Set default assets path
            var defaultPath = @"C:\Users\larst\OneDrive\EMULATOR-AND-SYSTEM DATA\XBOX360\Tools\AuroraAssetEditor\Temp\# Completed";
            if (Directory.Exists(defaultPath))
            {
                AssetPathTextBox.Text = defaultPath;
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(defaultPath);
                    AssetPathTextBox.Text = defaultPath;
                }
                catch (Exception ex)
                {
                    SaveError(ex);
                }
            }

            // add support for TLS 1.1 and TLS 1.2
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol
                | (SecurityProtocolType)768 // TLS 1.1
                | (SecurityProtocolType)3072; // TLS 1.2

            OnlineAssetsTab.Content = new OnlineAssetsControl(this);
            _ftpAssetsControl = new FtpAssetsControl(this);
            FtpAssetsContainer.Content = _ftpAssetsControl;

            // Auto-load both lists if debug db exists
            if (File.Exists("content.debug.db"))
            {
                // Load local assets first
                if (!string.IsNullOrWhiteSpace(AssetPathTextBox.Text) && Directory.Exists(AssetPathTextBox.Text))
                {
                    LoadAssetsButton_Click(null, null);
                }
                
                // Then load FTP assets after a small delay
                var autoLoadWorker = new BackgroundWorker();
                autoLoadWorker.DoWork += (sender, e) =>
                {
                    Thread.Sleep(500); // Small delay to ensure local assets are loaded
                    Dispatcher.Invoke(() => _ftpAssetsControl.GetAssetsClick(null, null));
                };
                autoLoadWorker.RunWorkerAsync();
            }

            var startupWorker = new BackgroundWorker();
            startupWorker.DoWork += (sender, e) => {
                foreach(var arg in args.Where(File.Exists)) {
                    if(VerifyAuroraMagic(arg))
                        LoadAuroraAsset(arg);
                }
            };
            startupWorker.RunWorkerCompleted += (sender, e) => GlobalBusyIndicator.Visibility = Visibility.Collapsed;
            if(!args.Any())
                return;
            GlobalBusyIndicator.Visibility = Visibility.Visible;
            startupWorker.RunWorkerAsync();
        }

        internal static void SaveError(Exception ex) { File.AppendAllText("error.log", string.Format("[{0}]:{2}{1}{2}", DateTime.Now, ex, Environment.NewLine)); }

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
                var asset = new AuroraAsset.AssetFile(File.ReadAllBytes(filename));
                if(asset.HasBoxArt || asset.HasBackground || asset.HasIconBanner || asset.HasScreenshots) {
                    Dispatcher.Invoke(() => {
                        if(asset.HasBoxArt) {
                            var localBoxart = FindName("local_boxart") as WpfImage;
                            if (localBoxart != null)
                            {
                                var titleId = GlobalState.CurrentGame?.TitleId ?? "unknown";
                                var cachedImage = AssetCache.GetCachedImage(filename, titleId, "boxart");
                                if (cachedImage != null)
                                {
                                    localBoxart.Source = cachedImage;
                                }
                                else
                                {
                                    var boxartImage = asset.GetBoxart();
                                    AssetCache.CacheImage(boxartImage, filename, titleId, "boxart");
                                    localBoxart.Source = ConvertToImageSource(boxartImage);
                                }
                            }
                        }
                        if(asset.HasBackground) {
                            var localBackground = FindName("local_background") as WpfImage;
                            if (localBackground != null)
                            {
                                var titleId = GlobalState.CurrentGame?.TitleId ?? "unknown";
                                var cachedImage = AssetCache.GetCachedImage(filename, titleId, "background");
                                if (cachedImage != null)
                                {
                                    localBackground.Source = cachedImage;
                                }
                                else
                                {
                                    var backgroundImage = asset.GetBackground();
                                    AssetCache.CacheImage(backgroundImage, filename, titleId, "background");
                                    localBackground.Source = ConvertToImageSource(backgroundImage);
                                }
                            }
                        }
                        if(asset.HasIconBanner) {
                            var localIcon = FindName("local_icon") as WpfImage;
                            var localBanner = FindName("local_banner") as WpfImage;
                            var titleId = GlobalState.CurrentGame?.TitleId ?? "unknown";

                            if (localIcon != null)
                            {
                                var cachedIcon = AssetCache.GetCachedImage(filename, titleId, "icon");
                                if (cachedIcon != null)
                                {
                                    localIcon.Source = cachedIcon;
                                }
                                else
                                {
                                    var iconImage = asset.GetIcon();
                                    AssetCache.CacheImage(iconImage, filename, titleId, "icon");
                                    localIcon.Source = ConvertToImageSource(iconImage);
                                }
                            }

                            if (localBanner != null)
                            {
                                var cachedBanner = AssetCache.GetCachedImage(filename, titleId, "banner");
                                if (cachedBanner != null)
                                {
                                    localBanner.Source = cachedBanner;
                                }
                                else
                                {
                                    var bannerImage = asset.GetBanner();
                                    AssetCache.CacheImage(bannerImage, filename, titleId, "banner");
                                    localBanner.Source = ConvertToImageSource(bannerImage);
                                }
                            }
                        }
                        if(asset.HasScreenshots) {
                            var screenshots = asset.GetScreenshots();
                            var titleId = GlobalState.CurrentGame?.TitleId ?? "unknown";
                            
                            for (int i = 0; i < Math.Min(screenshots.Length, 5); i++)
                            {
                                var imageControl = FindName($"local_screenshot{i + 1}") as WpfImage;
                                if (imageControl != null)
                                {
                                    var cachedScreenshot = AssetCache.GetCachedImage(filename, titleId, $"screenshot{i + 1}");
                                    if (cachedScreenshot != null)
                                    {
                                        imageControl.Source = cachedScreenshot;
                                    }
                                    else
                                    {
                                        var screenshot = screenshots[i];
                                        AssetCache.CacheImage(screenshot, filename, titleId, $"screenshot{i + 1}");
                                        imageControl.Source = ConvertToImageSource(screenshot);
                                    }
                                }
                            }
                        }
                        VisualAssetsTab.IsSelected = true;
                    });
                }
                else {
                    MessageBox.Show($"ERROR: {filename} doesn't contain any assets", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch(Exception ex) {
                SaveError(ex);
                MessageBox.Show($"ERROR: While processing {filename}\n{ex.Message}\nSee error.log for more details about this error", "ERROR",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAssetOnClick(object sender, RoutedEventArgs e) {
            var ofd = new OpenFileDialog {
                                             Title = "Select Asset(s) to load",
                                             Filter = AssetFileFilter,
                                             FilterIndex = 5,
                                             Multiselect = true
                                         };
            if(ofd.ShowDialog() != true)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             foreach(var fileName in ofd.FileNames) {
                                 if(VerifyAuroraMagic(fileName))
                                     LoadAuroraAsset(fileName);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => GlobalBusyIndicator.Visibility = Visibility.Collapsed;
            bw.RunWorkerAsync();
        }

        private static bool VerifyAuroraMagic(string fileName) {
            using(var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using(var br = new BinaryReader(stream))
                    return br.ReadUInt32() == 0x41455852; /* RXEA in LittleEndian format */
            }
        }

        private void CreateNewOnClick(object sender, RoutedEventArgs e) {
            ClearLocalControls();
        }

        private void ExitOnClick(object sender, RoutedEventArgs e) { Close(); }

        internal void OnDragEnter(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop) && (e.AllowedEffects & DragDropEffects.Copy) == DragDropEffects.Copy)
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None; // Ignore this one
        }

        private void ClearCurrentGame_Click(object sender, RoutedEventArgs e)
        {
            var NewGame = new Game
            {
                Title = string.Empty,
                TitleId = string.Empty,
                DbId = string.Empty,
                IsGameSelected = false,
            };
            GlobalState.CurrentGame = NewGame;
        }

        private void CopyTitleIdToClipboard_Click(object sender, RoutedEventArgs e)
        {
            string titleId = GlobalState.CurrentGame.TitleId;
            if (!string.IsNullOrEmpty(titleId))
            {
                Clipboard.SetText(titleId);
            }
        }

        private void CopyDbIdToClipboard_Click(object sender, RoutedEventArgs e)
        {
            string DbID = GlobalState.CurrentGame.DbId;
            if (!string.IsNullOrEmpty(DbID))
            {
                Clipboard.SetText(DbID);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GlobalState.GameChanged -= OnGameChanged;
        }

        private void MenuItem_Click(Object sender, RoutedEventArgs e)
        {
        }

        private void ClearLocalControls()
        {
            // Clear all local image controls
            var controls = new[] { "local_boxart", "local_background", "local_banner", "local_icon" };
            foreach (var control in controls)
            {
                var imageControl = FindName(control) as WpfImage;
                if (imageControl != null)
                {
                    imageControl.Source = null;
                }
            }

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
            using (var ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                return bi;
            }
        }

        internal void UpdateFtpGames(List<FtpGameInfo> ftpGames)
        {
            _ftpGames = ftpGames;
        }

        internal void UpdateMatchingColors()
        {
            // This method can be implemented later if needed
        }

        internal void Reset()
        {
            ClearLocalControls();
        }

        internal void Load(Image img, bool isIcon = false)
        {
            if (img == null) return;

            var imageSource = ConvertToImageSource(img);
            
            if (isIcon)
            {
                var localIcon = FindName("local_icon") as WpfImage;
                if (localIcon != null)
                {
                    localIcon.Source = imageSource;
                }
            }
            else
            {
                // Find first empty screenshot slot
                for (int i = 1; i <= 5; i++)
                {
                    var imageControl = FindName($"local_screenshot{i}") as WpfImage;
                    if (imageControl != null && imageControl.Source == null)
                    {
                        imageControl.Source = imageSource;
                        break;
                    }
                }
            }
        }

        private void LoadAssetsButton_Click(object sender, RoutedEventArgs e)
        {
            var path = AssetPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Please enter a valid directory path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Dispatcher.Invoke(() => {
                ListViewBusyIndicator.Visibility = Visibility.Visible;
                FolderListView.ItemsSource = null; // Clear current items
            });

            var worker = new BackgroundWorker();
            worker.DoWork += (s, args) =>
            {
                try
                {
                    var directories = Directory.GetDirectories(path);
                    var folders = directories
                        .Select(dir => new DirectoryInfo(dir))
                        .Select(dirInfo =>
                        {
                            var folder = new FolderInfo
                            {
                                GameName = dirInfo.Name,
                                TitleId = GetTitleIdFromFolder(dirInfo),
                                Assets = CountFiles(dirInfo, "*.asset"),
                                Boxart = CountFiles(dirInfo, "boxart*.png"),
                                Back = CountFiles(dirInfo, "background*.png"),
                                Banner = CountFiles(dirInfo, "banner*.png"),
                                Icon = CountFiles(dirInfo, "icon*.png"),
                                Screens = CountFiles(dirInfo, "screenshot*.png")
                            };

                            // Pre-cache the assets information
                            if (!string.IsNullOrEmpty(folder.TitleId))
                            {
                                folder.CachedAssets = AssetCache.CheckFolderCache(dirInfo.FullName, folder.TitleId);
                            }

                            return folder;
                        })
                        .OrderBy(f => f.GameName)
                        .ToList();

                    // Post-process to set background colors only for matching TitleIDs
                    foreach (var folder in folders)
                    {
                        if (!string.IsNullOrEmpty(folder.TitleId) && 
                            _ftpGames.Any(f => f.TitleId?.Equals(folder.TitleId, StringComparison.OrdinalIgnoreCase) == true))
                        {
                            Dispatcher.Invoke(() => {
                                folder.BackgroundColor = new SolidColorBrush(Colors.LightGreen);
                            });
                        }
                    }

                    args.Result = folders;
                }
                catch (Exception ex)
                {
                    SaveError(ex);
                    args.Result = ex;
                }
            };

            worker.RunWorkerCompleted += (s, args) =>
            {
                Dispatcher.Invoke(() => {
                    if (args.Result is Exception ex)
                    {
                        MessageBox.Show($"Error loading assets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (args.Result is List<FolderInfo> folders)
                    {
                        FolderListView.ItemsSource = folders;
                    }

                    ListViewBusyIndicator.Visibility = Visibility.Collapsed;
                });
            };

            worker.RunWorkerAsync();
        }

        private string CountFiles(DirectoryInfo dirInfo, string pattern)
        {
            try
            {
                int count = dirInfo.GetFiles(pattern, SearchOption.AllDirectories).Length;
                return count > 0 ? count.ToString() : "";
            }
            catch (Exception ex)
            {
                SaveError(ex);
                return "";
            }
        }

        private string GetTitleIdFromFolder(DirectoryInfo dirInfo)
        {
            // Try to find a title ID in brackets [XXXXX]
            var match = System.Text.RegularExpressions.Regex.Match(dirInfo.Name, @"\[([A-Fa-f0-9]{5,8})\]");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpper();
            }

            // Try to find a title ID at the start of the name (common format)
            match = System.Text.RegularExpressions.Regex.Match(dirInfo.Name, @"^([A-Fa-f0-9]{5,8})");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpper();
            }

            // Try to find a title ID anywhere in the name
            match = System.Text.RegularExpressions.Regex.Match(dirInfo.Name, @"[^A-Fa-f0-9]([A-Fa-f0-9]{5,8})[^A-Fa-f0-9]");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpper();
            }

            return "";
        }

        internal void DragDrop(object sender, DragEventArgs e)
        {
            // This method can be implemented later if needed
        }

        private void FileOpening(object sender, ContextMenuEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void SaveAllAssetsFtpOnClick(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void EditMenuOpened(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void ShowFtpAssets(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void TestColoring_Click(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void TabChanged(object sender, SelectionChangedEventArgs e)
        {
            // Implementation can be added later if needed
        }

        private void LocalTitleFilterChanged(object sender, TextChangedEventArgs e)
        {
            ApplyLocalFilters();
        }

        private void LocalTitleIdFilterChanged(object sender, TextChangedEventArgs e)
        {
            ApplyLocalFilters();
        }

        private void ApplyLocalFilters()
        {
            if (FolderListView?.ItemsSource == null) return;

            var view = CollectionViewSource.GetDefaultView(FolderListView.ItemsSource);
            if (view == null) return;

            var titleFilter = LocalTitleFilterBox?.Text?.ToLower() ?? "";
            var titleIdFilter = LocalTitleIdFilterBox?.Text?.ToLower() ?? "";

            view.Filter = item =>
            {
                if (item is FolderInfo folder)
                {
                    if (string.IsNullOrWhiteSpace(titleFilter) && string.IsNullOrWhiteSpace(titleIdFilter))
                        return true;

                    if (!string.IsNullOrWhiteSpace(titleFilter) && !string.IsNullOrWhiteSpace(titleIdFilter))
                        return folder.GameName.ToLower().Contains(titleFilter) && 
                               folder.TitleId.ToLower().Contains(titleIdFilter);

                    if (!string.IsNullOrWhiteSpace(titleFilter))
                        return folder.GameName.ToLower().Contains(titleFilter);

                    return folder.TitleId.ToLower().Contains(titleIdFilter);
                }
                return false;
            };
        }

        private void FolderListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedFolder = FolderListView.SelectedItem as FolderInfo;
            if (selectedFolder == null || selectedFolder.CachedAssets == null) return;

            // Clear existing assets
            ClearLocalControls();

            // Update the current game info
            var newGame = new Game
            {
                Title = selectedFolder.GameName,
                TitleId = selectedFolder.TitleId,
                IsGameSelected = true
            };
            GlobalState.CurrentGame = newGame;

            try
            {
                // Load all cached assets
                foreach (var cachedAsset in selectedFolder.CachedAssets)
                {
                    var assetType = cachedAsset.Key;
                    var (filePath, hash) = cachedAsset.Value;
                    var cachePath = AssetCache.GetCachePath(selectedFolder.TitleId, assetType, hash);

                    if (File.Exists(cachePath))
                    {
                        var image = AssetCache.LoadImageFromCache(cachePath);
                        if (image != null)
                        {
                            var control = FindName($"local_{assetType}") as WpfImage;
                            if (control != null)
                            {
                                control.Source = image;
                            }
                        }
                    }
                }

                VisualAssetsTab.IsSelected = true;
            }
            catch (Exception ex)
            {
                SaveError(ex);
                MessageBox.Show($"Error loading assets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            // Implementation can be added later if needed
        }
    }

    public class FolderInfo : INotifyPropertyChanged
    {
        private System.Windows.Media.Brush _backgroundColor = System.Windows.Media.Brushes.Transparent;

        public string GameName { get; set; }
        public string TitleId { get; set; }
        public string Assets { get; set; }
        public string Boxart { get; set; }
        public string Back { get; set; }
        public string Banner { get; set; }
        public string Icon { get; set; }
        public string Screens { get; set; }

        // Store cache information for quick access
        public Dictionary<string, (string FilePath, string Hash)> CachedAssets { get; set; }

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

        public string Title => GameName;

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
