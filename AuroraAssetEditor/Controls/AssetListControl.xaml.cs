using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Diagnostics;
using AuroraAssetEditor.Models;

namespace AuroraAssetEditor.Controls
{
    public partial class AssetListControl : UserControl
    {
        private readonly CollectionViewSource _itemsViewSource;
        private ICollectionView _itemsView;

        public event SelectionChangedEventHandler SelectionChanged;
        public object SelectedItem => ItemListView?.SelectedItem;
        public object ItemsSource => ItemListView?.ItemsSource;

        public AssetListControl()
        {
            // Debug.WriteLine("Initializing AssetListControl");
            InitializeComponent();
            
            _itemsViewSource = new CollectionViewSource();
            // Debug.WriteLine("Created CollectionViewSource");
            
            ItemListView.ItemsSource = _itemsViewSource.View;
            // Debug.WriteLine("Set ItemListView.ItemsSource");
            
            _itemsView = CollectionViewSource.GetDefaultView(ItemListView.ItemsSource);
            // Debug.WriteLine($"Initial _itemsView is null: {_itemsView == null}");
        }

        public void SetItems(System.Collections.IEnumerable items)
        {
            // Debug.WriteLine($"\n=== Setting items in AssetListControl ===");
            // Debug.WriteLine($"Items is null: {items == null}");
            
            try
            {
                ItemListView.ItemsSource = null; // Clear existing source
                _itemsViewSource.Source = items;
                ItemListView.ItemsSource = _itemsViewSource.View;
                _itemsView = CollectionViewSource.GetDefaultView(ItemListView.ItemsSource);
                
                // Debug.WriteLine($"_itemsView is null after setting: {_itemsView == null}");
                // Debug.WriteLine($"ItemListView.ItemsSource is null: {ItemListView.ItemsSource == null}");
                
                if (_itemsView != null)
                {
                    // Debug.WriteLine("CollectionView created successfully");
                    if (items != null)
                    {
                        var count = 0;
                        foreach (var item in items)
                        {
                            count++;
                            if (item is FolderInfo folderInfo)
                            {
                                // Debug.WriteLine($"Item {count}: GameName={folderInfo.GameName}, TitleId={folderInfo.TitleId}");
                            }
                        }
                        // Debug.WriteLine($"Total items processed: {count}");
                    }
                }
                else
                {
                    // Debug.WriteLine("Warning: CollectionView is null after setting items");
                }
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error in SetItems: {ex.Message}");
                // Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            // Debug.WriteLine("=== Finished setting items ===\n");
        }

        public void RefreshView()
        {
            // Debug.WriteLine("\n=== Refreshing view ===");
            try
            {
                var view = CollectionViewSource.GetDefaultView(ItemListView.ItemsSource);
                if (view != null)
                {
                    // Debug.WriteLine("CollectionView exists, refreshing");
                    view.Refresh();
                    // Debug.WriteLine("View refreshed");
                }
                else
                {
                    // Debug.WriteLine("Warning: Cannot refresh view - CollectionView is null");
                }
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error in RefreshView: {ex.Message}");
            }
            // Debug.WriteLine("=== Finished refresh ===\n");
        }

        public void ApplyFilter(Func<object, bool> filter)
        {
            // Debug.WriteLine("\n=== Applying filter ===");
            try
            {
                var view = CollectionViewSource.GetDefaultView(ItemListView.ItemsSource);
                if (view == null)
                {
                    // Debug.WriteLine("Warning: Cannot apply filter - CollectionView is null");
                    return;
                }

                if (filter == null)
                {
                    // Debug.WriteLine("Clearing filter");
                    view.Filter = null;
                    return;
                }

                // Debug.WriteLine("Setting new filter");
                view.Filter = new Predicate<object>(filter);
                // Debug.WriteLine("=== Filter applied ===\n");
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error in ApplyFilter: {ex.Message}");
            }
        }

        public void ShowBusyIndicator()
        {
            BusyIndicator.Visibility = Visibility.Visible;
        }

        public void HideBusyIndicator()
        {
            BusyIndicator.Visibility = Visibility.Collapsed;
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
            // Debug.WriteLine("\n=== Applying text filters ===");
            try
            {
                var view = CollectionViewSource.GetDefaultView(ItemListView.ItemsSource);
                if (view == null)
                {
                    // Debug.WriteLine("Warning: Cannot apply filters - CollectionView is null");
                    return;
                }

                var titleFilter = TitleFilterBox?.Text?.ToLower() ?? "";
                var titleIdFilter = TitleIdFilterBox?.Text?.ToLower() ?? "";
                // Debug.WriteLine($"Title filter: '{titleFilter}', TitleId filter: '{titleIdFilter}'");

                view.Filter = obj =>
                {
                    if (obj is FolderInfo item)
                    {
                        if (string.IsNullOrWhiteSpace(titleFilter) && string.IsNullOrWhiteSpace(titleIdFilter))
                        {
                            return true;
                        }

                        var matchesTitle = string.IsNullOrWhiteSpace(titleFilter) || 
                                         item.GameName?.ToLower().Contains(titleFilter) == true;
                        var matchesTitleId = string.IsNullOrWhiteSpace(titleIdFilter) || 
                                           item.TitleId?.ToLower().Contains(titleIdFilter) == true;

                        return matchesTitle && matchesTitleId;
                    }
                    return false;
                };
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error in ApplyFilters: {ex.Message}");
            }
            // Debug.WriteLine("=== Filters applied ===\n");
        }

        private void ItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Debug.WriteLine("\n=== Selection Changed ===");
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is FolderInfo selected)
            {
                // Debug.WriteLine($"Selected item: {selected.GameName}");
            }
            SelectionChanged?.Invoke(this, e);
            // Debug.WriteLine("=== Selection Change Handled ===\n");
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            if (headerClicked?.Column is GridViewColumn column)
            {
                var binding = column.DisplayMemberBinding as Binding;
                string propertyName = binding?.Path.Path;
                
                if (!string.IsNullOrEmpty(propertyName))
                {
                    var view = CollectionViewSource.GetDefaultView(ItemListView.ItemsSource);
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
    }
} 