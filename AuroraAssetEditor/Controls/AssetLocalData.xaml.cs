using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using AuroraAssetEditor.Models;

namespace AuroraAssetEditor.Controls
{
    public partial class AssetLocalData : UserControl
    {
        private const int MaxDescriptionLength = 512;
        private string _fullDescription;

        public AssetLocalData()
        {
            InitializeComponent();
        }

        public void UpdateData(string titleId, string titleName, string releaseDate, string developer, string publisher, string description, string variantsJson)
        {
            TitleIdBlock.Text = $"TitleID: {titleId}";
            TitleNameBlock.Text = titleName;
            ReleaseDateBlock.Text = releaseDate;
            DeveloperBlock.Text = developer;
            PublisherBlock.Text = publisher;

            // Handle description truncation
            _fullDescription = description;
            if (!string.IsNullOrEmpty(description) && description.Length > MaxDescriptionLength)
            {
                DescriptionBlock.Text = description.Substring(0, MaxDescriptionLength) + "...";
                ShowMoreBlock.Visibility = Visibility.Visible;
            }
            else
            {
                DescriptionBlock.Text = description;
                ShowMoreBlock.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrWhiteSpace(variantsJson))
            {
                try
                {
                    var variants = JsonConvert.DeserializeObject<List<GameVariant>>(variantsJson);
                    if (variants != null && variants.Count > 0)
                    {
                        VariantsList.ItemsSource = variants;
                        RelatedContentPanel.Visibility = Visibility.Visible;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.SaveError(ex);
                }
            }

            RelatedContentPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowMoreBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var window = new Window
            {
                Title = "Full Description",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var scrollViewer = new ScrollViewer();
            var textBlock = new TextBlock
            {
                Text = _fullDescription,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };

            scrollViewer.Content = textBlock;
            window.Content = scrollViewer;

            // Handle ESC key
            window.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    window.Close();
                }
            };

            window.ShowDialog();
        }

        public void Clear()
        {
            TitleIdBlock.Text = string.Empty;
            TitleNameBlock.Text = string.Empty;
            ReleaseDateBlock.Text = string.Empty;
            DeveloperBlock.Text = string.Empty;
            PublisherBlock.Text = string.Empty;
            DescriptionBlock.Text = string.Empty;
            ShowMoreBlock.Visibility = Visibility.Collapsed;
            _fullDescription = string.Empty;
            VariantsList.ItemsSource = null;
            RelatedContentPanel.Visibility = Visibility.Collapsed;
        }
    }
} 