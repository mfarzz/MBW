using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MBW.App.Views
{
    public sealed partial class ExcelImportDialog : ContentDialog
    {
        private readonly ExcelImportDialogViewModel _viewModel;
        private bool _suppressSelectionEvents;

        public ExcelImportDialog(ExcelImportDialogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.PreviewChanged += (_, _) => RebuildPreviewTable();
            XamlRoot = AppServices.GetMainWindow().Content.XamlRoot;
        }

        public ExcelImportSelection? Result { get; private set; }

        public async Task InitializeAsync()
        {
            _suppressSelectionEvents = true;
            HeaderRowCombo.Items.Clear();
            for (var i = 1; i <= 20; i++)
            {
                HeaderRowCombo.Items.Add(i);
            }

            HeaderRowCombo.SelectedItem = 1;
            await _viewModel.InitializeAsync();

            SheetList.ItemsSource = _viewModel.SheetNames;
            if (_viewModel.SelectedSheetName is not null)
            {
                SheetList.SelectedItem = _viewModel.SelectedSheetName;
            }

            HeaderRowCombo.SelectedItem = _viewModel.SelectedHeaderRow;
            SyncChrome();
            RebuildPreviewTable();
            _suppressSelectionEvents = false;
            IsPrimaryButtonEnabled = _viewModel.CanConfirm;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ExcelImportDialogViewModel.StatusMessage)
                or nameof(ExcelImportDialogViewModel.FileName)
                or nameof(ExcelImportDialogViewModel.SummaryText)
                or nameof(ExcelImportDialogViewModel.IsBusy)
                or nameof(ExcelImportDialogViewModel.CanConfirm))
            {
                SyncChrome();
            }
        }

        private void SyncChrome()
        {
            FileNameText.Text = _viewModel.FileName;
            StatusText.Text = _viewModel.StatusMessage;
            SummaryText.Text = _viewModel.SummaryText;
            BusyRing.IsActive = _viewModel.IsBusy;
            IsPrimaryButtonEnabled = _viewModel.CanConfirm && !_viewModel.IsBusy;
        }

        private async void SheetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvents || SheetList.SelectedItem is not string sheetName)
            {
                return;
            }

            _viewModel.SelectedSheetName = sheetName;
            await Task.Yield();
            IsPrimaryButtonEnabled = _viewModel.CanConfirm && !_viewModel.IsBusy;
        }

        private async void HeaderRowCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvents || HeaderRowCombo.SelectedItem is not int headerRow)
            {
                return;
            }

            _viewModel.SelectedHeaderRow = headerRow;
            await Task.Yield();
            IsPrimaryButtonEnabled = _viewModel.CanConfirm && !_viewModel.IsBusy;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var selection = _viewModel.CreateSelection();
            if (selection is null)
            {
                args.Cancel = true;
                return;
            }

            Result = selection;
        }

        private void RebuildPreviewTable()
        {
            PreviewHost.Children.Clear();
            if (_viewModel.PreviewHeaders.Count == 0)
            {
                return;
            }

            var columnCount = _viewModel.PreviewHeaders.Count;
            PreviewHost.Children.Add(BuildRow(_viewModel.PreviewHeaders, isHeader: true, columnCount));

            foreach (var row in _viewModel.PreviewRows)
            {
                PreviewHost.Children.Add(CreateDivider());
                PreviewHost.Children.Add(BuildRow(row.Cells, isHeader: false, columnCount));
            }
        }

        private static Grid BuildRow(System.Collections.Generic.IReadOnlyList<string> cells, bool isHeader, int columnCount)
        {
            var grid = new Grid
            {
                Padding = new Thickness(10, 6, 10, 6),
                Background = isHeader
                    ? Application.Current.Resources["SubtleFillColorSecondaryBrush"] as Brush
                    : null
            };

            for (var i = 0; i < columnCount; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
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
