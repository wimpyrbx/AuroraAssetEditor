using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using AuroraAssetEditor.Controls;

namespace AuroraAssetEditor
{
    public partial class ImageViewerWindow : Window
    {
        private readonly List<ImageSource> _images;
        private int _currentIndex;
        private readonly AssetArtwork.NavigationStyleType _navigationStyle;

        public ImageViewerWindow(ImageSource imageSource, string title, AssetArtwork.NavigationStyleType navigationStyle)
            : this(new List<ImageSource> { imageSource }, title, 0, navigationStyle)
        {
        }

        public ImageViewerWindow(List<ImageSource> images, string title, int startIndex, AssetArtwork.NavigationStyleType navigationStyle)
        {
            InitializeComponent();
            _images = images;
            _currentIndex = startIndex;
            _navigationStyle = navigationStyle;
            Title = $"Image Viewer - {title}";

            // Make the window modal
            Owner = Application.Current.MainWindow;
            ShowInTaskbar = false;

            // Apply navigation style
            ApplyNavigationStyle();

            if (_images.Count > 1)
            {
                NavigationPanel.Visibility = Visibility.Visible;
                UpdateDisplay();
            }
            else
            {
                ViewerImage.Source = _images[0];
            }
        }

        private void ApplyNavigationStyle()
        {
            string buttonStyleKey;
            string textStyleKey;

            switch (_navigationStyle)
            {
                case AssetArtwork.NavigationStyleType.Minimal:
                    buttonStyleKey = "MinimalNavigationButtonStyle";
                    textStyleKey = "MinimalNavigationTextStyle";
                    break;
                case AssetArtwork.NavigationStyleType.Modern:
                    buttonStyleKey = "ModernNavigationButtonStyle";
                    textStyleKey = "ModernNavigationTextStyle";
                    break;
                default:
                    buttonStyleKey = "NavigationButtonStyle";
                    textStyleKey = "NavigationTextStyle";
                    break;
            }

            if (FindResource(buttonStyleKey) is Style buttonStyle)
            {
                PrevButton.Style = buttonStyle;
                NextButton.Style = buttonStyle;
            }

            if (FindResource(textStyleKey) is Style textStyle)
            {
                NavigationCounter.Style = textStyle;
            }
        }

        private void UpdateDisplay()
        {
            ViewerImage.Source = _images[_currentIndex];
            NavigationCounter.Text = $"{_currentIndex + 1}/{_images.Count}";
        }

        private void OnPreviousClick(object sender, RoutedEventArgs e)
        {
            NavigateToPrevious();
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            NavigateToNext();
        }

        private void NavigateToPrevious()
        {
            if (_images.Count <= 1) return;
            
            _currentIndex--;
            if (_currentIndex < 0)
                _currentIndex = _images.Count - 1;
            
            UpdateDisplay();
        }

        private void NavigateToNext()
        {
            if (_images.Count <= 1) return;
            
            _currentIndex++;
            if (_currentIndex >= _images.Count)
                _currentIndex = 0;
            
            UpdateDisplay();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    break;
                case Key.Left:
                    NavigateToPrevious();
                    break;
                case Key.Right:
                    NavigateToNext();
                    break;
            }
        }
    }
} 