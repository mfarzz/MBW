using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;

namespace MBW.App.Views
{
    public sealed partial class ConfigurationPage : Page
    {
        private readonly ConfigurationViewModel _viewModel;

        public ConfigurationPage()
        {
            InitializeComponent();

            _viewModel = AppServices.GetConfigurationViewModel();
            DataContext = _viewModel;
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = _viewModel.EnsureLoadedAsync(force: true);
        }

        public Task ReloadAsync() => _viewModel.EnsureLoadedAsync(force: true);
    }
}
