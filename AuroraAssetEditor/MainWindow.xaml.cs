﻿//
//  MainWindow.xaml.cs
//  AuroraAssetEditor
//
//  Created by Swizzy on 08/05/2015
//  Copyright (c) 2015 Swizzy. All rights reserved.

namespace AuroraAssetEditor {
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
    using Classes;
    using Models;
    using Controls;
    using Helpers;
    using Microsoft.Win32;
    using Ookii.Dialogs.Wpf;
    using Image = System.Drawing.Image;
    using Size = System.Drawing.Size;
    using System.ComponentModel;
    using System.Collections.ObjectModel;
    using System.Windows.Data;

    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        private const string AssetFileFilter =
            "Game Cover/Boxart Asset File(defaultFilename) (GC*.asset)|GC*.asset|Background Asset File(defaultFilename) (BK*.asset)|BK*.asset|Icon/Banner Asset File(defaultFilename) (GL*.asset)|GL*.asset|Screenshot Asset File(defaultFilename) (SS*.asset)|SS*.asset|Aurora Asset Files (*.asset)|*.asset|FSD Assets Files (*.assets)|*.assets|All Files(*)|*";

        private const string ImageFileFilter =
            "All Supported Images|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif;*.tiff;|BMP (*.bmp)|*.bmp|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|GIF (*.gif)|*.gif|TIFF (*.tif;*.tiff)|*.tiff;*.tif|PNG (*.png)|*.png|All Files|*";

        private readonly BackgroundControl _background;
        private readonly MenuItem[] _backgroundMenu;
        private readonly BoxartControl _boxart;
        private readonly MenuItem[] _boxartMenu;
        private readonly IconBannerControl _iconBanner;
        private readonly UIElement[] _iconBannerMenu;
        private readonly ScreenshotsControl _screenshots;
        private readonly MenuItem[] _screenshotsMenu;
        private readonly FtpAssetsControl _ftpAssetsControl;
        private List<FtpGameInfo> _ftpGames = new List<FtpGameInfo>();

