//
//  FtpAssetsControl.xaml.cs
//  AuroraAssetEditor
//
//  Created by Swizzy on 13/05/2015
//  Copyright (c) 2015 Swizzy. All rights reserved.

namespace AuroraAssetEditor.Controls {
    using System;
    using System.ComponentModel;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using AuroraAssetEditor.Models;
    using Classes;
    using Helpers;
    using Image = System.Drawing.Image;
    using WpfImage = System.Windows.Controls.Image;

    /// <summary>
    ///     Interaction logic for FtpAssetsControl.xaml
    /// </summary>
    public partial class FtpAssetsControl {
        private readonly ThreadSafeObservableCollection<AuroraDbManager.ContentItem> _assetsList = new ThreadSafeObservableCollection<AuroraDbManager.ContentItem>();
        private readonly CollectionViewSource _assetsViewSource = new CollectionViewSource();
        private readonly ICollectionView _assetView;
        private readonly MainWindow _main;
        private byte[] _buffer;
        private bool _isBusy, _isError;

        public FtpAssetsControl(MainWindow main) {
            InitializeComponent();
            _assetsViewSource.Source = _assetsList;
            _main = main;
            App.FtpOperations.StatusChanged += (sender, args) => Dispatcher.Invoke(new Action(() => Status.Text = args.StatusMessage));
            FtpAssetsBox.ItemsSource = _assetView = _assetsViewSource.View;
            if(!App.FtpOperations.HaveSettings) {
                var ip = GetActiveIp();
                var index = ip.LastIndexOf('.');
                if(ip.Length > 0 && index > 0)
                    IpBox.Text = ip.Substring(0, index + 1);
            }
            else {
                IpBox.Text = App.FtpOperations.IpAddress;
                UserBox.Text = App.FtpOperations.Username;
                PassBox.Text = App.FtpOperations.Password;
                PortBox.Text = App.FtpOperations.Port;
            }
        }

        private static string GetActiveIp() {
            foreach(var unicastAddress in
                NetworkInterface.GetAllNetworkInterfaces().Where(f => f.OperationalStatus == OperationalStatus.Up).Select(f => f.GetIPProperties()).Where(
                                                                                                                                                          ipInterface =>
                                                                                                                                                          ipInterface.GatewayAddresses.Count > 0)
                                .SelectMany(
                                            ipInterface =>
                                            ipInterface.UnicastAddresses.Where(
                                                                               unicastAddress =>
                                                                               (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork) && (unicastAddress.IPv4Mask.ToString() != "0.0.0.0")))
                )
                return unicastAddress.Address.ToString();
            return "";
        }

        private void TestConnectionClick(object sender, RoutedEventArgs e) {
            var ip = IpBox.Text;
            var user = UserBox.Text;
            var pass = PassBox.Text;
            var port = PortBox.Text;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => App.FtpOperations.TestConnection(ip, user, pass, port);
            bw.RunWorkerCompleted += (o, args) => _main.GlobalBusyIndicator.Visibility = Visibility.Collapsed;
            _main.GlobalBusyIndicator.Visibility = Visibility.Visible;
            Status.Text = string.Format("Running a connection test to {0}", IpBox.Text);
            bw.RunWorkerAsync();
        }

        private void SaveSettingsClick(object sender, RoutedEventArgs e) { App.FtpOperations.SaveSettings(IpBox.Text, UserBox.Text, PassBox.Text, PortBox.Text); }

