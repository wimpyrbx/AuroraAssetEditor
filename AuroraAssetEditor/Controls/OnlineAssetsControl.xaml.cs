//
//  OnlineAssetsControl.xaml.cs
//  AuroraAssetEditor
//
//  Created by Swizzy on 10/05/2015
//  Copyright (c) 2015 Swizzy. All rights reserved.

namespace AuroraAssetEditor.Controls {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing.Imaging;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using AuroraAssetEditor.Helpers;
    using Classes;
    using System.Linq;
    using Image = System.Drawing.Image;

    /// <summary>
    ///     Interaction logic for OnlineAssetsControl.xaml
    /// </summary>
    public partial class OnlineAssetsControl {
        private enum OnlineAssetSources
        {
            XboxUnityOption = 0,
            ArchiveOption = 1,
            XboxComOption = 2
        }
        private readonly BackgroundControl _background;
        private readonly UIElement[] _backgroundMenu;
        private readonly UIElement[] _bannerMenu;
        private readonly BoxartControl _boxart;
        private readonly UIElement[] _coverMenu;
        private readonly IconBannerControl _iconBanner;
        private readonly UIElement[] _iconMenu;
        private readonly MainWindow _main;
        private readonly ScreenshotsControl _screenshots;
        private readonly UIElement[] _screenshotsMenu;
        private readonly BackgroundWorker _unityWorker = new BackgroundWorker();
        private readonly XboxAssetDownloader _xboxAssetDownloader = new XboxAssetDownloader();
        private readonly BackgroundWorker _xboxWorker = new BackgroundWorker();
        private BackgroundWorker _archiveWorker;
        private InternetArchiveDownloader _archiveDownloader;
        private Image _img;
        private string _keywords;
        private XboxLocale[] _locales;
        private uint _titleId;
        private XboxUnity.XboxUnityAsset[] _unityResult;
        private XboxTitleInfo[] _xboxResult;
        private InternetArchiveAsset[] _archiveResult;

