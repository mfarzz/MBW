using MBW.App.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MBW.App.Controls
{
    public sealed partial class SmtpSettingsForm : UserControl
    {
        private const double MinScrollHeight = 400;
        private const double MaxScrollHeight = 560;
        private const double HeightRatio = 0.72;

        public SmtpSettingsForm()
        {
            InitializeComponent();
            Loaded += SmtpSettingsForm_Loaded;
            DataContextChanged += SmtpSettingsForm_DataContextChanged;
        }

        private void ServerSectionSeparator_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (DataContext is SmtpSettingsViewModel viewModel)
            {
                viewModel.IsServerSectionExpanded = !viewModel.IsServerSectionExpanded;
            }
        }

        private void SmtpSettingsForm_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateScrollHeight();
        }

        private void ServerSectionSeparator_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }

        private void ServerSectionSeparator_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        public void UpdateScrollHeight()
        {
            var hostHeight = XamlRoot?.Size.Height ?? 0;
            if (hostHeight <= 0)
            {
                FormScroller.MaxHeight = MaxScrollHeight;
                return;
            }

            var target = hostHeight * HeightRatio;
            FormScroller.MaxHeight = System.Math.Clamp(target, MinScrollHeight, MaxScrollHeight);
        }

        private void SmtpSettingsForm_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            SyncPasswordFromViewModel();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmtpSettingsViewModel viewModel)
            {
                viewModel.Password = PasswordBox.Password;
            }
        }

        private void SyncPasswordFromViewModel()
        {
            if (DataContext is SmtpSettingsViewModel viewModel)
            {
                PasswordBox.Password = viewModel.Password;
            }
        }
    }
}
