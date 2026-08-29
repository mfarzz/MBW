using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MBW.App.ViewModels;
using System;
using System.Threading.Tasks;
using System.IO;

namespace MBW.App.Views
{
    public sealed partial class EmailEditorPage : Page
    {
        private EmailEditorViewModel? ViewModel => this.DataContext as EmailEditorViewModel;

        public EmailEditorPage()
        {
            this.InitializeComponent();
            this.DataContext = Composition.AppServices.CreateEmailEditorViewModel();
            this.Loaded += EmailEditorPage_Loaded;
        }

        private void EmailEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PreviewRequested += ViewModel_PreviewRequested;
            }
            _ = InitializeWebView2Async();
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
                var item = new MenuFlyoutItem
                {
                    Text = variable,
                    Tag = variable
                };
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
                XamlRoot = this.XamlRoot
            };

            var content = new StackPanel
            {
                Spacing = 12,
                Padding = new Thickness(12, 12, 12, 12)
            };

            content.Children.Add(new TextBlock 
            { 
                Text = $"From: {preview.FromEmail}", 
                FontSize = 12 
            });

            content.Children.Add(new TextBlock 
            { 
                Text = $"To: {preview.ToEmail}", 
                FontSize = 12 
            });

            content.Children.Add(new TextBlock 
            { 
                Text = $"Subject: {preview.Subject}", 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                FontSize = 13 
            });

            content.Children.Add(new Border
            {
                Height = 1,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LightGray)
            });

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 400,
                Content = new TextBlock
                {
                    Text = preview.HtmlBody,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12
                }
            };

            content.Children.Add(scrollViewer);

            dialog.Content = content;

            await dialog.ShowAsync();
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                // Initialize WebView2
                await HtmlEditor.EnsureCoreWebView2Async();

                // Load HTML editor
                var editorHtmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "html", "editor.html");
                if (File.Exists(editorHtmlPath))
                {
                    HtmlEditor.Source = new Uri($"file:///{editorHtmlPath}");
                }
                else
                {
                    // Fallback: Create inline HTML
                    var html = @"
                    <html>
                    <head>
                        <style>
                            body { margin: 0; padding: 0; font-family: 'Segoe UI', sans-serif; }
                            #editor { width: 100%; min-height: 300px; padding: 16px; border: none; 
                                     outline: none; font-size: 14px; line-height: 1.6; color: #333; }
                            .variable { color: #0078D4; font-weight: 500; background-color: #E7F3FF; 
                                       padding: 2px 4px; border-radius: 2px; cursor: default; }
                        </style>
                    </head>
                    <body>
                        <div id='editor'></div>
                        <script>
                            const editor = document.getElementById('editor');
                            editor.contentEditable = true;
                            editor.spellcheck = true;
                            editor.style.outline = 'none';

                            window.insertVariable = function(varName) {
                                const selection = window.getSelection();
                                if (selection.rangeCount > 0) {
                                    const range = selection.getRangeAt(0);
                                    const span = document.createElement('span');
                                    span.className = 'variable';
                                    span.textContent = varName;
                                    span.style.color = '#0078D4';
                                    span.style.fontWeight = '500';
                                    span.style.backgroundColor = '#E7F3FF';
                                    span.style.padding = '2px 4px';
                                    span.style.borderRadius = '2px';
                                    range.insertNode(span);
                                    range.setStartAfter(span);
                                    range.collapse(true);
                                    selection.removeAllRanges();
                                    selection.addRange(range);
                                    editor.focus();
                                }
                            };

                            window.getEditorContent = function() { return editor.innerHTML; };
                            window.setEditorContent = function(html) { editor.innerHTML = html; };
                            window.clearEditor = function() { editor.innerHTML = ''; editor.focus(); };

                            editor.focus();
                        </script>
                    </body>
                    </html>";
                    HtmlEditor.NavigateToString(html);
                }

                // Optional: Set message received handler
                HtmlEditor.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization error: {ex.Message}");
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Handle messages from JavaScript if needed
            System.Diagnostics.Debug.WriteLine($"Message from WebView2: {e.WebMessageAsJson}");
        }

        private void OnUndoClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('undo')");
        }

        private void OnRedoClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('redo')");
        }

        private void OnBoldClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('bold')");
        }

        private void OnItalicClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('italic')");
        }

        private void OnUnderlineClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('underline')");
        }

        private void OnFontSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            {
                var fontSize = item.Content?.ToString() ?? "14";
                HtmlEditor?.CoreWebView2?.ExecuteScriptAsync($"document.execCommand('fontSize', false, '{fontSize}')");
            }
        }

        private void OnAlignLeftClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('justifyLeft')");
        }

        private void OnAlignCenterClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('justifyCenter')");
        }

        private void OnAlignRightClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('justifyRight')");
        }

        private void OnBulletListClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('insertUnorderedList')");
        }

        private void OnNumberedListClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('insertOrderedList')");
        }

        private void OnLinkClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("document.execCommand('createLink', false, 'https://example.com')");
        }

        private void OnInsertVariableClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string varName)
            {
                var variable = $"{{{varName}}}";
                HtmlEditor.CoreWebView2?.ExecuteScriptAsync($"insertVariable('{variable}')");
            }
        }

        private void OnInsertVariableFromListClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string varName)
            {
                var variable = $"{{{varName}}}";
                HtmlEditor.CoreWebView2?.ExecuteScriptAsync($"insertVariable('{variable}')");
            }
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            HtmlEditor.CoreWebView2?.ExecuteScriptAsync("editor.innerHTML = ''");
        }
    }
}
