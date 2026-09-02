using MBW.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MBW.App.Controls
{
    public sealed partial class SendProgressForm : UserControl
    {
        private SendProgressViewModel? _viewModel;

        public SendProgressForm()
        {
            InitializeComponent();
            DataContextChanged += SendProgressForm_DataContextChanged;
        }

        private void SendProgressForm_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_viewModel is not null)
            {
                _viewModel.EntryUpdated -= ViewModel_EntryUpdated;
            }

            _viewModel = DataContext as SendProgressViewModel;
            if (_viewModel is not null)
            {
                _viewModel.EntryUpdated += ViewModel_EntryUpdated;
            }
        }

        private void ViewModel_EntryUpdated(object? sender, SendProgressEntry entry)
        {
            LogList.UpdateLayout();
            LogList.ScrollIntoView(entry);
        }
    }
}
