#pragma checksum "MainWindow.xaml" "{8829d00f-11b8-4213-878b-770e8597ac16}" "0000000000000000000000000000000000000000000000000000000000000000"
//------------------------------------------------------------------------------
// Fallback for IDE IntelliSense when XAML markup compile output is unavailable.
// Real build uses auto-generated files under obj/.
//------------------------------------------------------------------------------

namespace MBW.App
{
    partial class MainWindow
    {
#pragma warning disable 0169, 0649
        private Microsoft.UI.Xaml.Controls.Frame RootFrame = null!;
#pragma warning restore 0649, 0169

        private bool _contentLoaded;

        public void InitializeComponent()
        {
            if (_contentLoaded)
            {
                return;
            }

            _contentLoaded = true;

            var resourceLocator = new System.Uri("ms-appx:///MainWindow.xaml");
            Microsoft.UI.Xaml.Application.LoadComponent(
                this,
                resourceLocator,
                Microsoft.UI.Xaml.Controls.Primitives.ComponentResourceLocation.Application);
        }
    }
}
