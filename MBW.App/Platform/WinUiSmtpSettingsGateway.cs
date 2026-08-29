using MBW.App.Controls;

using MBW.App.ViewModels;

using MBW.Core.Interfaces;

using Microsoft.UI.Dispatching;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using System;

using System.Threading;

using System.Threading.Tasks;



namespace MBW.App.Platform

{

    public sealed class WinUiSmtpSettingsGateway : ISmtpSettingsUiGateway

    {

        private readonly Window _window;

        private readonly ISmtpSettingsService _settingsService;



        public WinUiSmtpSettingsGateway(Window window, ISmtpSettingsService settingsService)

        {

            _window = window;

            _settingsService = settingsService;

        }



        public Task<bool> ShowEditorAsync(CancellationToken cancellationToken = default)

        {

            return RunOnUiThreadAsync(async () =>

            {

                var viewModel = new SmtpSettingsViewModel(_settingsService);

                await viewModel.LoadAsync();



                var form = new SmtpSettingsForm { DataContext = viewModel };

                var saved = false;



                var dialog = new ContentDialog

                {

                    Title = "SMTP Settings",

                    Content = form,

                    PrimaryButtonText = "Save",

                    CloseButtonText = "Cancel",

                    DefaultButton = ContentDialogButton.Primary,

                    XamlRoot = _window.Content.XamlRoot

                };



                dialog.PrimaryButtonClick += async (_, args) =>

                {

                    var deferral = args.GetDeferral();

                    try

                    {

                        if (!await viewModel.SaveAsync())

                        {

                            args.Cancel = true;

                            return;

                        }



                        saved = true;

                    }

                    finally

                    {

                        deferral.Complete();

                    }

                };



                var result = await dialog.ShowAsync();

                return saved && result == ContentDialogResult.Primary;

            });

        }



        private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)

        {

            var queue = _window.DispatcherQueue;

            if (queue.HasThreadAccess)

            {

                return action();

            }



            var tcs = new TaskCompletionSource<T>();

            queue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>

            {

                try

                {

                    tcs.SetResult(await action());

                }

                catch (Exception ex)

                {

                    tcs.SetException(ex);

                }

            });

            return tcs.Task;

        }

    }

}