        public OnlineAssetsControl(MainWindow main, BoxartControl boxart, BackgroundControl background, IconBannerControl iconBanner, ScreenshotsControl screenshots) {
            InitializeComponent();
            GlobalState.GameChanged += OnGameChanged;
            XboxAssetDownloader.StatusChanged += StatusChanged;
            _main = main;
            _boxart = boxart;
            _background = background;
            _iconBanner = iconBanner;
            _screenshots = screenshots;
            SourceBox.SelectedIndex = (int) OnlineAssetSources.XboxUnityOption;

            #region Xbox.com Locale worker

            var bw = new BackgroundWorker();
            bw.DoWork += LocaleWorkerDoWork;
            bw.RunWorkerCompleted += (sender, args) => {
                                         LocaleBox.ItemsSource = _locales;
                                         SourceBox.Items.Add("Xbox.com");
                                         var index = 0;
                                         for(var i = 0; i < _locales.Length; i++) {
                                             if(!_locales[i].Locale.Equals("en-us", StringComparison.InvariantCultureIgnoreCase))
                                                 continue;
                                             index = i;
                                             break;
                                         }
                                         LocaleBox.SelectedIndex = index;
                                     };
            bw.RunWorkerAsync();

            #endregion

            #region Unity Worker

            _unityWorker.DoWork += (o, args) => {
                                       try {
                                           _unityResult = XboxUnity.GetUnityCoverInfo(args.Argument.ToString());
                                           Dispatcher.Invoke(new Action(() => StatusMessage.Text = "Finished downloading asset information..."));
                                           args.Result = true;
                                       }
                                       catch(Exception ex) {
                                           MainWindow.SaveError(ex);
                                           Dispatcher.Invoke(new Action(() => StatusMessage.Text = "An error has occured, check error.log for more information..."));
                                           args.Result = false;
                                       }
                                   };
            _unityWorker.RunWorkerCompleted += (o, args) => {
                                                   if((bool)args.Result) {
                                                       ResultBox.ItemsSource = _unityResult;
                                                       SearchResultCount.Text = _unityResult.Length.ToString(CultureInfo.InvariantCulture);
                                                   }
                                                   else {
                                                       ResultBox.ItemsSource = null;
                                                       SearchResultCount.Text = "0";
                                                   }
                                               };

            #endregion

            #region Xbox.com Worker

            _xboxWorker.DoWork += (sender, args) => {
                                      try {
                                          _xboxResult = _keywords == null
                                                            ? _xboxAssetDownloader.GetTitleInfo(_titleId, args.Argument as XboxLocale)
                                                            : _xboxAssetDownloader.GetTitleInfo(_keywords, args.Argument as XboxLocale);
                                          Dispatcher.Invoke(new Action(() => StatusMessage.Text = "Finished downloading asset information..."));
                                          args.Result = true;
                                      }
                                      catch(Exception ex) {
                                          MainWindow.SaveError(ex);
                                          Dispatcher.Invoke(new Action(() => StatusMessage.Text = "An error has occured, check error.log for more information..."));
                                          args.Result = false;
                                      }
                                  };
            _xboxWorker.RunWorkerCompleted += (sender, args) => {
                                                  if((bool)args.Result) {
                                                      var disp = new List<XboxTitleInfo.XboxAssetInfo>();
                                                      foreach(var info in _xboxResult)
                                                          disp.AddRange(info.AssetsInfo);
                                                      ResultBox.ItemsSource = disp;
                                                      SearchResultCount.Text = disp.Count.ToString(CultureInfo.InvariantCulture);
                                                      DownloadAllButton.Visibility = Visibility.Visible;
                                                  }
                                                  else {
                                                      ResultBox.ItemsSource = null;
                                                      SearchResultCount.Text = "0";
                                                  }
                                              };

            #endregion

            #region Internet Archive Worker

            _archiveWorker = new BackgroundWorker();
            _archiveDownloader = new InternetArchiveDownloader();

            _archiveWorker.DoWork += (sender, args) =>
            {
                try
                {
                    uint titleId = (uint)args.Argument;
                    _archiveResult = _archiveDownloader.GetTitleInfo(titleId);
                    Dispatcher.Invoke(new Action(() => StatusMessage.Text = "Finished retrieving asset information..."));
                    args.Result = true;
                }
                catch (Exception ex)
                {
                    MainWindow.SaveError(ex);
                    Dispatcher.Invoke(new Action(() => StatusMessage.Text = "An error has occurred, check error.log for more information..."));
                    args.Result = false;
                }
            };

            _archiveWorker.RunWorkerCompleted += (sender, args) =>
            {
                if ((bool)args.Result)
                {
                    ResultBox.ItemsSource = _archiveResult;
                    SearchResultCount.Text = _archiveResult.Length.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    ResultBox.ItemsSource = null;
                    SearchResultCount.Text = "0";
                }
            };

            #endregion


            #region Cover Menu

            _coverMenu = new UIElement[] {
                                             new MenuItem {
                                                              Header = "Save cover to file"
                                                          },
                                             new MenuItem {
                                                              Header = "Set as cover"
                                                          }
                                         };
            ((MenuItem)_coverMenu[0]).Click += (sender, args) => MainWindow.SaveToFile(_img, "Select where to save the cover", "cover.png");
            ((MenuItem)_coverMenu[1]).Click += (sender, args) => _boxart.Load(_img);

            #endregion

            #region Icon Menu

            _iconMenu = new UIElement[] {
                                            new MenuItem {
                                                             Header = "Save icon to file"
                                                         },
                                            new MenuItem {
                                                             Header = "Set as icon"
                                                         }
                                        };
            ((MenuItem)_iconMenu[0]).Click += (sender, args) => MainWindow.SaveToFile(_img, "Select where to save the icon", "icon.png");
            ((MenuItem)_iconMenu[1]).Click += (sender, args) => _iconBanner.Load(_img, true);

            #endregion

            #region Banner Menu

            _bannerMenu = new UIElement[] {
                                              new MenuItem {
                                                               Header = "Save banner to file"
                                                           },
                                              new MenuItem {
                                                               Header = "Set as banner"
                                                           }
                                          };
            ((MenuItem)_bannerMenu[0]).Click += (sender, args) => MainWindow.SaveToFile(_img, "Select where to save the banner", "banner.png");
            ((MenuItem)_bannerMenu[1]).Click += (sender, args) => _iconBanner.Load(_img, false);

            #endregion

            #region Background Menu

            _backgroundMenu = new UIElement[] {
                                                  new MenuItem {
                                                                   Header = "Save background to file"
                                                               },
                                                  new MenuItem {
                                                                   Header = "Set as background"
                                                               }
                                              };
            ((MenuItem)_backgroundMenu[0]).Click += (sender, args) => MainWindow.SaveToFile(_img, "Select where to save the background", "background.png");
            ((MenuItem)_backgroundMenu[1]).Click += (sender, args) => _background.Load(_img);

            #endregion

            #region Screenshots Menu

            _screenshotsMenu = new UIElement[] {
                                                   new MenuItem {
                                                                    Header = "Save screenshot to file"
                                                                },
                                                   new MenuItem {
                                                                    Header = "Replace current screenshot"
                                                                },
                                                   new MenuItem {
                                                                    Header = "Add new screenshot"
                                                                }
                                               };
            ((MenuItem)_screenshotsMenu[0]).Click += (sender, args) => MainWindow.SaveToFile(_img, "Select where to save the screenshot", "screenshot.png");
            ((MenuItem)_screenshotsMenu[1]).Click += (sender, args) => _screenshots.Load(_img, true);
            ((MenuItem)_screenshotsMenu[2]).Click += (sender, args) => _screenshots.Load(_img, false);

            #endregion
        }

