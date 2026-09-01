using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;

namespace MBW.App.Views
{
    public sealed partial class SendPage : Page
    {
        private readonly SendPageViewModel _viewModel;

        public SendPage()
        {
            InitializeComponent();

            _viewModel = AppServices.GetSendPageViewModel();
            DataContext = _viewModel;
            NavigationCacheMode = NavigationCacheMode.Enabled;

            _viewModel.HtmlPreviewChanged += ViewModel_HtmlPreviewChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = _viewModel.EnsureLoadedAsync(force: true);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _ = _viewModel.PersistSettingsAsync();
        }

        public Task ReloadAsync() => _viewModel.EnsureLoadedAsync(force: true);

        private void RenameVariablePicker_SelectionPicked(object sender, string column)
        {
            InsertRenameVariable(column);
            RenameVariablePicker.SelectedItem = null;
        }

        private void InsertRenameVariable(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return;
            }

            var token = $"{{{columnName}}}";
            var text = RenamePatternBox.Text ?? string.Empty;
            var start = RenamePatternBox.SelectionStart;
            if (start < 0 || start > text.Length)
            {
                start = text.Length;
            }

            var length = RenamePatternBox.SelectionLength;
            if (length < 0)
            {
                length = 0;
            }

            if (start + length > text.Length)
            {
                length = text.Length - start;
            }

            RenamePatternBox.Text = text[..start] + token + text[(start + length)..];
            var caret = start + token.Length;
            RenamePatternBox.SelectionStart = caret;
            RenamePatternBox.SelectionLength = 0;
            RenamePatternBox.Focus(FocusState.Programmatic);
        }

        private async void ViewModel_HtmlPreviewChanged(object? sender, string htmlBody)
        {
            await UpdateHtmlPreviewAsync(htmlBody);
        }

        private async Task UpdateHtmlPreviewAsync(string htmlBody)
        {
            try
            {
                if (PreviewWebView.CoreWebView2 is null)
                {
                    await PreviewWebView.EnsureCoreWebView2Async();
                }

                PreviewWebView.CoreWebView2?.Settings.IsZoomControlEnabled = false;
                PreviewWebView.Visibility = Visibility.Visible;
                PreviewWebView.NavigateToString(WrapPreviewHtml(htmlBody));
            }
            catch
            {
                PreviewWebView.Visibility = Visibility.Collapsed;
            }
        }

        private static string WrapPreviewHtml(string htmlBody)
        {
            var body = htmlBody ?? string.Empty;
            if (body.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                return body;
            }

            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />" +
                   "<meta name=\"color-scheme\" content=\"light\" />" +
                   "<style>html,body{background:#ffffff !important;color:#1b1b1b !important;}body{font-family:'Segoe UI',sans-serif;font-size:14px;line-height:1.6;margin:0;padding:12px;}p{margin:0 0 0.75em 0;}</style>" +
                   "</head><body>" + body + "</body></html>";
        }
    }
}
