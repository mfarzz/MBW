using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace MBW.App.Views
{
    public sealed partial class EmailEditorPage : Page
    {
        private bool _isEditorReady;
        private bool _suppressRibbonEvents = true;
        private bool _updatingRibbonFromEditor;
        private bool _isHtmlSourceMode;
        private bool _selectionInLink;
        private string? _pendingHtml;

        private const int TableGridMaxRows = 8;
        private const int TableGridMaxCols = 8;
        private Border[,]? _tableGridCells;

        private EmailEditorViewModel? ViewModel => DataContext as EmailEditorViewModel;

        public EmailEditorPage()
        {
            InitializeComponent();
            DataContext = AppServices.CreateEmailEditorViewModel();
            BuildTableGridPicker();
            InsertTableFlyout.Opened += (_, _) => UpdateTableGridHighlight(0, 0);
            Loaded += EmailEditorPage_Loaded;
        }

        private async void EmailEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel is not null)
            {
                ViewModel.PreviewRequested += ViewModel_PreviewRequested;
                ViewModel.PullEditorContentAsync = SyncEditorToViewModelAsync;
                ViewModel.EditorContentLoaded += ViewModel_EditorContentLoaded;

                var path = AppServices.WorkspaceCoordinator.WorkspacePath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    await ViewModel.LoadWorkspaceAsync(path);
                }
            }

            await InitializeWebView2Async();
        }

        private async void ViewModel_EditorContentLoaded(object? sender, string html)
        {
            await ApplyEditorContentAsync(html);
        }

        public async Task SyncEditorToViewModelAsync()
        {
            if (ViewModel is null)
            {
                return;
            }

            if (_isHtmlSourceMode)
            {
                ViewModel.HtmlBody = HtmlSourceBox.Text;
                return;
            }

            if (HtmlEditor.CoreWebView2 is null || !_isEditorReady)
            {
                return;
            }

            var json = await HtmlEditor.CoreWebView2.ExecuteScriptAsync("getEditorContent()");
            ViewModel.HtmlBody = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
        }

        private async Task ApplyEditorContentAsync(string html)
        {
            if (!_isEditorReady || HtmlEditor.CoreWebView2 is null)
            {
                _pendingHtml = html;
                return;
            }

            var encoded = JsonSerializer.Serialize(html ?? string.Empty);
            await HtmlEditor.CoreWebView2.ExecuteScriptAsync($"setEditorContent({encoded})");
            _pendingHtml = null;
        }

        private async Task InvokeEditorFunctionAsync(string functionName, params object[] args)
        {
            if (!_isEditorReady || HtmlEditor?.CoreWebView2 is null || _isHtmlSourceMode)
            {
                return;
            }

            var argList = string.Join(",", args.Select(a => JsonSerializer.Serialize(a)));
            await HtmlEditor.CoreWebView2.ExecuteScriptAsync($"{functionName}({argList})");
        }

        private async Task ExecEditorCommandAsync(string command, string? value = null)
        {
            if (value is null)
            {
                await InvokeEditorFunctionAsync("execEditorCommand", command, null!);
            }
            else
            {
                await InvokeEditorFunctionAsync("execEditorCommand", command, value);
            }
        }

        private async void ViewModel_PreviewRequested(object? sender, EmailEditorViewModel.PreviewEventArgs e)
        {
            await ShowPreviewDialogAsync(e);
        }

        private void VariableFlyout_Opened(object sender, object e)
        {
            if (sender is not MenuFlyout flyout || ViewModel is null)
            {
                return;
            }

            while (flyout.Items.Count > 2)
            {
                flyout.Items.RemoveAt(2);
            }

            foreach (var variable in ViewModel.AvailableVariables)
            {
                var item = new MenuFlyoutItem { Text = variable, Tag = variable };
                item.Click += OnInsertVariableClick;
                flyout.Items.Add(item);
            }
        }

        private async Task ShowPreviewDialogAsync(EmailEditorViewModel.PreviewEventArgs preview)
        {
            var dialog = new ContentDialog
            {
                Title = "Email Preview",
                PrimaryButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var content = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
            content.Children.Add(new TextBlock { Text = $"From: {preview.FromEmail}", FontSize = 12 });
            content.Children.Add(new TextBlock { Text = $"To: {preview.ToEmail}", FontSize = 12 });
            content.Children.Add(new TextBlock 
            { 
                Text = $"Subject: {preview.Subject}", 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                FontSize = 13 
            });
            content.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray)
            });

            var previewWebView = new WebView2 { MinHeight = 320, MinWidth = 520 };
            content.Children.Add(previewWebView);
            dialog.Content = content;

            var dialogTask = dialog.ShowAsync();
            try
            {
                await previewWebView.EnsureCoreWebView2Async();
                previewWebView.NavigateToString(WrapPreviewHtml(preview.HtmlBody));
            }
            catch
            {
                content.Children.Remove(previewWebView);
                content.Children.Add(new ScrollViewer
            {
                MaxHeight = 400,
                Content = new TextBlock
                {
                    Text = preview.HtmlBody,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12
                }
                });
            }

            await dialogTask;
        }

        private static string WrapPreviewHtml(string htmlBody)
        {
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />" +
                   "<style>body{font-family:'Segoe UI',sans-serif;font-size:14px;line-height:1.6;color:#1b1b1b;margin:0;padding:8px;}</style>" +
                   "</head><body>" + htmlBody + "</body></html>";
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                await HtmlEditor.EnsureCoreWebView2Async();
                HtmlEditor.CoreWebView2.Settings.IsWebMessageEnabled = true;
                HtmlEditor.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                HtmlEditor.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                HtmlEditor.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                HtmlEditor.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                var editorHtmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "html", "editor.html");
                if (File.Exists(editorHtmlPath))
                {
                    var fullPath = Path.GetFullPath(editorHtmlPath).Replace('\\', '/');
                    HtmlEditor.Source = new Uri($"file:///{fullPath}");
                }
                else
                {
                    HtmlEditor.NavigateToString(GetFallbackEditorHtml());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization error: {ex.Message}");
            }
        }

        private void CoreWebView2_NavigationStarting(
            Microsoft.Web.WebView2.Core.CoreWebView2 sender,
            Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            if (IsEditorDocumentNavigation(args.Uri))
            {
                return;
            }

            args.Cancel = true;
        }

        private void CoreWebView2_NewWindowRequested(
            Microsoft.Web.WebView2.Core.CoreWebView2 sender,
            Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
            _ = OpenLinkInBrowserAsync(args.Uri);
        }

        private static bool IsEditorDocumentNavigation(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return true;
            }

            return uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                || uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task OpenLinkInBrowserAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && !Uri.TryCreate($"https://{url}", UriKind.Absolute, out uri))
            {
                return;
            }

            try
            {
                await Launcher.LaunchUriAsync(uri);
            }
            catch
            {
                // Ignore links that cannot be opened on this device.
            }
        }

        private async void CoreWebView2_NavigationCompleted(
            Microsoft.Web.WebView2.Core.CoreWebView2 sender,
            Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                return;
            }

            _isEditorReady = true;
            var html = _pendingHtml ?? ViewModel?.HtmlBody ?? string.Empty;
            await ApplyEditorContentAsync(html);
            _suppressRibbonEvents = false;
            await HtmlEditor.CoreWebView2.ExecuteScriptAsync("window.postSelectionFormat && window.postSelectionFormat()");
        }

        private async void CoreWebView2_WebMessageReceived(
            object? sender,
            Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var document = JsonDocument.Parse(e.WebMessageAsJson);
                if (!document.RootElement.TryGetProperty("type", out var typeElement))
                {
                    return;
                }

                switch (typeElement.GetString())
                {
                    case "editorContent":
                        if (ViewModel is not null
                            && document.RootElement.TryGetProperty("content", out var contentElement))
                        {
                            ViewModel.HtmlBody = contentElement.GetString() ?? string.Empty;
                        }
                        break;
                    case "requestLink":
                        await ShowLinkDialogAsync();
                        break;
                    case "requestSave":
                        if (ViewModel is not null)
                        {
                            ViewModel.StatusMessage = "Use File → Save Workspace to save your template.";
                        }
                        break;
                    case "openLink":
                        if (document.RootElement.TryGetProperty("url", out var urlElement))
                        {
                            await OpenLinkInBrowserAsync(urlElement.GetString());
                        }
                        break;
                    case "requestPaste":
                        await PasteFromClipboardAsync(formatted: true);
                        break;
                    case "requestPastePlain":
                        await PasteFromClipboardAsync(formatted: false);
                        break;
                    case "selectionFormat":
                        if (document.RootElement.TryGetProperty("state", out var stateElement))
                        {
                            UpdateRibbonFromEditor(stateElement);
                        }
                        break;
                }
            }
            catch
            {
                // Ignore malformed messages.
            }
        }

        private void UpdateRibbonFromEditor(JsonElement state)
        {
            _updatingRibbonFromEditor = true;
            try
            {
                BoldToggle.IsChecked = state.TryGetProperty("bold", out var bold) && bold.GetBoolean();
                ItalicToggle.IsChecked = state.TryGetProperty("italic", out var italic) && italic.GetBoolean();
                UnderlineToggle.IsChecked = state.TryGetProperty("underline", out var underline) && underline.GetBoolean();
                StrikethroughToggle.IsChecked = state.TryGetProperty("strikeThrough", out var strike) && strike.GetBoolean();
                SuperscriptToggle.IsChecked = state.TryGetProperty("superscript", out var sup) && sup.GetBoolean();
                SubscriptToggle.IsChecked = state.TryGetProperty("subscript", out var sub) && sub.GetBoolean();
                BulletToggle.IsChecked = state.TryGetProperty("unorderedList", out var bullets) && bullets.GetBoolean();
                NumberedToggle.IsChecked = state.TryGetProperty("orderedList", out var numbered) && numbered.GetBoolean();

                if (state.TryGetProperty("alignment", out var alignmentElement))
                {
                    var alignment = alignmentElement.GetString();
                    AlignLeftToggle.IsChecked = alignment == "left";
                    AlignCenterToggle.IsChecked = alignment == "center";
                    AlignRightToggle.IsChecked = alignment == "right";
                    JustifyToggle.IsChecked = alignment == "justify";
                }

                if (state.TryGetProperty("fontSize", out var fontSizeElement)
                    && fontSizeElement.ValueKind == JsonValueKind.Number)
                {
                    FontSizeCombo.Text = fontSizeElement.GetInt32().ToString();
                }

                if (state.TryGetProperty("fontFamily", out var fontFamilyElement))
                {
                    SelectFontFamilyInCombo(fontFamilyElement.GetString());
                }

                if (state.TryGetProperty("lineHeight", out var lineHeightElement))
                {
                    SelectLineSpacingInCombo(lineHeightElement.GetString());
                }

                _selectionInLink = state.TryGetProperty("inLink", out var inLinkElement) && inLinkElement.GetBoolean();
                UpdateLinkButtonState(_selectionInLink);
            }
            finally
            {
                _updatingRibbonFromEditor = false;
            }
        }

        private void SelectFontFamilyInCombo(string? fontFamily)
        {
            if (string.IsNullOrWhiteSpace(fontFamily))
            {
                return;
            }

            for (var i = 0; i < FontFamilyCombo.Items.Count; i++)
            {
                if (FontFamilyCombo.Items[i] is ComboBoxItem item
                    && string.Equals(item.Content?.ToString(), fontFamily, StringComparison.OrdinalIgnoreCase))
                {
                    FontFamilyCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectLineSpacingInCombo(string? lineHeight)
        {
            if (string.IsNullOrWhiteSpace(lineHeight))
            {
                return;
            }

            for (var i = 0; i < LineSpacingCombo.Items.Count; i++)
            {
                if (LineSpacingCombo.Items[i] is ComboBoxItem item
                    && string.Equals(item.Content?.ToString(), lineHeight, StringComparison.Ordinal))
                {
                    LineSpacingCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string GetFallbackEditorHtml()
        {
            return """
                <!DOCTYPE html><html><head><meta charset="utf-8" />
                <style>body{margin:0;background:#f3f3f3}#editor{min-height:400px;padding:48px;background:#fff;margin:24px auto;max-width:816px}</style>
                </head><body><div id="editor" contenteditable="true" spellcheck="false"></div>
                <script src="../js/setup-editor.js"></script>
                <script src="../js/editor-context-menu.js"></script></body></html>
                """;
        }

        private async void OnPasteClick(object sender, RoutedEventArgs e) =>
            await PasteFromClipboardAsync(formatted: false);

        private async Task PasteFromClipboardAsync(bool formatted)
        {
            var package = Clipboard.GetContent();
            if (formatted && package.Contains(StandardDataFormats.Html))
            {
                var rawHtml = await package.GetHtmlFormatAsync();
                var html = ExtractHtmlFragment(rawHtml);
                if (!string.IsNullOrWhiteSpace(html))
                {
                    await InvokeEditorFunctionAsync("insertHtmlAtSelection", html);
                    return;
                }
            }

            if (package.Contains(StandardDataFormats.Text))
            {
                var text = await package.GetTextAsync();
                await InvokeEditorFunctionAsync("pastePlainText", text ?? string.Empty);
            }
        }

        private static string ExtractHtmlFragment(string cfHtml)
        {
            if (string.IsNullOrEmpty(cfHtml))
            {
                return string.Empty;
            }

            var startMatch = Regex.Match(cfHtml, @"StartFragment:(\d+)");
            var endMatch = Regex.Match(cfHtml, @"EndFragment:(\d+)");
            if (!startMatch.Success || !endMatch.Success)
            {
                return cfHtml;
            }

            var start = int.Parse(startMatch.Groups[1].Value);
            var end = int.Parse(endMatch.Groups[1].Value);
            if (start < 0 || end <= start || end > cfHtml.Length)
            {
                return string.Empty;
            }

            return cfHtml[start..end];
        }

        private async void OnUndoClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("undo");

        private async void OnRedoClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("redo");

        private async void OnBoldClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("bold");

        private async void OnItalicClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("italic");

        private async void OnUnderlineClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("underline");

        private async void OnStrikethroughClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("strikeThrough");

        private async void OnSuperscriptClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("superscript");

        private async void OnSubscriptClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("subscript");

        private async void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressRibbonEvents || _updatingRibbonFromEditor || e.AddedItems.Count == 0 || FontFamilyCombo.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            await InvokeEditorFunctionAsync("setFontFamily", item.Content?.ToString() ?? "Segoe UI");
        }

        private async void OnFontSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressRibbonEvents || e.AddedItems.Count == 0)
            {
                return;
            }

            if (FontSizeCombo.SelectedItem is ComboBoxItem item)
            {
                FontSizeCombo.Text = item.Content?.ToString() ?? FontSizeCombo.Text;
            }

            await ApplyFontSizeFromComboAsync();
        }

        private async void OnFontSizeLostFocus(object sender, RoutedEventArgs e) =>
            await ApplyFontSizeFromComboAsync();

        private async void OnFontSizeKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await ApplyFontSizeFromComboAsync();
            }
        }

        private async Task ApplyFontSizeFromComboAsync()
        {
            if (_suppressRibbonEvents)
            {
                return;
            }

            var text = FontSizeCombo.Text?.Trim();
            if (!int.TryParse(text, out var size) || size is < 1 or > 400)
            {
                return;
            }

            await InvokeEditorFunctionAsync("setFontSize", size);
        }

        private async void TextColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            FontColorBar.Fill = new SolidColorBrush(args.NewColor);

            if (_suppressRibbonEvents)
            {
                return;
            }

            await InvokeEditorFunctionAsync("setForeColor", ToHex(args.NewColor));
        }

        private async void HighlightColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_suppressRibbonEvents)
            {
                return;
            }

            await InvokeEditorFunctionAsync("setBackColor", ToHex(args.NewColor));
        }

        private async void OnAlignLeftClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("justifyLeft");

        private async void OnAlignCenterClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("justifyCenter");

        private async void OnAlignRightClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("justifyRight");

        private async void OnJustifyClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("justifyFull");

        private async void OnBulletListClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("insertUnorderedList");

        private async void OnNumberedListClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("insertOrderedList");

        private async void OnIndentClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("indent");

        private async void OnOutdentClick(object sender, RoutedEventArgs e) =>
            await ExecEditorCommandAsync("outdent");

        private async void OnLineSpacingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressRibbonEvents || _updatingRibbonFromEditor || e.AddedItems.Count == 0 || LineSpacingCombo.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            await InvokeEditorFunctionAsync("setLineHeight", item.Content?.ToString() ?? "1.15");
        }

        private void UpdateLinkButtonState(bool inLink)
        {
            LinkInsertIcon.Visibility = inLink ? Visibility.Collapsed : Visibility.Visible;
            LinkRemoveIcon.Visibility = inLink ? Visibility.Visible : Visibility.Collapsed;
            ToolTipService.SetToolTip(LinkButton, inLink ? "Remove Link" : "Insert Link (Ctrl+K)");
        }

        private void BuildTableGridPicker()
        {
            TableGridPicker.Children.Clear();
            TableGridPicker.RowDefinitions.Clear();
            TableGridPicker.ColumnDefinitions.Clear();

            for (var row = 0; row < TableGridMaxRows; row++)
            {
                TableGridPicker.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            }

            for (var col = 0; col < TableGridMaxCols; col++)
            {
                TableGridPicker.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            }

            _tableGridCells = new Border[TableGridMaxRows, TableGridMaxCols];
            for (var row = 0; row < TableGridMaxRows; row++)
            {
                for (var col = 0; col < TableGridMaxCols; col++)
                {
                    var cellRow = row;
                    var cellCol = col;
                    var cell = new Border
                    {
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                        BorderThickness = new Thickness(1),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        Tag = (cellRow, cellCol)
                    };

                    cell.PointerEntered += (_, _) => UpdateTableGridHighlight(cellRow + 1, cellCol + 1);
                    cell.PointerPressed += async (_, _) => await InsertTableFromGridAsync(cellRow + 1, cellCol + 1);

                    Grid.SetRow(cell, cellRow);
                    Grid.SetColumn(cell, cellCol);
                    TableGridPicker.Children.Add(cell);
                    _tableGridCells[cellRow, cellCol] = cell;
                }
            }
        }

        private void UpdateTableGridHighlight(int rows, int cols)
        {
            if (_tableGridCells is null)
            {
                return;
            }

            rows = Math.Clamp(rows, 0, TableGridMaxRows);
            cols = Math.Clamp(cols, 0, TableGridMaxCols);

            InsertTableSizeLabel.Text = rows > 0 && cols > 0
                ? $"{rows} × {cols} Table"
                : "Insert Table";

            var activeBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 103, 192));
            var idleBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            for (var row = 0; row < TableGridMaxRows; row++)
            {
                for (var col = 0; col < TableGridMaxCols; col++)
                {
                    _tableGridCells[row, col].Background = row < rows && col < cols ? activeBrush : idleBrush;
                }
            }
        }

        private async Task InsertTableFromGridAsync(int rows, int cols)
        {
            InsertTableFlyout.Hide();
            UpdateTableGridHighlight(0, 0);
            await InvokeEditorFunctionAsync("insertTable", rows, cols);
        }

        private async void OnLinkButtonClick(object sender, RoutedEventArgs e)
        {
            if (_selectionInLink)
            {
                await InvokeEditorFunctionAsync("removeLink");
                return;
            }

            await ShowLinkDialogAsync();
        }

        private async void OnInsertImageClick(object sender, RoutedEventArgs e) =>
            await PickAndInsertImageAsync();

        private async void OnHorizontalRuleClick(object sender, RoutedEventArgs e) =>
            await InvokeEditorFunctionAsync("insertHorizontalRule");

        private async void OnClearFormattingClick(object sender, RoutedEventArgs e) =>
            await InvokeEditorFunctionAsync("clearFormatting");

        private async void OnClearClick(object sender, RoutedEventArgs e) =>
            await InvokeEditorFunctionAsync("clearEditor");

        private async void OnToggleHtmlSourceClick(object sender, RoutedEventArgs e)
        {
            if (!_isHtmlSourceMode)
            {
                await SyncEditorToViewModelAsync();
                HtmlSourceBox.Text = ViewModel?.HtmlBody ?? string.Empty;
                HtmlEditor.Visibility = Visibility.Collapsed;
                HtmlSourceBox.Visibility = Visibility.Visible;
                _isHtmlSourceMode = true;
                if (ViewModel is not null)
                {
                    ViewModel.StatusMessage = "HTML source mode — click HTML button again to return to editor.";
                }
                return;
            }

            if (ViewModel is not null)
            {
                ViewModel.HtmlBody = HtmlSourceBox.Text;
                await ApplyEditorContentAsync(ViewModel.HtmlBody);
            }

            HtmlSourceBox.Visibility = Visibility.Collapsed;
            HtmlEditor.Visibility = Visibility.Visible;
            _isHtmlSourceMode = false;
            if (ViewModel is not null)
            {
                ViewModel.StatusMessage = "Visual editor mode.";
            }
        }

        private async void OnInsertVariableClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string varName)
            {
                await InsertVariableIntoEditorAsync(varName);
            }
        }

        private async void OnInsertVariableFromListClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string varName)
            {
                await InsertVariableIntoEditorAsync(varName);
            }
        }

        private Task InsertVariableIntoEditorAsync(string varName) =>
            InvokeEditorFunctionAsync("insertVariable", $"{{{varName}}}");

        private async Task ShowLinkDialogAsync()
        {
            var urlBox = new TextBox { PlaceholderText = "https://example.com", MinWidth = 320 };
            var textBox = new TextBox { PlaceholderText = "Display text (optional)", MinWidth = 320 };

            var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
            panel.Children.Add(new TextBlock { Text = "URL", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(urlBox);
            panel.Children.Add(new TextBlock { Text = "Text to display", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(textBox);

            var dialog = new ContentDialog
            {
                Title = "Insert Link",
                Content = panel,
                PrimaryButtonText = "Insert",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var url = urlBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            await InvokeEditorFunctionAsync("insertLink", url, textBox.Text.Trim());
        }

        private async Task PickAndInsertImageAsync()
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".webp");

            var hwnd = WindowNative.GetWindowHandle(AppServices.GetMainWindow());
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var buffer = await FileIO.ReadBufferAsync(file);
            var bytes = buffer.ToArray();
            var mime = GetMimeType(file.FileType);
            var base64 = Convert.ToBase64String(bytes);
            var dataUrl = $"data:{mime};base64,{base64}";
            await InvokeEditorFunctionAsync("insertImage", dataUrl, file.Name);
        }

        private static string GetMimeType(string fileType) =>
            fileType.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };

        private static string ToHex(Windows.UI.Color color) =>
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