        public MainWindow(IEnumerable<string> args) {
            InitializeComponent();
            var ver = Assembly.GetAssembly(typeof(MainWindow)).GetName().Version;
            Title = string.Format(Title, ver.Major, ver.Minor, ver.Build);
            Icon = App.WpfIcon;
            
            DataContext = GlobalState.CurrentGame;
            GlobalState.GameChanged += OnGameChanged;  // Ensure the UI gets updated when the game changes

            // add support for TLS 1.1 and TLS 1.2
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol
                | (SecurityProtocolType)768 // TLS 1.1
                | (SecurityProtocolType)3072; // TLS 1.2

            #region Boxart

            _boxart = new BoxartControl(this);
            BoxartTab.Content = _boxart;
            _boxartMenu = new[] {
                                    new MenuItem {
                                                     Header = "Save Cover To File"
                                                 },
                                    new MenuItem {
                                                     Header = "Select new Cover"
                                                 }
                                };
            _boxartMenu[0].Click += _boxart.SaveImageToFileOnClick;
            _boxartMenu[1].Click += _boxart.SelectNewCover;

            #endregion

            #region Background

            _background = new BackgroundControl(this);
            BackgroundTab.Content = _background;
            _backgroundMenu = new[] {
                                        new MenuItem {
                                                         Header = "Save Background To File"
                                                     },
                                        new MenuItem {
                                                         Header = "Select new Background"
                                                     }
                                    };
            _backgroundMenu[0].Click += _background.SaveImageToFileOnClick;
            _backgroundMenu[1].Click += _background.SelectNewBackground;

            #endregion

            #region Icon & Banner

            _iconBanner = new IconBannerControl(this);
            IconBannerTab.Content = _iconBanner;
            _iconBannerMenu = new UIElement[] {
                                                  new MenuItem {
                                                                   Header = "Save Icon To File"
                                                               },
                                                  new MenuItem {
                                                                   Header = "Select new Icon"
                                                               },
                                                  new Separator(),
                                                  new MenuItem {
                                                                   Header = "Save Banner To File"
                                                               },
                                                  new MenuItem {
                                                                   Header = "Select new Banner"
                                                               }
                                              };
            ((MenuItem)_iconBannerMenu[0]).Click += _iconBanner.SaveIconToFileOnClick;
            ((MenuItem)_iconBannerMenu[1]).Click += _iconBanner.SelectNewIcon;
            ((MenuItem)_iconBannerMenu[3]).Click += _iconBanner.SaveBannerToFileOnClick;
            ((MenuItem)_iconBannerMenu[4]).Click += _iconBanner.SelectNewBanner;

            #endregion

            #region Screenshots

            _screenshots = new ScreenshotsControl(this);
            ScreenshotsTab.Content = _screenshots;
            _screenshotsMenu = new[] {
                                         new MenuItem {
                                                          Header = "Save Screenshot To File"
                                                      },
                                         new MenuItem {
                                                          Header = "Replace Screenshot"
                                                      },
                                         new MenuItem {
                                                          Header = "Add new Screenshot(s)"
                                                      },
                                         new MenuItem {
                                                          Header = "Remove screenshot"
                                                      }
                                     };
            _screenshotsMenu[0].Click += _screenshots.SaveImageToFileOnClick;
            _screenshotsMenu[1].Click += _screenshots.SelectNewScreenshot;
            _screenshotsMenu[2].Click += _screenshots.AddNewScreenshot;
            _screenshotsMenu[3].Click += _screenshots.RemoveScreenshot;

            #endregion

            OnlineAssetsTab.Content = new OnlineAssetsControl(this, _boxart, _background, _iconBanner, _screenshots);
            _ftpAssetsControl = new FtpAssetsControl(this, _boxart, _background, _iconBanner, _screenshots);
            FtpAssetsContainer.Content = _ftpAssetsControl;

            var bw = new BackgroundWorker();
            bw.DoWork += (sender, e) => {
                             foreach(var arg in args.Where(File.Exists)) {
                                 if(VerifyAuroraMagic(arg))
                                     LoadAuroraAsset(arg);
                                 else
                                     LoadFsdAsset(arg);
                             }
                         };
            bw.RunWorkerCompleted += (sender, e) => BusyIndicator.Visibility = Visibility.Collapsed;
            if(!args.Any())
                return;
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
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
                if(asset.HasBoxArt) {
                    _boxart.Load(asset);
                    Dispatcher.Invoke(new Action(() => BoxartTab.IsSelected = true));
                }
                else if(asset.HasBackground) {
                    _background.Load(asset);
                    Dispatcher.Invoke(new Action(() => BackgroundTab.IsSelected = true));
                }
                else if(asset.HasScreenshots) {
                    _screenshots.Load(asset);
                    Dispatcher.Invoke(new Action(() => ScreenshotsTab.IsSelected = true));
                }
                else if(asset.HasIconBanner) {
                    _iconBanner.Load(asset);
                    Dispatcher.Invoke(new Action(() => IconBannerTab.IsSelected = true));
                }
                else
                    MessageBox.Show(string.Format("ERROR: {0} Doesn't contain any Assets", filename), "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch(Exception ex) {
                SaveError(ex);
                MessageBox.Show(string.Format("ERROR: While processing {0}{1}{2}{1}See error.log for more details about this error", filename, Environment.NewLine, ex.Message), "ERROR",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFsdAsset(string filename) {
            try {
                var asset = new FsdAsset(File.ReadAllBytes(filename));
                var img = asset.GetBoxart();
                if(img != null) {
                    _boxart.Load(img);
                    Dispatcher.Invoke(new Action(() => BoxartTab.IsSelected = true));
                }
                img = asset.GetBackground();
                if(img != null) {
                    _background.Load(img);
                    Dispatcher.Invoke(new Action(() => BackgroundTab.IsSelected = true));
                }
                img = asset.GetIcon();
                if(img != null) {
                    _iconBanner.Load(img, true);
                    Dispatcher.Invoke(new Action(() => IconBannerTab.IsSelected = true));
                }
                img = asset.GetBanner();
                if(img != null) {
                    _iconBanner.Load(img, false);
                    Dispatcher.Invoke(new Action(() => IconBannerTab.IsSelected = true));
                }
                var screenshots = asset.GetScreenshots();
                if(screenshots.Length > 0) {
                    foreach(var ss in screenshots) {
                        if(_screenshots.SpaceLeft())
                            _screenshots.Load(ss, false);
                        else {
                            MessageBox.Show("ERROR: Not enough space to fit all screenshots, please clear current screenshots and load the FSD asset again...", "ERROR", MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                            return;
                        }
                    }
                    Dispatcher.Invoke(new Action(() => ScreenshotsTab.IsSelected = true));
                }
                else
                    MessageBox.Show(string.Format("ERROR: {0} Doesn't contain any Assets", filename), "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch(Exception ex) {
                SaveError(ex);
                MessageBox.Show(string.Format("ERROR: While processing {0}{1}{2}{1}See error.log for more details about this error", filename, Environment.NewLine, ex.Message), "ERROR",
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
                                 else
                                     LoadFsdAsset(fileName);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => BusyIndicator.Visibility = Visibility.Collapsed;
            bw.RunWorkerAsync();
        }

        private static bool VerifyAuroraMagic(string fileName) {
            using(var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using(var br = new BinaryReader(stream))
                    return br.ReadUInt32() == 0x41455852; /* RXEA in LittleEndian format */
            }
        }

        private void CreateNewOnClick(object sender, RoutedEventArgs e) {
            _boxart.Reset();
            _background.Reset();
            _screenshots.Reset();
            _iconBanner.Reset();
        }

        private void SaveBoxartOnClick(object sender, RoutedEventArgs e) {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             try {
                                 _boxart.Save();
                             }
                             catch(Exception ex) {
                                 SaveError(ex);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => BusyIndicator.Visibility = Visibility.Collapsed;
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
        }

        private void SaveBackgroundOnClick(object sender, RoutedEventArgs e) {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             try {
                                 _background.Save();
                             }
                             catch(Exception ex) {
                                 SaveError(ex);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => BusyIndicator.Visibility = Visibility.Collapsed;
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
        }

        private void SaveScreenshotsOnClick(object sender, RoutedEventArgs e) {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             try {
                                 _screenshots.Save();
                             }
                             catch(Exception ex) {
                                 SaveError(ex);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => BusyIndicator.Visibility = Visibility.Collapsed;
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
        }

        private void SaveIconBannerOnClick(object sender, RoutedEventArgs e) {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             try {
                                 _iconBanner.Save();
                             }
                             catch(Exception ex) {
                                 SaveError(ex);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => BusyIndicator.Visibility = Visibility.Collapsed;
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
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

        private Image GetImage(string filename, Size newSize) {
            try {
                var shouldResize = false;
                Dispatcher.Invoke(new Action(() => shouldResize = AutoResizeImages.IsChecked));
                var ms = new MemoryStream(File.ReadAllBytes(filename));
                var img = Image.FromStream(ms);
                if(!img.Size.Equals(newSize) && shouldResize) {
                    //TODO: Add option to honor aspect ratio
                    img = new Bitmap(img, newSize);
                }
                return img;
            }
            catch(Exception ex) {
                SaveFileError(filename, ex);
                return null;
            }
        }

        internal void DragDrop(UIElement sender, DragEventArgs e) {
            if(!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             var askScreenshot = true;
                             foreach(var t in files) {
                                 if(VerifyAuroraMagic(t))
                                     LoadAuroraAsset(t);
                                 else if(VerifyFsdMagic(t))
                                     LoadFsdAsset(t);
                                 else if(Equals(sender, _boxart))
                                     _boxart.Load(GetImage(t, new Size(900, 600)));
                                 else if(Equals(sender, _background))
                                     _background.Load(GetImage(t, new Size(1280, 720)));
                                 else if(Equals(sender, _screenshots)) {
                                     if(askScreenshot && _screenshots.SelectedExists()) { // Do we have a screenshot selected?
                                         var res = MessageBox.Show(string.Format("Do you want to replace the current Screenshot with {0}?", t), "Replace screenshot?", MessageBoxButton.YesNoCancel,
                                                                   MessageBoxImage.Question, MessageBoxResult.Cancel);
                                         if(res == MessageBoxResult.Yes) {
                                             _screenshots.Load(GetImage(t, new Size(1000, 562)), true); // We want to replace it
                                             askScreenshot = false;
                                         }
                                         else if(res == MessageBoxResult.No && _screenshots.SpaceLeft()) // Do we have space for another screenshot?
                                             _screenshots.Load(GetImage(t, new Size(1000, 562)), false);
                                     }
                                     else if(_screenshots.SpaceLeft()) {
                                         askScreenshot = false; // The user probably want to add the remaining covers...
                                         _screenshots.Load(GetImage(t, new Size(1000, 562)), true);
                                     }
                                     else
                                         MessageBox.Show("There is no space left for new screenshots :(", "No space left", MessageBoxButton.OK, MessageBoxImage.Error);
                                 }
                                 else if(Equals(sender, _iconBanner)) {
                                     var res = MessageBox.Show(string.Format("Is {0} an Icon? (If you select no it's assumed it's a banner)", t), "Is this an icon?", MessageBoxButton.YesNoCancel,
                                                               MessageBoxImage.Question, MessageBoxResult.Cancel);
                                     switch(res) {
                                         case MessageBoxResult.Yes:
                                             _iconBanner.Load(GetImage(t, new Size(64, 64)), true);
                                             break;
                                         case MessageBoxResult.No:
                                             _iconBanner.Load(GetImage(t, new Size(420, 96)), false);
                                             break;
                                     }
                                 }
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => { BusyIndicator.Visibility = Visibility.Collapsed; };
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
        }

        private static bool VerifyFsdMagic(string fileName) {
            using(var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using(var br = new BinaryReader(stream))
                    return br.ReadUInt32() == 0x41445346; /* FSDA in LittleEndian format */
            }
        }

        internal static void SaveToFile(Image img, string title, string defaultFilename) {
            var sfd = new SaveFileDialog {
                                             Title = title,
                                             FileName = defaultFilename,
                                             Filter = ImageFileFilter
                                         };
            if(sfd.ShowDialog() != true)
                return;
            var fmt = ImageFormat.Png;
            var extension = Path.GetExtension(sfd.FileName);
            if(extension != null) {
                switch(extension.ToLower()) {
                    case ".png":
                        break; // already our default
                    case ".jpg":
                    case ".jpeg":
                        fmt = ImageFormat.Jpeg;
                        break;
                    case ".bmp":
                        fmt = ImageFormat.Bmp;
                        break;
                    case ".tif":
                    case ".tiff":
                        fmt = ImageFormat.Tiff;
                        break;
                    case ".gif":
                        fmt = ImageFormat.Gif;
                        break;
                }
            }
            using(var ms = new MemoryStream()) {
                img.Save(ms, fmt);
                File.WriteAllBytes(sfd.FileName, ms.ToArray());
            }
        }

        public Image LoadImage(string title, string defaultFilename, Size newSize) {
            var ofd = new OpenFileDialog {
                                             Title = title,
                                             FileName = defaultFilename,
                                             Filter = ImageFileFilter
                                         };
            return ofd.ShowDialog() != true ? null : (GetImage(ofd.FileName, newSize));
        }

        public IEnumerable<Image> LoadImages(string title, string defaultFilename, Size newSize) {
            var ofd = new OpenFileDialog {
                                             Title = title,
                                             FileName = defaultFilename,
                                             Filter = ImageFileFilter,
                                             Multiselect = true
                                         };
            return ofd.ShowDialog() != true ? null : ofd.FileNames.Select(fileName => GetImage(fileName, newSize));
        }

        private void TabChanged(object sender, SelectionChangedEventArgs e) {
            if(BoxartTab.IsSelected)
                EditMenu.ItemsSource = _boxartMenu;
            else if(BackgroundTab.IsSelected)
                EditMenu.ItemsSource = _backgroundMenu;
            else if(IconBannerTab.IsSelected)
                EditMenu.ItemsSource = _iconBannerMenu;
            else if(ScreenshotsTab.IsSelected)
                EditMenu.ItemsSource = _screenshotsMenu;
            else
                EditMenu.ItemsSource = null;
            
            // Always enable the EditMenu when tabs are selected
            EditMenu.IsEnabled = true;
        }

        private void EditMenuOpened(object sender, RoutedEventArgs e) {
            if(BoxartTab.IsSelected)
                ((MenuItem)EditMenu.Items[0]).IsEnabled = _boxart.HavePreview;
            else if(BackgroundTab.IsSelected)
                ((MenuItem)EditMenu.Items[0]).IsEnabled = _background.HavePreview;
            else if(IconBannerTab.IsSelected) {
                ((MenuItem)EditMenu.Items[0]).IsEnabled = _iconBanner.HaveIcon;
                ((MenuItem)EditMenu.Items[3]).IsEnabled = _iconBanner.HaveBanner;
            }
            else if(ScreenshotsTab.IsSelected) {
                ((MenuItem)EditMenu.Items[0]).IsEnabled = _screenshots.HavePreview;
                ((MenuItem)EditMenu.Items[3]).IsEnabled = _screenshots.HavePreview;
            }
        }

        private void SaveAllAssetsOnClick(object sender, RoutedEventArgs e) {
            var ipd = new InputDialog(this, "Please specify TitleID:");
            if(ipd.ShowDialog() != true || string.IsNullOrWhiteSpace(ipd.Value))
                return;
            var fsd = new VistaFolderBrowserDialog {
                                                       Description = "Select where to save the asset files"
                                                   };
            if(fsd.ShowDialog(this) != true)
                return;
            var name = ipd.Value;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             try {
                                 var filename = Path.Combine(fsd.SelectedPath, string.Format("GC{0}.asset", name));
                                 if(_boxart.HavePreview || !File.Exists(filename))
                                     _boxart.Save(filename);
                                 filename = Path.Combine(fsd.SelectedPath, string.Format("BK{0}.asset", name));
                                 if(_background.HavePreview || !File.Exists(filename))
                                     _background.Save(filename);
                                 filename = Path.Combine(fsd.SelectedPath, string.Format("GL{0}.asset", name));
                                 if(_iconBanner.HaveBanner || _iconBanner.HaveIcon || !File.Exists(filename))
                                     _iconBanner.Save(filename);
                                 filename = Path.Combine(fsd.SelectedPath, string.Format("SS{0}.asset", name));
                                 if(_screenshots.HaveScreenshots || !File.Exists(filename))
                                     _screenshots.Save(filename);
                             }
                             catch(Exception ex) {
                                 SaveError(ex);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => BusyIndicator.Visibility = Visibility.Collapsed;
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
        }

        private void SaveAllAssetsFtpOnClick(object sender, RoutedEventArgs e) {
            if(!App.FtpOperations.ConnectionEstablished) {
                MessageBox.Show("ERROR: FTP Connection could not be established", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var dialog = new TitleAndDbIdDialog(this);
            if(dialog.ShowDialog() != true)
                return;
            var tid = dialog.TitleId;
            var aid = dialog.AssetId;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             try {
                                 if(_boxart.HavePreview)
                                     App.FtpOperations.SendAssetData(string.Format("GC{0}.asset", tid), aid, _boxart.GetData());
                                 if(_background.HavePreview)
                                     App.FtpOperations.SendAssetData(string.Format("BK{0}.asset", tid), aid, _background.GetData());
                                 if(_iconBanner.HaveBanner || _iconBanner.HaveIcon)
                                     App.FtpOperations.SendAssetData(string.Format("GL{0}.asset", tid), aid, _iconBanner.GetData());
                                 if(_screenshots.HaveScreenshots)
                                     App.FtpOperations.SendAssetData(string.Format("SS{0}.asset", tid), aid, _screenshots.GetData());
                             }
                             catch(Exception ex) {
                                 MessageBox.Show("There was an error while processing your request, check error.log for more information...");
                                 SaveError(ex);
                             }
                         };
            bw.RunWorkerCompleted += (o, args) => BusyIndicator.Visibility = Visibility.Collapsed;
            BusyIndicator.Visibility = Visibility.Visible;
            bw.RunWorkerAsync();
        }

        private void FileOpening(object sender, ContextMenuEventArgs e) { FtpUpload.IsEnabled = App.FtpOperations.HaveSettings; }

        private void LoadAssetsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = AssetPathTextBox.Text;
                if (Directory.Exists(path))
                {
                    var folders = Directory.GetDirectories(path)
                        .Select(folderPath => 
                        {
                            var folderName = Path.GetFileName(folderPath);
                            // Split the folder name into Game Name and TitleID
                            var parts = folderName.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                            var gameName = parts.Length > 0 ? parts[0].Trim() : folderName;
                            var titleId = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                            // Check for 8-character subfolder
                            var hasSubfolder = Directory.Exists(Path.Combine(folderPath, titleId));

                            // Count files
                            int CountFiles(string pattern) => 
                                Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories).Length;

                            var boxartCount = CountFiles("boxart_*.png");
                            var backgroundCount = CountFiles("background*.png");
                            var bannerCount = CountFiles("banner*.png");
                            var iconCount = CountFiles("icon*.png");
                            var screenshotCount = CountFiles("screenshot*.png");

                            return new FolderInfo
                            {
                                GameName = gameName,
                                TitleId = titleId,
                                Assets = hasSubfolder ? "✓" : "",
                                Boxart = boxartCount > 0 ? boxartCount.ToString() : "",
                                Back = backgroundCount > 0 ? backgroundCount.ToString() : "",
                                Banner = bannerCount > 0 ? bannerCount.ToString() : "",
                                Icon = iconCount > 0 ? iconCount.ToString() : "",
                                Screens = screenshotCount > 0 ? screenshotCount.ToString() : ""
                            };
                        })
                        .ToList();

                    FolderListView.ItemsSource = folders;
                    UpdateMatchingColors();
                }
                else
                {
                    MessageBox.Show("The specified path does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading assets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowLocalAssetsButton_Click(object sender, RoutedEventArgs e)
        {
            LocalAssets.Visibility = Visibility.Visible;
        }

        private void FolderListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Get the selected folder info
                if (FolderListView.SelectedItem is FolderInfo selectedFolder)
                {
                    // Get the folder path
                    string folderPath = Path.Combine(AssetPathTextBox.Text, $"{selectedFolder.GameName} ({selectedFolder.TitleId})");
                    string subfolderPath = Path.Combine(folderPath, selectedFolder.TitleId);

                    if (Directory.Exists(subfolderPath))
                    {
                        // Find all asset files in the subfolder
                        var assetFiles = Directory.GetFiles(subfolderPath, "*.asset");

                        if (assetFiles.Length > 0)
                        {
                            // Clear existing assets
                            CreateNewOnClick(null, null);

                            // Process each asset file
                            foreach (var assetFile in assetFiles)
                            {
                                if (VerifyAuroraMagic(assetFile))
                                {
                                    LoadAuroraAsset(assetFile);
                                }
                                else if (VerifyFsdMagic(assetFile))
                                {
                                    LoadFsdAsset(assetFile);
                                }
                            }

                            // Switch to the first tab with content
                            if (_boxart.HavePreview)
                                BoxartTab.IsSelected = true;
                            else if (_background.HavePreview)
                                BackgroundTab.IsSelected = true;
                            else if (_iconBanner.HaveBanner || _iconBanner.HaveIcon)
                                IconBannerTab.IsSelected = true;
                            else if (_screenshots.HaveScreenshots)
                                ScreenshotsTab.IsSelected = true;
                        }
                        else
                        {
                            MessageBox.Show("No asset files found in the subfolder.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Subfolder '{selectedFolder.TitleId}' not found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading assets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HighlightMatchingRows()
        {
            var ftpTitleIds = GetFtpTitleIds();
            
            // Highlight local assets
            if (FolderListView.ItemsSource is List<FolderInfo> localAssets)
            {
                foreach (var item in localAssets)
                {
                    if (!string.IsNullOrEmpty(item.TitleId) && ftpTitleIds.Contains(item.TitleId))
                    {
                        item.BackgroundColor = System.Windows.Media.Brushes.LightGreen;
                    }
                    else
                    {
                        item.BackgroundColor = System.Windows.Media.Brushes.Transparent;
                    }
                }
            }

            // Highlight FTP assets
            if (_ftpAssetsControl?.FtpAssetsBox?.ItemsSource is IEnumerable<AuroraDbManager.ContentItem> ftpAssets)
            {
                var localTitleIds = new HashSet<string>(
                    (FolderListView.ItemsSource as List<FolderInfo>)?.Select(f => f.TitleId) ?? 
                    Enumerable.Empty<string>());

                foreach (var item in ftpAssets)
                {
                    if (!string.IsNullOrEmpty(item.TitleId) && localTitleIds.Contains(item.TitleId))
                    {
                        item.BackgroundColor = System.Windows.Media.Brushes.LightGreen;
                    }
                    else
                    {
                        item.BackgroundColor = System.Windows.Media.Brushes.Transparent;
                    }
                }
            }
        }

        private HashSet<string> GetFtpTitleIds()
        {
            return new HashSet<string>(_ftpGames.Select(g => g.TitleId));
        }

        public void UpdateFtpGames(List<FtpGameInfo> games)
        {
            _ftpGames = games;
            // If local assets are already loaded, update their highlighting
            if (FolderListView.ItemsSource != null)
            {
                HighlightMatchingRows();
            }
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
                    var view = CollectionViewSource.GetDefaultView(FolderListView.ItemsSource);
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

        public void UpdateMatchingColors()
        {
            // Only proceed if both lists have items
            var localAssets = FolderListView?.ItemsSource as List<FolderInfo>;
            var ftpAssets = _ftpAssetsControl?.FtpAssetsBox?.ItemsSource as IEnumerable<AuroraDbManager.ContentItem>;
            
            if (localAssets == null || ftpAssets == null || !localAssets.Any() || !ftpAssets.Any())
                return;

            // Create lookup sets with normalized TitleIDs
            var localTitleIds = new HashSet<string>(
                localAssets.Select(f => f.TitleId?.ToUpper()?.Trim())
                .Where(id => !string.IsNullOrEmpty(id)));
            
            var ftpTitleIds = new HashSet<string>(
                ftpAssets.Select(f => f.TitleId?.ToUpper()?.Trim())
                .Where(id => !string.IsNullOrEmpty(id)));

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Local TitleIDs: {string.Join(", ", localTitleIds)}");
            System.Diagnostics.Debug.WriteLine($"FTP TitleIDs: {string.Join(", ", ftpTitleIds)}");

            // Update local assets colors
            foreach (var item in localAssets)
            {
                var normalizedTitleId = item.TitleId?.ToUpper()?.Trim();
                item.BackgroundColor = !string.IsNullOrEmpty(normalizedTitleId) && ftpTitleIds.Contains(normalizedTitleId)
                    ? System.Windows.Media.Brushes.LightGreen
                    : System.Windows.Media.Brushes.Transparent;
                
                System.Diagnostics.Debug.WriteLine($"Local: {item.GameName} ({normalizedTitleId}) - Match: {ftpTitleIds.Contains(normalizedTitleId)}");
            }

            // Update FTP assets colors
            foreach (var item in ftpAssets)
            {
                var normalizedTitleId = item.TitleId?.ToUpper()?.Trim();
                item.BackgroundColor = !string.IsNullOrEmpty(normalizedTitleId) && localTitleIds.Contains(normalizedTitleId)
                    ? System.Windows.Media.Brushes.LightGreen
                    : System.Windows.Media.Brushes.Transparent;
                
                System.Diagnostics.Debug.WriteLine($"FTP: {item.TitleName} ({normalizedTitleId}) - Match: {localTitleIds.Contains(normalizedTitleId)}");
            }

            // Force ListView updates
            FolderListView.Items.Refresh();
            _ftpAssetsControl.FtpAssetsBox.Items.Refresh();
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
            var view = CollectionViewSource.GetDefaultView(FolderListView.ItemsSource);
            if (view == null) return;

            var titleFilter = LocalTitleFilterBox.Text.ToLower();
            var titleIdFilter = LocalTitleIdFilterBox.Text.ToLower();

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

        private void ShowFtpAssets()
        {
            EditMenu.ItemsSource = null;
            EditMenu.IsEnabled = false;
        }
    }

    public class FolderInfo
    {
        public string GameName { get; set; }
        public string TitleId { get; set; }
        public string Assets { get; set; }
        public string Boxart { get; set; }
        public string Back { get; set; }
        public string Banner { get; set; }
        public string Icon { get; set; }
        public string Screens { get; set; }
        public System.Windows.Media.Brush BackgroundColor { get; set; } = System.Windows.Media.Brushes.Transparent;
    }

    public class FtpGameInfo
    {
        public string Title { get; set; }
        public string TitleId { get; set; }
        public string DatabaseId { get; set; }
    }
}
