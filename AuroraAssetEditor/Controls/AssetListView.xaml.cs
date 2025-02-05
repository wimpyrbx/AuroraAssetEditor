using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AuroraAssetEditor.Models;

namespace AuroraAssetEditor.Controls
{
    public partial class AssetListView : UserControl
    {
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(ObservableCollection<GridViewColumn>), 
                typeof(AssetListView), new PropertyMetadata(null, OnColumnsChanged));
            
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<IAssetItem>), 
                typeof(AssetListView), new PropertyMetadata(null));

        public event SelectionChangedEventHandler SelectionChanged;

        public ObservableCollection<GridViewColumn> Columns
        {
            get => (ObservableCollection<GridViewColumn>)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public IEnumerable<IAssetItem> ItemsSource
        {
            get => (IEnumerable<IAssetItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public AssetListView()
        {
            InitializeComponent();
            Columns = new ObservableCollection<GridViewColumn>();
            AssetList.ItemsSource = ItemsSource;
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AssetListView control)
            {
                control.MainGridView.Columns.Clear();
                foreach (var column in control.Columns)
                {
                    control.MainGridView.Columns.Add(column);
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
        }

        private void TitleFilterChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TitleIdFilterChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var view = CollectionViewSource.GetDefaultView(AssetList.ItemsSource);
            if (view == null) return;

            var titleFilter = TitleFilterBox.Text.ToLower();
            var titleIdFilter = TitleIdFilterBox.Text.ToLower();

            view.Filter = item =>
            {
                if (item is IAssetItem asset)
                {
                    if (string.IsNullOrWhiteSpace(titleFilter) && string.IsNullOrWhiteSpace(titleIdFilter))
                        return true;

                    if (!string.IsNullOrWhiteSpace(titleFilter) && !string.IsNullOrWhiteSpace(titleIdFilter))
                        return asset.Title.ToLower().Contains(titleFilter) && 
                               asset.TitleId.ToLower().Contains(titleIdFilter);

                    if (!string.IsNullOrWhiteSpace(titleFilter))
                        return asset.Title.ToLower().Contains(titleFilter);

                    return asset.TitleId.ToLower().Contains(titleIdFilter);
                }
                return false;
            };
        }
    }
} 