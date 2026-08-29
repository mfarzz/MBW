using MBW.App.Platform;

using MBW.App.ViewModels;

using MBW.Core.Interfaces;

using MBW.Core.Services;

using MBW.Infrastructure.Email;

using MBW.Infrastructure.Excel;

using MBW.Infrastructure.Services;

using MBW.Infrastructure.Storage;

using Microsoft.UI.Xaml;



namespace MBW.App.Composition

{

    /// <summary>

    /// Composition root: wires Infrastructure implementations to Core contracts.

    /// </summary>

    public static class AppServices

    {

        private static bool _isInitialized;



        public static IWorkspaceService WorkspaceService { get; private set; } = null!;



        public static IExcelImporter ExcelImporter { get; private set; } = null!;



        public static ISmtpSettingsService SmtpSettingsService { get; private set; } = null!;



        public static WorkspaceCoordinator WorkspaceCoordinator { get; private set; } = null!;



        public static SmtpSettingsCoordinator SmtpSettingsCoordinator { get; private set; } = null!;



        public static void Initialize(Window mainWindow)

        {

            if (_isInitialized)

            {

                return;

            }



            WorkspaceService = new WorkspaceService(new StorageService());

            ExcelImporter = new ExcelImporter();

            SmtpSettingsService = new SmtpSettingsService();



            var workspaceUiGateway = new WinUiWorkspaceUiGateway(mainWindow);

            WorkspaceCoordinator = new WorkspaceCoordinator(WorkspaceService, workspaceUiGateway);



            var smtpUiGateway = new WinUiSmtpSettingsGateway(mainWindow, SmtpSettingsService);

            SmtpSettingsCoordinator = new SmtpSettingsCoordinator(SmtpSettingsService, smtpUiGateway);



            _isInitialized = true;

        }



        public static EmailEditorViewModel CreateEmailEditorViewModel()

        {

            return new EmailEditorViewModel(WorkspaceService, ExcelImporter, WorkspaceCoordinator);

        }



        public static ShellViewModel CreateShellViewModel()

        {

            return new ShellViewModel(WorkspaceCoordinator, SmtpSettingsCoordinator);

        }

    }

}


