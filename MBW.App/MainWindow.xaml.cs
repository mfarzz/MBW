using MBW.App.Composition;
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
using System.Threading.Tasks;

namespace MBW.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Button> _mainNavButtons = new();
        private readonly Dictionary<string, Button> _configNavButtons = new();
        private readonly Dictionary<string, Border> _navRails = new();
        private readonly Dictionary<string, IconElement> _navIcons = new();
        private readonly ShellViewModel _shellViewModel;
        private bool _isConfigurationOpen = true;
        private string _currentTag = "Email";

        public ShellViewModel ShellViewModel => _shellViewModel;

        public MainWindow()
        {
            InitializeComponent();

            AppServices.Initialize(this);
            _shellViewModel = AppServices.CreateShellViewModel();
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            _shellViewModel.WorkspaceChanged += (_, _) => _ = OnWorkspaceChangedAsync();
            _shellViewModel.NavigationRequested += (_, tag) => NavigateFromWorkspaceMenu(tag);
            SyncShellLabels();

            ConfigureTitleBar();
            RegisterNavElements();

            NavigateToTag("Email");
            RefreshShellState();
            _ = _shellViewModel.InitializeAsync();
        }

        private void RegisterNavElements()
        {
            AddButtonIfExists(_mainNavButtons, "Email", "EmailNavButton");
            AddButtonIfExists(_mainNavButtons, "Database", "DatabaseNavButton");
            AddButtonIfExists(_mainNavButtons, "Attachments", "AttachmentsNavButton");

            AddButtonIfExists(_configNavButtons, "Matching", "MatchingNavButton");
            AddButtonIfExists(_configNavButtons, "Rename", "RenameNavButton");
            AddButtonIfExists(_configNavButtons, "Sending", "SendingNavButton");

            AddRailIfExists("Email", "EmailNavRail");
            AddRailIfExists("Database", "DatabaseNavRail");
            AddRailIfExists("Attachments", "AttachmentsNavRail");
            AddRailIfExists("Configuration", "ConfigurationNavRail");

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
                NavigateToTag(tag);
            }
        }

        private void ConfigNavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                NavigateToTag(tag);
            }
        }

        private void ConfigurationToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isConfigurationOpen = !_isConfigurationOpen;
            RefreshShellState();
        }

        private void TopMenuButton_Click(object sender, RoutedEventArgs e)
        {
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
            SyncEmailEditorToCoordinator();
            await _shellViewModel.SaveWorkspaceAsync();
        }

        private void CloseFileMenuFlyout()
        {
            if (FindElement<Button>("FileMenuButton")?.Flyout is FlyoutBase flyout && flyout.IsOpen)
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
                or nameof(ShellViewModel.SmtpIsConnected))
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
        }

        private async Task OnWorkspaceChangedAsync()
        {
            NavigateToTag("Email");
            await ReloadEmailEditorAsync();
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

        private void SyncEmailEditorToCoordinator()
        {
            if (RootFrame.Content is not FrameworkElement { DataContext: EmailEditorViewModel viewModel })
            {
                return;
            }

            _shellViewModel.ApplyEmailTemplate(viewModel.GetCurrentTemplate());
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

        private async void WorkspaceSmtp_Click(object sender, RoutedEventArgs e)
        {
            await RunSmtpCommandAsync();
        }

        private void NavigateFromWorkspaceMenu(string tag)
        {
            if (tag == "Configuration")
            {
                _isConfigurationOpen = true;
                NavigateToTag("Matching");
                return;
            }

            NavigateToTag(tag);
        }

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

        private void NavigateToTag(string tag)
        {
            var pageType = tag switch
            {
                "Email" => typeof(EmailEditorPage),
                "Database" => typeof(DatabasePage),
                "Attachments" => typeof(AttachmentsPage),
                "Matching" => typeof(ConfigurationPage),
                "Rename" => typeof(ConfigurationPage),
                "Sending" => typeof(ConfigurationPage),
                _ => typeof(EmailEditorPage)
            };

            if (RootFrame.CurrentSourcePageType != pageType)
            {
                RootFrame.Navigate(pageType, tag);
            }

            _currentTag = tag;
            RefreshShellState();
        }

        private void RefreshShellState()
        {
            if (FindElement<FrameworkElement>("ConfigurationItemsPanel") is FrameworkElement panel)
            {
                panel.Visibility = _isConfigurationOpen ? Visibility.Visible : Visibility.Collapsed;
            }

            if (FindElement<FontIcon>("ConfigurationChevron") is FontIcon chevron)
            {
                chevron.Glyph = _isConfigurationOpen ? "\uE70D" : "\uE76C";
            }

            foreach (var pair in _mainNavButtons)
            {
                ApplyMainNavVisual(pair.Key, pair.Value, pair.Key == _currentTag);
            }

            foreach (var pair in _configNavButtons)
            {
                ApplyConfigVisual(pair.Value, pair.Key == _currentTag);
            }

            var configActive = _currentTag is "Matching" or "Rename" or "Sending";
            if (FindElement<Button>("ConfigurationToggleButton") is Button configToggle)
            {
                ApplyMainNavVisual("Configuration", configToggle, configActive);
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

        private static void ApplyConfigVisual(Button button, bool isActive)
        {
            button.Background = isActive
                ? GetThemeBrush("AccentFillColorSecondaryBrush")
                : TransparentBrush;

            button.Foreground = isActive
                ? GetThemeBrush("TextFillColorPrimary")
                : GetThemeBrush("TextFillColorSecondary");
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