        private void LocaleWorkerDoWork(object sender, DoWorkEventArgs doWorkEventArgs) { _locales = XboxAssetDownloader.GetLocales(); }

        private void StatusChanged(object sender, StatusArgs e) { Dispatcher.Invoke(new Action(() => StatusMessage.Text = e.StatusMessage)); }

        private void OnGameChanged() {
            Dispatcher.Invoke(() =>
            {
                TitleIdBox.Text = GlobalState.CurrentGame.TitleId;
            });
        }

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e) { e.Handled = !uint.TryParse(e.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out _titleId); }

        private void SourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            LocaleGrid.Visibility = SourceBox.SelectedIndex == (int) OnlineAssetSources.XboxComOption ? Visibility.Visible : Visibility.Hidden;
            KeywordsButton.Visibility = SourceBox.SelectedIndex == (int) OnlineAssetSources.ArchiveOption ? Visibility.Hidden : Visibility.Visible;
            KeywordsBox.Visibility = SourceBox.SelectedIndex == (int)OnlineAssetSources.ArchiveOption ? Visibility.Hidden : Visibility.Visible;
            DownloadAllButton.Visibility = Visibility.Hidden;

        }

        private void ByTitleIdClick(object sender, RoutedEventArgs e)
        {
            uint.TryParse(TitleIdBox.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out _titleId);
            if (_unityWorker.IsBusy || _xboxWorker.IsBusy || _archiveWorker.IsBusy)
            {
                MessageBox.Show("Please wait for previous operation to complete!");
                return;
            }
            PreviewImg.Source = null;
            PreviewImg.ContextMenu.ItemsSource = null;
            _main.EditMenu.ItemsSource = null;
            _keywords = null;

            switch (SourceBox.SelectedIndex)
            {
                case 0:
                    StatusMessage.Text = "Downloading asset information...";
                    _unityWorker.RunWorkerAsync(_titleId.ToString("X08"));
                    break;
                case 2:
                    _xboxWorker.RunWorkerAsync(LocaleBox.SelectedItem);
                    break;
                case 1:
                    StatusMessage.Text = "Downloading cover from Internet Archive...";
                    _archiveWorker.RunWorkerAsync(_titleId);
                    break;
            }
        }

        private void ByKeywordsClick(object sender, RoutedEventArgs e) {
            if(_unityWorker.IsBusy || _xboxWorker.IsBusy) {
                MessageBox.Show("Please wait for previous operation to complete!");
                return;
            }
            PreviewImg.Source = null;
            PreviewImg.ContextMenu.ItemsSource = null;
            _main.EditMenu.ItemsSource = null;
            StatusMessage.Text = "Downloading asset information...";
            if(SourceBox.SelectedIndex == (int) OnlineAssetSources.XboxUnityOption)
                _unityWorker.RunWorkerAsync(KeywordsBox.Text);
            else {
                _keywords = KeywordsBox.Text;
                _xboxWorker.RunWorkerAsync(LocaleBox.SelectedItem);
            }
        }

        private void SetPreview(Image img, int maxWidth, int maxHeight) {
            PreviewImg.MaxHeight = maxHeight;
            PreviewBox.MaxHeight = maxHeight + 20;
            PreviewImg.MaxWidth = maxWidth;
            PreviewBox.MaxWidth = maxWidth + 20;
            _img = img;
            var bi = new BitmapImage();
            bi.BeginInit();
            var ms = new MemoryStream();
            img.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            bi.StreamSource = ms;
            bi.EndInit();
            PreviewImg.Source = bi;
        }

        private void ResultBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var unity = ResultBox.SelectedItem as XboxUnity.XboxUnityAsset;
            if(unity != null) {
                if(!unity.HaveAsset) {
                    PreviewImg.Source = null;
                    PreviewImg.ContextMenu.ItemsSource = null;
                    _main.EditMenu.ItemsSource = null;
                    StatusMessage.Text = "Downloading asset data...";
                    var bw = new BackgroundWorker();
                    bw.DoWork += (o, args) => {
                                     var asset = args.Argument as XboxUnity.XboxUnityAsset;
                                     if(asset != null)
                                         asset.GetCover();
                                 };
                    bw.RunWorkerCompleted += (o, args) => {
                                                 StatusMessage.Text = "Finished downloading asset data...";
                                                 ResultBox_SelectionChanged(null, null);
                                             };
                    bw.RunWorkerAsync(unity);
                }
                else {
                    SetPreview(unity.GetCover(), 900, 600);
                    PreviewImg.ContextMenu.ItemsSource = _coverMenu;
                    _main.EditMenu.ItemsSource = _coverMenu;
                }
            }
            else {
                var xbox = ResultBox.SelectedItem as XboxTitleInfo.XboxAssetInfo;
                if(xbox != null) {
                    if (!xbox.HaveAsset)
                    {
                        PreviewImg.Source = null;
                        StatusMessage.Text = "Downloading asset data...";
                        var bw = new BackgroundWorker();
                        bw.DoWork += (o, args) => {
                            var asset = args.Argument as XboxTitleInfo.XboxAssetInfo;
                            if (asset != null)
                                asset.GetAsset();
                        };
                        bw.RunWorkerCompleted += (o, args) => {
                            StatusMessage.Text = "Finished downloading asset data...";
                            ResultBox_SelectionChanged(null, null);
                        };
                        bw.RunWorkerAsync(xbox);
                        return;
                    }
                    switch (xbox.AssetType)
                    {
                        case XboxTitleInfo.XboxAssetType.Icon:
                            SetPreview(xbox.GetAsset().Image, 64, 64);
                            PreviewImg.ContextMenu.ItemsSource = _iconMenu;
                            _main.EditMenu.ItemsSource = _iconMenu;
                            break;
                        case XboxTitleInfo.XboxAssetType.Banner:
                            SetPreview(xbox.GetAsset().Image, 420, 96);
                            PreviewImg.ContextMenu.ItemsSource = _bannerMenu;
                            _main.EditMenu.ItemsSource = _bannerMenu;
                            break;
                        case XboxTitleInfo.XboxAssetType.Background:
                            SetPreview(xbox.GetAsset().Image, 1280, 720);
                            PreviewImg.ContextMenu.ItemsSource = _backgroundMenu;
                            _main.EditMenu.ItemsSource = _backgroundMenu;
                            break;
                        case XboxTitleInfo.XboxAssetType.Screenshot:
                            SetPreview(xbox.GetAsset().Image, 1000, 562);
                            PreviewImg.ContextMenu.ItemsSource = _screenshotsMenu;
                            _main.EditMenu.ItemsSource = _screenshotsMenu;
                            break;
                    }
                } else
                {
                    var archive = ResultBox.SelectedItem as InternetArchiveAsset;
                    if (archive != null)
                    {
                        if (!archive.HaveAsset)
                        {
                            PreviewImg.Source = null;
                            StatusMessage.Text = "Downloading cover...";
                            var bw = new BackgroundWorker();
                            bw.DoWork += (o, args) =>
                            {
                                var asset = args.Argument as InternetArchiveAsset;
                                if (asset != null)
                                    asset.GetCover();
                            };
                            bw.RunWorkerCompleted += (o, args) =>
                            {
                                StatusMessage.Text = "Finished downloading cover...";
                                ResultBox_SelectionChanged(null, null);
                            };
                            bw.RunWorkerAsync(archive);
                        }
                        else
                        {
                            var cover = archive.GetCover();
                            if (cover != null)
                            {
                                SetPreview(cover, 900, 600);
                                PreviewImg.ContextMenu.ItemsSource = _coverMenu;
                                _main.EditMenu.ItemsSource = _coverMenu;
                            }
                            else
                            {
                                PreviewImg.Source = null;
                                PreviewImg.ContextMenu.ItemsSource = null;
                                _main.EditMenu.ItemsSource = null;
                                StatusMessage.Text = "Failed to load cover image.";
                            }
                        }
                    }

                }

            }
        }

        private void DownloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultBox.ItemsSource == null) return;

            var assets = ResultBox.ItemsSource.OfType<XboxTitleInfo.XboxAssetInfo>().ToList();
            if (!assets.Any())
            {
                MessageBox.Show("No assets found to download.");
                return;
            }

            _background.Reset();
            _iconBanner.Reset();
            _screenshots.Reset();

            DownloadAllButton.IsEnabled = false;
            StatusMessage.Text = "Downloading all assets...";

            var bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;

            bw.DoWork += (s, args) =>
            {
                int total = assets.Count;
                int max_ss = 3;
                int current_ss = 0;
                int current = 0;

                foreach (var asset in assets)
                {
                    try
                    {
                        if (!asset.HaveAsset)
                        {
                            if (asset.AssetType == XboxTitleInfo.XboxAssetType.Screenshot)
                            {
                                if (current_ss >= max_ss)
                                {
                                    continue;
                                }
                                current_ss++;
                            }
                            asset.GetAsset();
                        }

                        var image = asset.GetAsset().Image;

                        bw.ReportProgress(
                            (int)((float)++current / total * 100),
                            new Tuple<XboxTitleInfo.XboxAssetType, Image>(asset.AssetType, image)
                        );
                    }
                    catch (Exception ex)
                    {
                        MainWindow.SaveError(ex);
                    }
                }
            };

            bw.ProgressChanged += (s, args) =>
            {
                var assetInfo = args.UserState as Tuple<XboxTitleInfo.XboxAssetType, Image>;
                if (assetInfo != null)
                {
                    // Apply the asset based on its type
                    switch (assetInfo.Item1)
                    {
                        case XboxTitleInfo.XboxAssetType.Background:
                            _background.Load(assetInfo.Item2);
                            break;
                        case XboxTitleInfo.XboxAssetType.Icon:
                            _iconBanner.Load(assetInfo.Item2, true);
                            break;
                        case XboxTitleInfo.XboxAssetType.Banner:
                            _iconBanner.Load(assetInfo.Item2, false);
                            break;
                        case XboxTitleInfo.XboxAssetType.Screenshot:
                            _screenshots.Load(assetInfo.Item2, false);
                            break;
                    }
                    StatusMessage.Text = $"Downloaded and applied {assetInfo.Item1} ({args.ProgressPercentage}%)";
                }
            };

            bw.RunWorkerCompleted += (s, args) =>
            {
                DownloadAllButton.IsEnabled = true;
                if (args.Error != null)
                {
                    StatusMessage.Text = "Error downloading some assets. Check error.log for details.";
                    MainWindow.SaveError(args.Error);
                }
                else
                {
                    StatusMessage.Text = "Finished downloading and applying all assets.";
                }
            };

            bw.RunWorkerAsync();
        }

        private void TitleIdBox_TextChanged(object sender, TextChangedEventArgs e) { TitleIdBox.Text = Regex.Replace(TitleIdBox.Text, "[^a-fA-F0-9]+", ""); }

        private void OnDragEnter(object sender, DragEventArgs e) { _main.OnDragEnter(sender, e); }

        private void OnDrop(object sender, DragEventArgs e) { _main.DragDrop(this, e); }
    }
}
