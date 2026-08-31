using MBW.App.Platform;
using MBW.App.ViewModels;
using MBW.Core.Interfaces;
using MBW.Core.Services;
using MBW.Infrastructure.Attachments;
using MBW.Infrastructure.Email;
using MBW.Infrastructure.Excel;
using MBW.Infrastructure.Services;
using MBW.Infrastructure.Storage;
using Microsoft.UI.Xaml;
using System;

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

        public static IAttachmentService AttachmentService { get; private set; } = null!;

        public static ISmtpSettingsService SmtpSettingsService { get; private set; } = null!;

        public static IRecentProjectsService RecentProjectsService { get; private set; } = null!;

        public static WorkspaceCoordinator WorkspaceCoordinator { get; private set; } = null!;

        public static SmtpSettingsCoordinator SmtpSettingsCoordinator { get; private set; } = null!;

        public static WelcomeViewModel WelcomeViewModel { get; private set; } = null!;

        private static DatabaseViewModel? _databaseViewModel;
        private static AttachmentsViewModel? _attachmentsViewModel;

        private static Window? _mainWindow;

        public static Window GetMainWindow() =>
            _mainWindow ?? throw new InvalidOperationException("AppServices is not initialized.");

        public static void Initialize(Window mainWindow)
        {
            if (_isInitialized)
            {
                return;
            }

            _mainWindow = mainWindow;

            WorkspaceService = new WorkspaceService(new StorageService());
            ExcelImporter = new ExcelImporter();
            AttachmentService = new AttachmentService();
            SmtpSettingsService = new SmtpSettingsService();
            RecentProjectsService = new RecentProjectsService();

            var workspaceUiGateway = new WinUiWorkspaceUiGateway(mainWindow);
            WorkspaceCoordinator = new WorkspaceCoordinator(WorkspaceService, workspaceUiGateway);

            var smtpUiGateway = new WinUiSmtpSettingsGateway(mainWindow, SmtpSettingsService);
            SmtpSettingsCoordinator = new SmtpSettingsCoordinator(SmtpSettingsService, smtpUiGateway);

            WelcomeViewModel = new WelcomeViewModel(WorkspaceCoordinator, RecentProjectsService);

            _isInitialized = true;
        }

        public static DatabaseViewModel GetDatabaseViewModel()
        {
            return _databaseViewModel ??= new DatabaseViewModel(ExcelImporter, WorkspaceCoordinator);
        }

        public static DatabaseViewModel CreateDatabaseViewModel() => GetDatabaseViewModel();

        public static AttachmentsViewModel GetAttachmentsViewModel()
        {
            return _attachmentsViewModel ??= new AttachmentsViewModel(
                WorkspaceCoordinator,
                AttachmentService);
        }

        public static AttachmentsViewModel CreateAttachmentsViewModel() => GetAttachmentsViewModel();

        public static EmailEditorViewModel CreateEmailEditorViewModel()
        {
            return new EmailEditorViewModel(WorkspaceService, ExcelImporter, WorkspaceCoordinator);
        }

        public static ShellViewModel CreateShellViewModel()
        {
            return new ShellViewModel(WorkspaceCoordinator, SmtpSettingsCoordinator, RecentProjectsService, ExcelImporter);
        }

        public static WelcomeViewModel CreateWelcomeViewModel()
        {
            return WelcomeViewModel;
        }
    }
}
