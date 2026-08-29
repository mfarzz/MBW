using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MBW.App.Views;
using System.Collections.Generic;

namespace MBW.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Button> _mainNavButtons = new();
        private readonly Dictionary<string, Button> _configNavButtons = new();
        private readonly Dictionary<string, Border> _navRails = new();
        private readonly Dictionary<string, IconElement> _navIcons = new();
        private bool _isConfigurationOpen = true;
        private string _currentTag = "Email";

        public MainWindow()
        {
            InitializeComponent();

            ConfigureTitleBar();
            RegisterNavElements();

            NavigateToTag("Email");
            RefreshShellState();
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

        private void SmtpButton_Click(object sender, RoutedEventArgs e)
        {
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
