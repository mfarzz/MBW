using MBW.App.Composition;
using MBW.App.Shell;
using MBW.App.ViewModels;
using MBW.App.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Windows.System;

namespace MBW.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Button> _mainNavButtons = new();
        private readonly Dictionary<string, Border> _navRails = new();
        private readonly Dictionary<string, IconElement> _navIcons = new();
        private readonly ShellViewModel _shellViewModel;
        private bool _isProjectOpen;
        private string _currentTag = "Welcome";
        private bool _sidebarVisible = true;
        private bool _statusBarVisible = true;

        public ShellViewModel ShellViewModel => _shellViewModel;

        public MainWindow()
        {
            InitializeComponent();

            AppServices.Initialize(this);
            _shellViewModel = AppServices.CreateShellViewModel();
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            _shellViewModel.WorkspaceChanged += (_, _) => EnterProjectMode();
            _shellViewModel.NavigationRequested += (_, tag) => NavigateFromWorkspaceMenu(tag);
            AppServices.WelcomeViewModel.ProjectOpened += (_, _) => EnterProjectMode();
            SyncShellLabels();

            ConfigureTitleBar();
            RegisterNavElements();

            ShowWelcomeScreen();
            _ = _shellViewModel.InitializeAsync();
        }

        private void RegisterNavElements()
        {
            AddButtonIfExists(_mainNavButtons, "Email", "EmailNavButton");
            AddButtonIfExists(_mainNavButtons, "Database", "DatabaseNavButton");
            AddButtonIfExists(_mainNavButtons, "Attachments", "AttachmentsNavButton");
            AddButtonIfExists(_mainNavButtons, "Configuration", "ConfigurationNavButton");
            AddButtonIfExists(_mainNavButtons, "Send", "SendNavButton");

            AddRailIfExists("Email", "EmailNavRail");
            AddRailIfExists("Database", "DatabaseNavRail");
            AddRailIfExists("Attachments", "AttachmentsNavRail");
            AddRailIfExists("Configuration", "ConfigurationNavRail");
            AddRailIfExists("Send", "SendNavRail");

            if (FindElement<SymbolIcon>("EmailNavIcon") is SymbolIcon emailIcon)
            {
                _navIcons["Email"] = emailIcon;
            }

            if (FindElement<FontIcon>("DatabaseNavIcon") is FontIcon databaseIcon)
            {
                _navIcons["Database"] = databaseIcon;
            }

            if (FindElement<SymbolIcon>("AttachmentsNavIcon") is SymbolIcon attachmentsIcon)
            {
                _navIcons["Attachments"] = attachmentsIcon;
            }

            if (FindElement<SymbolIcon>("ConfigurationNavIcon") is SymbolIcon configurationIcon)
            {
                _navIcons["Configuration"] = configurationIcon;
            }

            if (FindElement<SymbolIcon>("SendNavIcon") is SymbolIcon sendIcon)
            {
                _navIcons["Send"] = sendIcon;
            }
        }

        private void ConfigureTitleBar()
        {
            ExtendsContentIntoTitleBar = true;

            if (FindElement<Grid>("AppTitleBar") is Grid titleBar)
            {
                SetTitleBar(titleBar);
                titleBar.SizeChanged += (_, _) => UpdateTitleBarPadding();
            }

            var appTitleBar = AppWindow.TitleBar;
            appTitleBar.ExtendsContentIntoTitleBar = true;
            appTitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;

            appTitleBar.BackgroundColor = ColorHelper.FromArgb(255, 243, 243, 243);
            appTitleBar.InactiveBackgroundColor = ColorHelper.FromArgb(255, 243, 243, 243);
            appTitleBar.ForegroundColor = ColorHelper.FromArgb(255, 27, 27, 27);
            appTitleBar.InactiveForegroundColor = ColorHelper.FromArgb(255, 138, 136, 134);

            appTitleBar.ButtonBackgroundColor = Colors.Transparent;
            appTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            appTitleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(20, 0, 0, 0);
            appTitleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(32, 0, 0, 0);
            appTitleBar.ButtonForegroundColor = ColorHelper.FromArgb(255, 27, 27, 27);
            appTitleBar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(255, 138, 136, 134);

            Activated += (_, _) => UpdateTitleBarPadding();
            UpdateTitleBarPadding();
        }

        private void UpdateTitleBarPadding()
        {
            if (FindElement<Grid>("AppTitleBar") is not Grid titleBar)
            {
                return;
            }

            titleBar.Padding = new Thickness(0, 0, AppWindow.TitleBar.RightInset, 0);
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                if (tag == "Send")
                {
                    _ = OpenSendPageAsync();
                    return;
                }

                NavigateToTag(tag);
            }
        }

        private async void FileNewWorkspace_Click(object sender, RoutedEventArgs e)
        {
            await RunFileCommandAsync(_shellViewModel.NewWorkspaceAsync);
        }

        private async void FileOpenWorkspace_Click(object sender, RoutedEventArgs e)
        {
            await RunFileCommandAsync(_shellViewModel.OpenWorkspaceAsync);
        }

        private async void FileSaveWorkspace_Click(object sender, RoutedEventArgs e)
        {
            await RunFileCommandAsync(SaveWorkspaceFromMenuAsync);
        }

        private void FileExit_Click(object sender, RoutedEventArgs e)
        {
            CloseFileMenuFlyout();
            Close();
        }

        private async void FileNewWorkspace_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await RunFileCommandAsync(_shellViewModel.NewWorkspaceAsync);
        }

        private async void FileOpenWorkspace_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await RunFileCommandAsync(_shellViewModel.OpenWorkspaceAsync);
        }

        private async void FileSaveWorkspace_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await RunFileCommandAsync(SaveWorkspaceFromMenuAsync);
        }

        private void FileExit_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            Close();
        }

        private async Task RunFileCommandAsync(Func<Task> command)
        {
            CloseFileMenuFlyout();
            await Task.Yield();
            await command();
        }

        private async Task SaveWorkspaceFromMenuAsync()
        {
            await SyncEmailEditorToCoordinator();
            await _shellViewModel.SaveWorkspaceAsync();
        }

        private void CloseFileMenuFlyout()
        {
            if (FindElement<Button>("FileMenuButton")?.Flyout is FlyoutBase flyout && flyout.IsOpen)
            {
                flyout.Hide();
            }
        }

        private void EditMenuFlyout_Opening(object sender, object e) => RefreshEditMenuState();

        private void RefreshEditMenuState()
        {
            var target = GetEditTarget();
            SetMenuItemEnabled("EditUndoItem", target?.CanUndo == true);
            SetMenuItemEnabled("EditRedoItem", target?.CanRedo == true);
            SetMenuItemEnabled("EditCutItem", target?.CanCut == true);
            SetMenuItemEnabled("EditCopyItem", target?.CanCopy == true);
            SetMenuItemEnabled("EditPasteItem", target?.CanPaste == true);
            SetMenuItemEnabled("EditPastePlainItem", target?.CanPastePlain == true);
            SetMenuItemEnabled("EditSelectAllItem", target?.CanSelectAll == true);
        }

        private IShellEditTarget? GetEditTarget() => RootFrame.Content as IShellEditTarget;

        private async void EditUndo_Click(object sender, RoutedEventArgs e) => await RunEditCommandAsync(t => t.CanUndo, t => t.UndoAsync());
        private async void EditRedo_Click(object sender, RoutedEventArgs e) => await RunEditCommandAsync(t => t.CanRedo, t => t.RedoAsync());
        private async void EditCut_Click(object sender, RoutedEventArgs e) => await RunEditCommandAsync(t => t.CanCut, t => t.CutAsync());
        private async void EditCopy_Click(object sender, RoutedEventArgs e) => await RunEditCommandAsync(t => t.CanCopy, t => t.CopyAsync());
        private async void EditPaste_Click(object sender, RoutedEventArgs e) => await RunEditCommandAsync(t => t.CanPaste, t => t.PasteAsync());
        private async void EditPastePlain_Click(object sender, RoutedEventArgs e) => await RunEditCommandAsync(t => t.CanPastePlain, t => t.PastePlainAsync());
        private async void EditSelectAll_Click(object sender, RoutedEventArgs e) => await RunEditCommandAsync(t => t.CanSelectAll, t => t.SelectAllAsync());

        private async void EditUndo_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (await TryEditCommandAsync(t => t.CanUndo, t => t.UndoAsync()))
            {
                args.Handled = true;
            }
        }

        private async void EditRedo_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (await TryEditCommandAsync(t => t.CanRedo, t => t.RedoAsync()))
            {
                args.Handled = true;
            }
        }

        private async void EditCut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (await TryEditCommandAsync(t => t.CanCut, t => t.CutAsync()))
            {
                args.Handled = true;
            }
        }

        private async void EditCopy_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (await TryEditCommandAsync(t => t.CanCopy, t => t.CopyAsync()))
            {
                args.Handled = true;
            }
        }

        private async void EditPaste_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (await TryEditCommandAsync(t => t.CanPaste, t => t.PasteAsync()))
            {
                args.Handled = true;
            }
        }

        private async void EditPastePlain_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (await TryEditCommandAsync(t => t.CanPastePlain, t => t.PastePlainAsync()))
            {
                args.Handled = true;
            }
        }

        private async void EditSelectAll_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (await TryEditCommandAsync(t => t.CanSelectAll, t => t.SelectAllAsync()))
            {
                args.Handled = true;
            }
        }

        private async Task RunEditCommandAsync(Func<IShellEditTarget, bool> canExecute, Func<IShellEditTarget, Task> action)
        {
            CloseEditMenuFlyout();
            await TryEditCommandAsync(canExecute, action);
        }

        private async Task<bool> TryEditCommandAsync(Func<IShellEditTarget, bool> canExecute, Func<IShellEditTarget, Task> action)
        {
            var target = GetEditTarget();
            if (target is null || !canExecute(target))
            {
                return false;
            }

            await action(target);
            return true;
        }

        private void CloseEditMenuFlyout()
        {
            if (FindElement<Button>("EditMenuButton")?.Flyout is FlyoutBase flyout && flyout.IsOpen)
            {
                flyout.Hide();
            }
        }

        private void ViewMenuFlyout_Opening(object sender, object e)
        {
            if (FindElement<ToggleMenuFlyoutItem>("ViewSidebarItem") is ToggleMenuFlyoutItem sidebarItem)
            {
                sidebarItem.IsChecked = _sidebarVisible;
                sidebarItem.IsEnabled = _isProjectOpen;
            }

            if (FindElement<ToggleMenuFlyoutItem>("ViewStatusBarItem") is ToggleMenuFlyoutItem statusItem)
            {
                statusItem.IsChecked = _statusBarVisible;
                statusItem.IsEnabled = _isProjectOpen;
            }

            SetMenuItemEnabled("ViewEmailItem", _isProjectOpen);
            SetMenuItemEnabled("ViewDatabaseItem", _isProjectOpen);
            SetMenuItemEnabled("ViewAttachmentsItem", _isProjectOpen);
            SetMenuItemEnabled("ViewConfigurationItem", _isProjectOpen);
            SetMenuItemEnabled("ViewSendItem", _isProjectOpen);
            SetMenuItemEnabled("ViewPreviewItem", _isProjectOpen);
            SetMenuItemEnabled("ViewRefreshItem", _isProjectOpen && RootFrame.Content is IShellRefreshable);
        }

        private void ViewSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (!_isProjectOpen)
            {
                return;
            }

            _sidebarVisible = FindElement<ToggleMenuFlyoutItem>("ViewSidebarItem")?.IsChecked == true;
            ApplyChromeLayout();
        }

        private void ViewStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (!_isProjectOpen)
            {
                return;
            }

            _statusBarVisible = FindElement<ToggleMenuFlyoutItem>("ViewStatusBarItem")?.IsChecked == true;
            ApplyChromeLayout();
        }

        private void ViewSidebar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen)
            {
                return;
            }

            args.Handled = true;
            _sidebarVisible = !_sidebarVisible;
            ApplyChromeLayout();
        }

        private void ViewEmail_Click(object sender, RoutedEventArgs e) => NavigateFromViewMenu("Email");
        private void ViewDatabase_Click(object sender, RoutedEventArgs e) => NavigateFromViewMenu("Database");
        private void ViewAttachments_Click(object sender, RoutedEventArgs e) => NavigateFromViewMenu("Attachments");
        private void ViewConfiguration_Click(object sender, RoutedEventArgs e) => NavigateFromViewMenu("Configuration");

        private async void ViewSend_Click(object sender, RoutedEventArgs e)
        {
            CloseViewMenuFlyout();
            if (_isProjectOpen)
            {
                await OpenSendPageAsync();
            }
        }

        private async void ViewPreview_Click(object sender, RoutedEventArgs e)
        {
            CloseViewMenuFlyout();
            if (_isProjectOpen)
            {
                await OpenSendPageAsync();
            }
        }

        private async void ViewRefresh_Click(object sender, RoutedEventArgs e)
        {
            CloseViewMenuFlyout();
            await RefreshCurrentPageAsync();
        }

        private void ViewEmail_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen) return;
            args.Handled = true;
            NavigateToTag("Email");
        }

        private void ViewDatabase_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen) return;
            args.Handled = true;
            NavigateToTag("Database");
        }

        private void ViewAttachments_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen) return;
            args.Handled = true;
            NavigateToTag("Attachments");
        }

        private void ViewConfiguration_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen) return;
            args.Handled = true;
            NavigateToTag("Configuration");
        }

        private async void ViewSend_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen) return;
            args.Handled = true;
            await OpenSendPageAsync();
        }

        private async void ViewPreview_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen) return;
            args.Handled = true;
            await OpenSendPageAsync();
        }

        private async void ViewRefresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isProjectOpen) return;
            args.Handled = true;
            await RefreshCurrentPageAsync();
        }

        private void NavigateFromViewMenu(string tag)
        {
            CloseViewMenuFlyout();
            NavigateToTag(tag);
        }

        private async Task RefreshCurrentPageAsync()
        {
            if (RootFrame.Content is IShellRefreshable refreshable)
            {
                await refreshable.RefreshAsync();
            }
        }

        private void CloseViewMenuFlyout()
        {
            if (FindElement<Button>("ViewMenuButton")?.Flyout is FlyoutBase flyout && flyout.IsOpen)
            {
                flyout.Hide();
            }
        }

        private async void HelpDocumentation_Click(object sender, RoutedEventArgs e)
        {
            CloseHelpMenuFlyout();
            await ShowDocumentationAsync();
        }

        private async void HelpShortcuts_Click(object sender, RoutedEventArgs e)
        {
            CloseHelpMenuFlyout();
            await ShowShortcutsAsync();
        }

        private async void HelpAbout_Click(object sender, RoutedEventArgs e)
        {
            CloseHelpMenuFlyout();
            await ShowAboutAsync();
        }

        private async void HelpDocumentation_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await ShowDocumentationAsync();
        }

        private async Task ShowDocumentationAsync()
        {
            var readmePath = FindReadmePath();
            if (readmePath is not null)
            {
                await Launcher.LaunchUriAsync(new Uri(readmePath));
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Getting started",
                Content = new TextBlock
                {
                    Text =
                        "1. File → New/Open Workspace\n" +
                        "2. Email — write the HTML template\n" +
                        "3. Database — import recipients from Excel\n" +
                        "4. Attachments — add shared/individual files\n" +
                        "5. Workspace → SMTP — configure sender\n" +
                        "6. Send — preview, set range/delay, send\n\n" +
                        "See README.md in the project folder for full documentation.",
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowShortcutsAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "Keyboard shortcuts",
                Content = new ScrollViewer
                {
                    MaxHeight = 420,
                    Content = new TextBlock
                    {
                        Text =
                            "File\n" +
                            "  Ctrl+N        New workspace\n" +
                            "  Ctrl+O        Open workspace\n" +
                            "  Ctrl+S        Save workspace\n" +
                            "  Alt+F4        Exit\n\n" +
                            "Edit\n" +
                            "  Ctrl+Z        Undo\n" +
                            "  Ctrl+Y        Redo\n" +
                            "  Ctrl+X / C / V  Cut / Copy / Paste\n" +
                            "  Ctrl+Shift+V  Paste as plain text\n" +
                            "  Ctrl+A        Select all\n\n" +
                            "View\n" +
                            "  Ctrl+B        Toggle sidebar\n" +
                            "  Ctrl+1…5      Email / Database / Attachments / Configuration / Send\n" +
                            "  Ctrl+Shift+P  Preview email (Send)\n" +
                            "  F5            Refresh current page\n\n" +
                            "Help\n" +
                            "  F1            Documentation",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                },
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowAboutAsync()
        {
            var version = GetAppVersion();
            var dialog = new ContentDialog
            {
                Title = "About MBW",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "MBW — MailBlast Workspace",
                            FontSize = 16,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        },
                        new TextBlock { Text = $"Version {version}" },
                        new TextBlock
                        {
                            Text = "A local-first Windows app for creating, previewing, and sending mail-merge email campaigns.",
                            TextWrapping = TextWrapping.WrapWholeWords
                        }
                    }
                },
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private static string GetAppVersion()
        {
            try
            {
                var v = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            catch
            {
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            }
        }

        private static string? FindReadmePath()
        {
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                for (var i = 0; i < 6 && dir is not null; i++)
                {
                    var candidate = Path.Combine(dir.FullName, "README.md");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    dir = dir.Parent;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private void CloseHelpMenuFlyout()
        {
            if (FindElement<Button>("HelpMenuButton")?.Flyout is FlyoutBase flyout && flyout.IsOpen)
            {
                flyout.Hide();
            }
        }

        private void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ShellViewModel.WorkspaceName)
                or nameof(ShellViewModel.StatusWorkspaceName)
                or nameof(ShellViewModel.WorkspaceSavedText)
                or nameof(ShellViewModel.SmtpStatusText)
                or nameof(ShellViewModel.SmtpIsConnected)
                or nameof(ShellViewModel.DatabaseStatusText)
                or nameof(ShellViewModel.AttachmentStatusText))
            {
                SyncShellLabels();
            }
        }

        private void SyncShellLabels()
        {
            if (FindElement<TextBlock>("WorkspaceNameText") is TextBlock workspaceName)
            {
                workspaceName.Text = _shellViewModel.WorkspaceName;
            }

            if (FindElement<TextBlock>("StatusWorkspaceText") is TextBlock statusWorkspace)
            {
                statusWorkspace.Text = _shellViewModel.StatusWorkspaceName;
            }

            if (FindElement<TextBlock>("WorkspaceSavedText") is TextBlock savedText)
            {
                savedText.Text = _shellViewModel.WorkspaceSavedText;
            }

            if (FindElement<TextBlock>("SmtpStatusText") is TextBlock smtpStatus)
            {
                smtpStatus.Text = _shellViewModel.SmtpStatusText;
            }

            if (FindElement<Microsoft.UI.Xaml.Shapes.Ellipse>("SmtpDot") is Microsoft.UI.Xaml.Shapes.Ellipse smtpDot)
            {
                smtpDot.Fill = _shellViewModel.SmtpIsConnected
                    ? GetThemeBrush("SystemFillColorSuccess")
                    : GetThemeBrush("SystemFillColorCritical");
            }

            if (FindElement<TextBlock>("DatabaseStatusText") is TextBlock databaseStatus)
            {
                databaseStatus.Text = _shellViewModel.DatabaseStatusText;
            }

            if (FindElement<TextBlock>("AttachmentStatusText") is TextBlock attachmentStatus)
            {
                attachmentStatus.Text = _shellViewModel.AttachmentStatusText;
            }
        }

        private void EnterProjectMode()
        {
            if (_isProjectOpen)
            {
                _ = ReloadEmailEditorAsync();
                return;
            }

            _isProjectOpen = true;
            _sidebarVisible = true;
            _statusBarVisible = true;
            SetShellChromeVisible(true);
            NavigateToTag("Email");
            _ = ReloadEmailEditorAsync();
        }

        private void ShowWelcomeScreen()
        {
            _isProjectOpen = false;
            SetShellChromeVisible(false);
            NavigateToWelcome();
        }

        private void SetShellChromeVisible(bool visible)
        {
            if (!visible)
            {
                SidebarColumn.Width = new GridLength(0);
                StatusBarRow.Height = new GridLength(0);
                ShellSidebarPanel.Visibility = Visibility.Collapsed;
                ShellStatusBar.Visibility = Visibility.Collapsed;
                ShellBodyGrid.BorderThickness = new Thickness(0);

                if (FindElement<Button>("WorkspaceMenuButton") is Button workspaceMenu)
                {
                    workspaceMenu.Visibility = Visibility.Collapsed;
                }

                if (FindElement<Button>("SmtpButton") is Button smtpButton)
                {
                    smtpButton.Visibility = Visibility.Collapsed;
                }

                Grid.SetColumn(RootFrame, 0);
                Grid.SetColumnSpan(RootFrame, 2);
                RootFrame.Background = Application.Current.Resources["MBWStatusBarBrush"] as Brush ?? TransparentBrush;
                RootLayoutGrid.Background = Application.Current.Resources["MBWStatusBarBrush"] as Brush ?? TransparentBrush;
                return;
            }

            if (FindElement<Button>("WorkspaceMenuButton") is Button wsMenu)
            {
                wsMenu.Visibility = Visibility.Visible;
            }

            if (FindElement<Button>("SmtpButton") is Button smtp)
            {
                smtp.Visibility = Visibility.Visible;
            }

            ShellBodyGrid.BorderThickness = new Thickness(0, 1, 0, 0);
            RootFrame.Background = GetThemeBrush("ApplicationPageBackgroundThemeBrush");
            RootLayoutGrid.Background = Application.Current.Resources["MBWSidebarBrush"] as Brush ?? TransparentBrush;
            ApplyChromeLayout();
        }

        private void ApplyChromeLayout()
        {
            if (!_isProjectOpen)
            {
                return;
            }

            var showSidebar = _sidebarVisible;
            var showStatus = _statusBarVisible;

            SidebarColumn.Width = showSidebar
                ? (GridLength)Application.Current.Resources["ShellSidebarWidth"]
                : new GridLength(0);
            StatusBarRow.Height = showStatus
                ? (GridLength)Application.Current.Resources["ShellStatusBarHeight"]
                : new GridLength(0);

            ShellSidebarPanel.Visibility = showSidebar ? Visibility.Visible : Visibility.Collapsed;
            ShellStatusBar.Visibility = showStatus ? Visibility.Visible : Visibility.Collapsed;

            Grid.SetColumn(RootFrame, showSidebar ? 1 : 0);
            Grid.SetColumnSpan(RootFrame, showSidebar ? 1 : 2);
        }

        private void NavigateToWelcome()
        {
            if (RootFrame.CurrentSourcePageType != typeof(WelcomePage))
            {
                RootFrame.Navigate(typeof(WelcomePage));
            }

            _currentTag = "Welcome";
        }

        private async Task ReloadEmailEditorAsync()
        {
            var path = _shellViewModel.CurrentWorkspacePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (RootFrame.Content is EmailEditorPage { DataContext: EmailEditorViewModel viewModel })
            {
                await viewModel.LoadWorkspaceAsync(path);
            }
        }

        private async Task SyncEmailEditorToCoordinator()
        {
            if (RootFrame.Content is EmailEditorPage page)
            {
                await page.SyncEditorToViewModelAsync();
            }

            if (RootFrame.Content is FrameworkElement { DataContext: EmailEditorViewModel viewModel })
            {
                _shellViewModel.ApplyEmailTemplate(await viewModel.GetCurrentTemplateAsync());
            }
        }

        private async void SmtpButton_Click(object sender, RoutedEventArgs e)
        {
            await RunSmtpCommandAsync();
        }

        private void WorkspaceDatabase_Click(object sender, RoutedEventArgs e)
        {
            CloseWorkspaceMenuFlyout();
            NavigateToTag("Database");
        }

        private void WorkspaceAttachments_Click(object sender, RoutedEventArgs e)
        {
            CloseWorkspaceMenuFlyout();
            NavigateToTag("Attachments");
        }

        private void WorkspaceConfiguration_Click(object sender, RoutedEventArgs e)
        {
            CloseWorkspaceMenuFlyout();
            NavigateFromWorkspaceMenu("Configuration");
        }

        private async void WorkspaceSend_Click(object sender, RoutedEventArgs e)
        {
            CloseWorkspaceMenuFlyout();
            await OpenSendPageAsync();
        }

        private async void WorkspaceSmtp_Click(object sender, RoutedEventArgs e)
        {
            await RunSmtpCommandAsync();
        }

        private void NavigateFromWorkspaceMenu(string tag) => NavigateToTag(tag);

        private async Task RunSmtpCommandAsync()
        {
            CloseWorkspaceMenuFlyout();
            await Task.Yield();
            await _shellViewModel.ShowSmtpSettingsAsync();
        }

        private void CloseWorkspaceMenuFlyout()
        {
            if (FindElement<Button>("WorkspaceMenuButton")?.Flyout is FlyoutBase flyout && flyout.IsOpen)
            {
                flyout.Hide();
            }
        }

        public async Task OpenSendPageAsync()
        {
            await SyncEmailEditorToCoordinator();
            NavigateToTag("Send");
        }

        private void NavigateToTag(string tag)
        {
            if (!_isProjectOpen && tag != "Welcome")
            {
                return;
            }

            var pageType = tag switch
            {
                "Welcome" => typeof(WelcomePage),
                "Email" => typeof(EmailEditorPage),
                "Database" => typeof(DatabasePage),
                "Attachments" => typeof(AttachmentsPage),
                "Configuration" => typeof(ConfigurationPage),
                "Send" => typeof(SendPage),
                _ => typeof(EmailEditorPage)
            };

            if (RootFrame.CurrentSourcePageType != pageType)
            {
                RootFrame.Navigate(pageType);
            }
            else if (tag == "Configuration" && RootFrame.Content is ConfigurationPage configurationPage)
            {
                _ = configurationPage.ReloadAsync();
            }
            else if (tag == "Send" && RootFrame.Content is SendPage sendPage)
            {
                _ = sendPage.ReloadAsync();
            }

            _currentTag = tag;
            RefreshShellState();
        }

        private void RefreshShellState()
        {
            if (!_isProjectOpen || _currentTag == "Welcome")
            {
                return;
            }

            foreach (var pair in _mainNavButtons)
            {
                ApplyMainNavVisual(pair.Key, pair.Value, pair.Key == _currentTag);
            }
        }

        private void ApplyMainNavVisual(string tag, Button button, bool isActive)
        {
            button.Background = isActive
                ? GetThemeBrush("AccentFillColorSecondaryBrush")
                : TransparentBrush;

            if (_navRails.TryGetValue(tag, out var rail))
            {
                rail.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_navIcons.TryGetValue(tag, out var icon))
            {
                icon.Foreground = isActive
                    ? GetThemeBrush("AccentFillColorDefaultBrush")
                    : GetThemeBrush("TextFillColorSecondary");
            }
        }

        private void SetMenuItemEnabled(string name, bool enabled)
        {
            if (FindElement<MenuFlyoutItem>(name) is MenuFlyoutItem item)
            {
                item.IsEnabled = enabled;
            }
        }

        private static Brush GetThemeBrush(string key)
        {
            return Application.Current.Resources[key] as Brush ?? TransparentBrush;
        }

        private static readonly SolidColorBrush TransparentBrush = new(Microsoft.UI.Colors.Transparent);

        private void AddButtonIfExists(Dictionary<string, Button> map, string key, string controlName)
        {
            if (FindElement<Button>(controlName) is Button button)
            {
                map[key] = button;
            }
        }

        private void AddRailIfExists(string key, string controlName)
        {
            if (FindElement<Border>(controlName) is Border rail)
            {
                _navRails[key] = rail;
            }
        }

        private T? FindElement<T>(string name) where T : class
        {
            return (Content as FrameworkElement)?.FindName(name) as T;
        }
    }
}
