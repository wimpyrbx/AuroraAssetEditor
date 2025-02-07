//
//  App.xaml.cs
//  AuroraAssetEditor
//
//  Created by Swizzy on 08/05/2015
//  Copyright (c) 2015 Swizzy. All rights reserved.

namespace AuroraAssetEditor {
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using AuroraAssetEditor.Classes;
    using AuroraAssetEditor.Models;

    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App {
        internal static readonly FtpOperations FtpOperations = new FtpOperations();

        private static readonly Icon Icon =
            Icon.ExtractAssociatedIcon(Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(App)).Location), Path.GetFileName(Assembly.GetAssembly(typeof(App)).Location)));

        internal static readonly ImageSource WpfIcon = Imaging.CreateBitmapSourceFromHIcon(Icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

        internal static Xbox360GameData Xbox360GameData;

        private void AppStart(object sender, StartupEventArgs e)
        {
            // Initialize database before creating main window
            Classes.Xbox360DB.Initialize();
            
            var mainWindow = new MainWindow(e.Args);
            mainWindow.Show();
        }
    }
}