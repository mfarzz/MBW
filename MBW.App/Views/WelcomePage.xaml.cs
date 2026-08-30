using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace MBW.App.Views
{
    public sealed partial class WelcomePage : Page
    {
        private WelcomeViewModel? ViewModel => DataContext as WelcomeViewModel;

        public WelcomePage()
        {
            InitializeComponent();
            DataContext = AppServices.CreateWelcomeViewModel();
            Loaded += WelcomePage_Loaded;
        }

        private async void WelcomePage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel is not null)
            {
                await ViewModel.LoadAsync();
            }
        }

        private async void RecentProjectButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel is null || sender is not Button { Tag: RecentProjectItemViewModel item })
            {
                return;
            }

            await ViewModel.OpenRecentCommand.ExecuteAsync(item);
        }
    }
}
