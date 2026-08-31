using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace MBW.App.Controls
{
    public sealed partial class SearchableComboBox : UserControl
    {
        private bool _suppressSearch;
        private INotifyCollectionChanged? _itemsSourceNotify;

        public SearchableComboBox()
        {
            InitializeComponent();
            ItemsList.ItemsSource = FilteredItems;
            UpdateDisplayText();
        }

        public ObservableCollection<string> FilteredItems { get; } = new();

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(SearchableComboBox),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public string? SelectedItem
        {
            get => (string?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(string),
                typeof(SearchableComboBox),
                new PropertyMetadata(null, OnSelectedItemChanged));

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(SearchableComboBox),
                new PropertyMetadata("Pilih item", OnPlaceholderTextChanged));

        public string SearchPlaceholderText
        {
            get => (string)GetValue(SearchPlaceholderTextProperty);
            set => SetValue(SearchPlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty SearchPlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(SearchPlaceholderText),
                typeof(string),
                typeof(SearchableComboBox),
                new PropertyMetadata("Cari...", OnSearchPlaceholderTextChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SearchableComboBox control)
            {
                control.UnsubscribeItemsSource();
                if (e.NewValue is INotifyCollectionChanged notify)
                {
                    control._itemsSourceNotify = notify;
                    notify.CollectionChanged += control.OnItemsSourceCollectionChanged;
                }

                control.RefreshFilteredItems();
            }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SearchableComboBox control)
            {
                control.UpdateDisplayText();
            }
        }

        private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SearchableComboBox control)
            {
                control.UpdateDisplayText();
            }
        }

        private static void OnSearchPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SearchableComboBox control && e.NewValue is string placeholder)
            {
                control.SearchBox.PlaceholderText = placeholder;
            }
        }

        private void UnsubscribeItemsSource()
        {
            if (_itemsSourceNotify is not null)
            {
                _itemsSourceNotify.CollectionChanged -= OnItemsSourceCollectionChanged;
                _itemsSourceNotify = null;
            }
        }

        private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshFilteredItems();
        }

        private void UpdateDisplayText()
        {
            if (!string.IsNullOrWhiteSpace(SelectedItem))
            {
                SelectedTextBlock.Text = SelectedItem;
                SelectedTextBlock.Opacity = 1.0;
            }
            else
            {
                SelectedTextBlock.Text = PlaceholderText;
                SelectedTextBlock.Opacity = 0.55;
            }
        }

        private void TriggerButton_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(TriggerButton);
        }

        private void OptionsFlyout_Opening(object? sender, object e)
        {
            ApplyFlyoutWidth();

            _suppressSearch = true;
            SearchBox.Text = string.Empty;
            SearchBox.PlaceholderText = SearchPlaceholderText;
            _suppressSearch = false;
            RefreshFilteredItems();
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void ApplyFlyoutWidth()
        {
            if (TriggerButton.ActualWidth <= 0)
            {
                return;
            }

            var width = TriggerButton.ActualWidth;
            FlyoutContentPanel.Width = width;
            FlyoutContentPanel.MinWidth = width;
            FlyoutContentPanel.MaxWidth = width;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSearch)
            {
                return;
            }

            RefreshFilteredItems(SearchBox.Text);
            ApplyFlyoutWidth();
        }

        private void ItemsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string item)
            {
                SelectedItem = item;
                OptionsFlyout.Hide();
            }
        }

        private void RefreshFilteredItems(string? query = null)
        {
            var term = query ?? SearchBox?.Text ?? string.Empty;
            FilteredItems.Clear();

            if (ItemsSource is null)
            {
                return;
            }

            foreach (var item in ItemsSource.OfType<string>())
            {
                if (string.IsNullOrWhiteSpace(term)
                    || item.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredItems.Add(item);
                }
            }
        }
    }
}
