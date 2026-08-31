using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MBW.App.Views
{
    public sealed partial class DatabasePage : Page
    {
        private readonly DatabaseViewModel _viewModel;
        private bool _syncingScroll;

        public DatabasePage()
        {
            InitializeComponent();

            _viewModel = AppServices.GetDatabaseViewModel();
            _viewModel.PickExcelFileAsync = PickExcelFileAsync;
            _viewModel.ShowImportDialogAsync = ShowImportDialogAsync;
            _viewModel.ConfirmOverwriteAsync = ConfirmOverwriteAsync;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
            Loaded += DatabasePage_Loaded;
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
        }

        private async void DatabasePage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.EnsureLoadedAsync(force: false);
            RebuildTable();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DatabaseViewModel.ColumnHeaders)
                or nameof(DatabaseViewModel.PreviewRows)
                or nameof(DatabaseViewModel.HasData)
                or nameof(DatabaseViewModel.CurrentPage))
            {
                RebuildTable();
            }
        }

        private async Task<string?> PickExcelFileAsync()
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add(".xlsx");
            picker.FileTypeFilter.Add(".xlsm");

            var hwnd = WindowNative.GetWindowHandle(AppServices.GetMainWindow());
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        private async Task<ExcelImportSelection?> ShowImportDialogAsync(string sourcePath)
        {
            var dialogVm = new ExcelImportDialogViewModel(AppServices.ExcelImporter, sourcePath);
            var dialog = new ExcelImportDialog(dialogVm)
            {
                XamlRoot = XamlRoot
            };
            await dialog.InitializeAsync();
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? dialog.Result : null;
        }

        private async Task<bool> ConfirmOverwriteAsync(string fileName)
        {
            var dialog = new ContentDialog
            {
                Title = "Timpa file?",
                Content = $"File \"{fileName}\" sudah ada di folder data workspace. Timpa file tersebut?",
                PrimaryButtonText = "Timpa",
                CloseButtonText = "Batal",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private void RebuildTable()
        {
            HeaderHost.Child = null;
            BodyHost.Children.Clear();

            if (!_viewModel.HasData || _viewModel.ColumnHeaders.Count == 0)
            {
                return;
            }

            var columnCount = _viewModel.ColumnHeaders.Count;
            HeaderHost.Child = BuildRow(_viewModel.ColumnHeaders, isHeader: true, columnCount);

            foreach (var row in _viewModel.PreviewRows)
            {
                BodyHost.Children.Add(CreateDivider());
                BodyHost.Children.Add(BuildRow(row.Cells, isHeader: false, columnCount));
            }
        }

        private void HeaderScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_syncingScroll)
            {
                return;
            }

            _syncingScroll = true;
            BodyScrollViewer.ChangeView(HeaderScrollViewer.HorizontalOffset, null, null, disableAnimation: true);
            _syncingScroll = false;
        }

        private void BodyScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_syncingScroll)
            {
                return;
            }

            _syncingScroll = true;
            HeaderScrollViewer.ChangeView(BodyScrollViewer.HorizontalOffset, null, null, disableAnimation: true);
            _syncingScroll = false;
        }

        private static Grid BuildRow(System.Collections.Generic.IReadOnlyList<string> cells, bool isHeader, int columnCount)
        {
            var dividerBrush = Application.Current.Resources["DividerStrokeColorDefaultBrush"] as Brush;
            var grid = new Grid
            {
                MinHeight = isHeader ? 36 : 36
            };

            if (isHeader)
            {
                grid.Height = 36;
            }

            for (var i = 0; i < columnCount; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            }

            for (var i = 0; i < columnCount; i++)
            {
                var value = i < cells.Count ? cells[i] : string.Empty;
                var isLastColumn = i == columnCount - 1;
                var cellBorder = new Border
                {
                    BorderBrush = dividerBrush,
                    BorderThickness = new Thickness(0, 0, isLastColumn ? 0 : 1, 0),
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                var block = new TextBlock
                {
                    Text = value,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = isHeader
                        ? Microsoft.UI.Text.FontWeights.SemiBold
                        : Microsoft.UI.Text.FontWeights.Normal,
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
                    VerticalAlignment = isHeader
                        ? VerticalAlignment.Bottom
                        : VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 8, isHeader ? 6 : 0)
                };

                cellBorder.Child = block;
                Grid.SetColumn(cellBorder, i);
                grid.Children.Add(cellBorder);
            }

            return grid;
        }

        private static Border CreateDivider()
        {
            return new Border
            {
                Height = 1,
                Background = Application.Current.Resources["DividerStrokeColorDefaultBrush"] as Brush
            };
        }
    }
}
