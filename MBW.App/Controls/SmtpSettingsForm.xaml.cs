using MBW.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MBW.App.Controls
{
    public sealed partial class SmtpSettingsForm : UserControl
    {
        public SmtpSettingsForm()
        {
            InitializeComponent();
            Loaded += (_, _) => SyncPasswordFromViewModel();
            DataContextChanged += SmtpSettingsForm_DataContextChanged;
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