        private void FtpAssetsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FtpAssetsBox.SelectedItem is Classes.AuroraDbManager.ContentItem selectedAsset)
            {
                var newGame = new Game
                {
                    Title = selectedAsset.TitleName,
                    TitleId = selectedAsset.TitleId,
                    DbId = selectedAsset.DatabaseId,
                    IsGameSelected = true
                };

                GlobalState.CurrentGame = newGame;
            }
        }

        public void GetAssetsClick(object sender, RoutedEventArgs e) {
            _assetsList.Clear();
            
            var worker = new BackgroundWorker();
            worker.DoWork += (o, args) => {
                try {
                    string dbPath;
                    
                    // Check for local debug content.debug.db first
                    if (File.Exists("content.debug.db"))
                    {
                        dbPath = "content.debug.db";
                        Dispatcher.Invoke(() => Status.Text = "Using local content.debug.db file...");
                    }
                    else
                    {
                        // Fall back to FTP if no local file exists
                        if (!App.FtpOperations.HaveSettings)
                        {
                            args.Result = false;
                            return;
                        }
                            
                        dbPath = Path.Combine(Path.GetTempPath(), "AuroraAssetEditor.db");
                        if (!App.FtpOperations.DownloadContentDb(dbPath))
                        {
                            args.Result = false;
                            return;
                        }
                    }
                    
                    foreach(var title in AuroraDbManager.GetDbTitles(dbPath))
                        _assetsList.Add(title);
                    args.Result = true;
                }
                catch(Exception ex) {
                    MainWindow.SaveError(ex);
                    args.Result = false;
                }
            };
            
            worker.RunWorkerCompleted += (o, args) => {
                _main.GlobalBusyIndicator.Visibility = Visibility.Collapsed;
                if((bool)args.Result) {
                    Status.Text = "Finished loading assets information successfully...";
                    
                    // First update the FTP games list
                    var ftpGames = _assetsList.Select(item => new FtpGameInfo {
                        Title = item.TitleName,
                        TitleId = item.TitleId,
                        DatabaseId = item.DatabaseId
                    }).ToList();
                    
                    _main.UpdateFtpGames(ftpGames);
                    
                    // Then update the colors
                    _main.UpdateMatchingColors();
                }
                else {
                    Status.Text = "There was an error, check error.log for more information...";
                }
            };
            
            _main.GlobalBusyIndicator.Visibility = Visibility.Visible;
            Status.Text = "Loading assets information...";
            worker.RunWorkerAsync();
        }

        private void ProcessAsset(Task task, bool shouldHideWhenDone = true) {
            _isError = false;
            AuroraDbManager.ContentItem asset = null;
            Dispatcher.InvokeIfRequired(() => asset = FtpAssetsBox.SelectedItem as AuroraDbManager.ContentItem, DispatcherPriority.Normal);
            if(asset == null)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += (sender, args) => {
                try {
                    switch(task) {
                        case Task.GetBoxart:
                            _buffer = asset.GetBoxart();
                            break;
                        case Task.GetBackground:
                            _buffer = asset.GetBackground();
                            break;
                        case Task.GetIconBanner:
                            _buffer = asset.GetIconBanner();
                            break;
                        case Task.GetScreenshots:
                            _buffer = asset.GetScreenshots();
                            break;
                        case Task.SetBoxart:
                            asset.SaveAsBoxart(_buffer);
                            break;
                        case Task.SetBackground:
                            asset.SaveAsBackground(_buffer);
                            break;
                        case Task.SetIconBanner:
                            asset.SaveAsIconBanner(_buffer);
                            break;
                        case Task.SetScreenshots:
                            asset.SaveAsScreenshots(_buffer);
                            break;
                    }
                    args.Result = true;
                }
                catch(Exception ex) {
                    MainWindow.SaveError(ex);
                    args.Result = false;
                }
            };
            bw.RunWorkerCompleted += (sender, args) => {
                if(shouldHideWhenDone)
                    Dispatcher.InvokeIfRequired(() => _main.GlobalBusyIndicator.Visibility = Visibility.Collapsed, DispatcherPriority.Normal);
                var isGet = true;
                if((bool)args.Result) {
                    if(_buffer.Length > 0) {
                        var aurora = new AuroraAsset.AssetFile(_buffer);
                        switch(task) {
                            case Task.GetBoxart:
                                if (aurora.HasBoxArt)
                                {
                                    var localBoxart = _main.FindName("local_boxart") as WpfImage;
                                    if (localBoxart != null)
                                    {
                                        localBoxart.Source = ConvertToImageSource(aurora.GetBoxart());
                                    }
                                }
                                break;
                            case Task.GetBackground:
                                if (aurora.HasBackground)
                                {
                                    var localBackground = _main.FindName("local_background") as WpfImage;
                                    if (localBackground != null)
                                    {
                                        localBackground.Source = ConvertToImageSource(aurora.GetBackground());
                                    }
                                }
                                break;
                            case Task.GetIconBanner:
                                if (aurora.HasIconBanner)
                                {
                                    var localIcon = _main.FindName("local_icon") as WpfImage;
                                    var localBanner = _main.FindName("local_banner") as WpfImage;
                                    if (localIcon != null)
                                    {
                                        localIcon.Source = ConvertToImageSource(aurora.GetIcon());
                                    }
                                    if (localBanner != null)
                                    {
                                        localBanner.Source = ConvertToImageSource(aurora.GetBanner());
                                    }
                                }
                                break;
                            case Task.GetScreenshots:
                                if (aurora.HasScreenshots)
                                {
                                    var screenshots = aurora.GetScreenshots();
                                    for (int i = 0; i < Math.Min(screenshots.Length, 5); i++)
                                    {
                                        var imageControl = _main.FindName($"local_screenshot{i + 1}") as WpfImage;
                                        if (imageControl != null)
                                        {
                                            imageControl.Source = ConvertToImageSource(screenshots[i]);
                                        }
                                    }
                                }
                                break;
                            default:
                                isGet = false;
                                break;
                        }
                    }
                    if(shouldHideWhenDone && isGet)
                        Dispatcher.InvokeIfRequired(() => Status.Text = "Finished grabbing assets from FTP", DispatcherPriority.Normal);
                    else if(shouldHideWhenDone)
                        Dispatcher.InvokeIfRequired(() => Status.Text = "Finished saving assets to FTP", DispatcherPriority.Normal);
                }
                else {
                    switch(task) {
                        case Task.GetBoxart:
                        case Task.GetBackground:
                        case Task.GetIconBanner:
                        case Task.GetScreenshots:
                            break;
                        default:
                            isGet = false;
                            break;
                    }
                    if(isGet)
                        Dispatcher.InvokeIfRequired(() => Status.Text = "Failed getting asset data... See error.log for more information...", DispatcherPriority.Normal);
                    else
                        Dispatcher.InvokeIfRequired(() => Status.Text = "Failed saving asset data... See error.log for more information...", DispatcherPriority.Normal);
                    _isError = true;
                }
                _isBusy = false;
            };
            Dispatcher.InvokeIfRequired(() => _main.GlobalBusyIndicator.Visibility = Visibility.Visible, DispatcherPriority.Normal);
            _isBusy = true;
            bw.RunWorkerAsync();
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

        private void GetBoxartClick(object sender, RoutedEventArgs e) { ProcessAsset(Task.GetBoxart); }

        private void GetBackgroundClick(object sender, RoutedEventArgs e) { ProcessAsset(Task.GetBackground); }

        private void GetIconBannerClick(object sender, RoutedEventArgs e) { ProcessAsset(Task.GetIconBanner); }

        private void GetScreenshotsClick(object sender, RoutedEventArgs e) { ProcessAsset(Task.GetScreenshots); }

        private void GetFtpAssetsClick(object sender, RoutedEventArgs e) {
            if (_isBusy) return;
            GetBoxartClick(sender, e);
            GetBackgroundClick(sender, e);
            GetIconBannerClick(sender, e);
            GetScreenshotsClick(sender, e);
        }

        private void SaveFtpAssetsClick(object sender, RoutedEventArgs e) {
            if (_isBusy || _isError) return;
            SaveBoxartClick(sender, e);
            SaveBackgroundClick(sender, e);
            SaveIconBannerClick(sender, e);
            SaveScreenshotsClick(sender, e);
        }

        private void SaveBoxartClick(object sender, RoutedEventArgs e) {
            var localBoxart = _main.FindName("local_boxart") as WpfImage;
            if (localBoxart?.Source != null) {
                // Implementation needed
            }
        }

        private void SaveBackgroundClick(object sender, RoutedEventArgs e) {
            var localBackground = _main.FindName("local_background") as WpfImage;
            if (localBackground?.Source != null) {
                // Implementation needed
            }
        }

        private void SaveIconBannerClick(object sender, RoutedEventArgs e) {
            var localIcon = _main.FindName("local_icon") as WpfImage;
            var localBanner = _main.FindName("local_banner") as WpfImage;
            if (localIcon?.Source != null || localBanner?.Source != null) {
                // Implementation needed
            }
        }

        private void SaveScreenshotsClick(object sender, RoutedEventArgs e) {
            // Implementation needed
        }

        private void RemoveFtpAssetsClick(object sender, RoutedEventArgs e) {
            var asset = FtpAssetsBox.SelectedItem as AuroraDbManager.ContentItem;
            if (asset == null) return;
            
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                try {
                    // Remove each type of asset
                    asset.SaveAsBoxart(new byte[0]);
                    asset.SaveAsBackground(new byte[0]);
                    asset.SaveAsIconBanner(new byte[0]);
                    asset.SaveAsScreenshots(new byte[0]);
                    args.Result = true;
                }
                catch (Exception ex) {
                    MainWindow.SaveError(ex);
                    args.Result = false;
                }
            };
            bw.RunWorkerCompleted += (o, args) => {
                _main.GlobalBusyIndicator.Visibility = Visibility.Collapsed;
                if ((bool)args.Result)
                    Status.Text = "Finished removing assets from FTP";
                else
                    Status.Text = "Failed removing assets from FTP... See error.log for more information...";
            };
            _main.GlobalBusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
        }

        private void RemoveBoxartClick(object sender, RoutedEventArgs e) {
            var localBoxart = _main.FindName("local_boxart") as WpfImage;
            if (localBoxart != null) {
                localBoxart.Source = null;
            }
        }

        private void RemoveBackgroundClick(object sender, RoutedEventArgs e) {
            var localBackground = _main.FindName("local_background") as WpfImage;
            if (localBackground != null) {
                localBackground.Source = null;
            }
        }

        private void RemoveIconBannerClick(object sender, RoutedEventArgs e) {
            var localIcon = _main.FindName("local_icon") as WpfImage;
            var localBanner = _main.FindName("local_banner") as WpfImage;
            if (localIcon != null) {
                localIcon.Source = null;
            }
            if (localBanner != null) {
                localBanner.Source = null;
            }
        }

        private void RemoveScreenshotsClick(object sender, RoutedEventArgs e) {
            for (int i = 1; i <= 5; i++) {
                var imageControl = _main.FindName($"local_screenshot{i}") as WpfImage;
                if (imageControl != null) {
                    imageControl.Source = null;
                }
            }
        }

        private void TitleFilterChanged(Object sender, TextChangedEventArgs e) => FiltersChanged(TitleFilterBox.Text, TitleIdFilterBox.Text);

        private void TitleIdFilterChanged(Object sender, TextChangedEventArgs e) => FiltersChanged(TitleFilterBox.Text, TitleIdFilterBox.Text);

        private void FiltersChanged(string titleFilter, string titleIdFilter)
        {
            _assetView.Filter = item =>
            {
                var contentItem = item as AuroraDbManager.ContentItem;
                if (contentItem == null)
                    return false;
                if (string.IsNullOrWhiteSpace(titleFilter) && string.IsNullOrWhiteSpace(titleIdFilter))
                    return true;
                if (!string.IsNullOrWhiteSpace(titleFilter) && !string.IsNullOrWhiteSpace(titleIdFilter))
                    return contentItem.TitleName.ToLower().Contains(titleFilter.ToLower()) && contentItem.TitleId.ToLower().Contains(titleIdFilter.ToLower());
                if (!string.IsNullOrWhiteSpace(titleFilter))
                    return contentItem.TitleName.ToLower().Contains(titleFilter.ToLower());
                return contentItem.TitleId.ToLower().Contains(titleIdFilter.ToLower());
            };
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            if (headerClicked?.Column is GridViewColumn column)
            {
                var binding = column.DisplayMemberBinding as Binding;
                string propertyName = binding?.Path?.Path;
                
                if (!string.IsNullOrEmpty(propertyName))
                {
                    ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(FtpAssetsBox.ItemsSource);
                    if (view.SortDescriptions.Count > 0 &&
                        view.SortDescriptions[0].PropertyName == propertyName)
                    {
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new SortDescription(propertyName, ListSortDirection.Descending));
                    }
                    else
                    {
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new SortDescription(propertyName, ListSortDirection.Ascending));
                    }
                }
            }
        }

        private void FtpAssetsBoxContextOpening(object sender, ContextMenuEventArgs e)
        {
            if (FtpAssetsBox.SelectedItem == null)
                e.Handled = true;
        }

        private enum Task {
            GetBoxart,
            GetBackground,
            GetIconBanner,
            GetScreenshots,
            SetBoxart,
            SetBackground,
            SetIconBanner,
            SetScreenshots,
        }
    }
}
