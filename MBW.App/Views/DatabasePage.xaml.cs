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

        public DatabasePage()
        {
            InitializeComponent();

            _viewModel = AppServices.CreateDatabaseViewModel();
            _viewModel.PickExcelFileAsync = PickExcelFileAsync;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DatabaseViewModel.ColumnHeaders)
                or nameof(DatabaseViewModel.PreviewRows)
                or nameof(DatabaseViewModel.HasData))
            {
                RebuildPreviewTable();
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
            picker.FileTypeFilter.Add(".xls");

            var hwnd = WindowNative.GetWindowHandle(AppServices.GetMainWindow());
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        private void RebuildPreviewTable()
        {
            PreviewTableHost.Children.Clear();

            if (!_viewModel.HasData || _viewModel.ColumnHeaders.Count == 0)
            {
                return;
            }

            var columnCount = _viewModel.ColumnHeaders.Count;
            PreviewTableHost.Children.Add(BuildRow(_viewModel.ColumnHeaders, isHeader: true));

            foreach (var row in _viewModel.PreviewRows)
            {
                PreviewTableHost.Children.Add(CreateDivider());
                PreviewTableHost.Children.Add(BuildRow(row.Cells, isHeader: false));
            }

            Grid BuildRow(System.Collections.Generic.IReadOnlyList<string> cells, bool isHeader)
            {
                var grid = new Grid
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = isHeader
                        ? Application.Current.Resources["SubtleFillColorSecondaryBrush"] as Brush
                        : null
                };

                for (var i = 0; i < columnCount; i++)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                }

                for (var i = 0; i < columnCount; i++)
                {
                    var value = i < cells.Count ? cells[i] : string.Empty;
                    var block = new TextBlock
                    {
                        Text = value,
                        FontSize = 12,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontWeight = isHeader
                            ? Microsoft.UI.Text.FontWeights.SemiBold
                            : Microsoft.UI.Text.FontWeights.Normal
                    };
                    Grid.SetColumn(block, i);
                    grid.Children.Add(block);
                }

                return grid;
            }
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
