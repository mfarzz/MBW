using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class SendPageViewModel : ObservableObject
    {
        private readonly IExcelImporter _excelImporter;
        private readonly WorkspaceCoordinator _workspaceCoordinator;
        private readonly IAttachmentService _attachmentService;
        private readonly SmtpSettingsCoordinator _smtpCoordinator;

        private List<RecipientRow> _recipients = new();
        private Dictionary<long, string?> _individualMatches = new();
        private IReadOnlyList<string> _sharedFiles = Array.Empty<string>();
        private int _individualFolderFileCount;
        private EmailTemplate _template = new();

        public SendPageViewModel(
            IExcelImporter excelImporter,
            WorkspaceCoordinator workspaceCoordinator,
            IAttachmentService attachmentService,
            SmtpSettingsCoordinator smtpCoordinator)
        {
            _excelImporter = excelImporter;
            _workspaceCoordinator = workspaceCoordinator;
            _attachmentService = attachmentService;
            _smtpCoordinator = smtpCoordinator;
            _workspaceCoordinator.Changed += (_, _) => OnWorkspaceChanged();
        }

        public ObservableCollection<string> EmailColumns { get; } = new();

        [ObservableProperty]
        public partial string? SelectedEmailColumn { get; set; }

        [ObservableProperty]
        public partial bool IncludeSharedAttachments { get; set; } = true;

        [ObservableProperty]
        public partial bool IncludeIndividualAttachments { get; set; } = true;

        [ObservableProperty]
        public partial string AttachmentRenamePattern { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string RenamePreviewLine { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int CurrentRowIndex { get; set; }

        [ObservableProperty]
        public partial string FromLine { get; set; } = "From: —";

        [ObservableProperty]
        public partial string ToLine { get; set; } = "To: —";

        [ObservableProperty]
        public partial string Subject { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SharedAttachmentSummary { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string IndividualAttachmentSummary { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AttachmentLine { get; set; } = "Attachments: —";

        [ObservableProperty]
        public partial string RowCaption { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public bool HasWorkspace => _workspaceCoordinator.HasWorkspace;

        public bool HasDatabase => !string.IsNullOrWhiteSpace(_workspaceCoordinator.GetResolvedDataFilePath());

        public string? GateMessage
        {
            get
            {
                if (!HasWorkspace)
                {
                    return "Create or open a workspace first.";
                }

                if (!HasDatabase)
                {
                    return "Import an Excel database in the Database panel first.";
                }

                return null;
            }
        }

        public Visibility GateVisibility => GateMessage is null ? Visibility.Collapsed : Visibility.Visible;

        public Visibility FormVisibility => GateMessage is null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RenamePreviewVisibility =>
            string.IsNullOrWhiteSpace(RenamePreviewLine) ? Visibility.Collapsed : Visibility.Visible;

        public int RecipientCount => _recipients.Count;

        public bool CanGoPrevious => !IsBusy && CurrentRowIndex > 0;

        public bool CanGoNext => !IsBusy && CurrentRowIndex < RecipientCount - 1;

        public bool CanSendNow => !IsBusy && GateMessage is null && RecipientCount > 0;

        public string RenamePatternPlaceholder
        {
            get
            {
                var keyColumn = _workspaceCoordinator.GetAttachmentConfiguration().Link.KeyColumn;
                return string.IsNullOrWhiteSpace(keyColumn)
                    ? "Surat_{Column}.pdf"
                    : $"Surat_{{{keyColumn}}}.pdf";
            }
        }

        public event EventHandler<string>? HtmlPreviewChanged;

        public async Task EnsureLoadedAsync(bool force = false)
        {
            NotifyGateState();

            if (GateMessage is not null)
            {
                StatusMessage = GateMessage;
                return;
            }

            if (!force && RecipientCount > 0 && !IsBusy)
            {
                _template = GetCurrentTemplate();
                Subject = _template.Subject;
                var sendConfig = _workspaceCoordinator.GetSendConfiguration();
                AttachmentRenamePattern = sendConfig.AttachmentRenamePattern ?? string.Empty;
                OnPropertyChanged(nameof(RenamePatternPlaceholder));
                RefreshPreview();
                return;
            }

            await LoadAsync();
        }

        public async Task PersistSettingsAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            var current = _workspaceCoordinator.GetSendConfiguration();
            _workspaceCoordinator.UpdateSendConfiguration(new SendConfiguration
            {
                SmtpAccountId = current.SmtpAccountId,
                DelayMilliseconds = current.DelayMilliseconds,
                Concurrency = current.Concurrency,
                FromName = current.FromName,
                FromEmail = current.FromEmail,
                TestMode = current.TestMode,
                EmailColumn = SelectedEmailColumn ?? string.Empty,
                IncludeSharedAttachments = IncludeSharedAttachments,
                IncludeIndividualAttachments = IncludeIndividualAttachments,
                AttachmentRenamePattern = AttachmentRenamePattern ?? string.Empty
            });

            SyncTemplateSubjectToWorkspace();

            await _workspaceCoordinator.SaveCurrentAsync();
        }

        private void SyncTemplateSubjectToWorkspace()
        {
            if (!_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            var template = GetCurrentTemplate();
            _workspaceCoordinator.UpdateCurrentTemplate(new EmailTemplate(Subject, template.HtmlBody));
            _template = new EmailTemplate(Subject, template.HtmlBody);
        }

        public static string? GetRecipientEmail(RecipientRow recipient, string? column)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                return null;
            }

            var value = recipient.Get(column)?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        partial void OnSelectedEmailColumnChanged(string? value)
        {
            RefreshPreview();
            _ = PersistSettingsAsync();
        }

        partial void OnIncludeSharedAttachmentsChanged(bool value)
        {
            RefreshPreview();
            _ = PersistSettingsAsync();
        }

        partial void OnIncludeIndividualAttachmentsChanged(bool value)
        {
            RefreshPreview();
            _ = PersistSettingsAsync();
        }

        partial void OnAttachmentRenamePatternChanged(string value)
        {
            RefreshPreview();
            _ = PersistSettingsAsync();
        }

        partial void OnSubjectChanged(string value)
        {
            SyncTemplateSubjectToWorkspace();
            _ = PersistSettingsAsync();
        }

        partial void OnCurrentRowIndexChanged(int value)
        {
            RefreshPreview();
            NotifyNavigationState();
        }

        partial void OnIsBusyChanged(bool value)
        {
            NotifyNavigationState();
            OnPropertyChanged(nameof(CanSendNow));
            SendNowCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanGoPrevious))]
        private void PreviousRow()
        {
            if (CurrentRowIndex > 0)
            {
                CurrentRowIndex--;
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoNext))]
        private void NextRow()
        {
            if (CurrentRowIndex < RecipientCount - 1)
            {
                CurrentRowIndex++;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendNow))]
        private async Task SendNowAsync()
        {
            if (!TryValidateForSend(out var error))
            {
                StatusMessage = error;
                return;
            }

            await PersistSettingsAsync();
            StatusMessage = "Sending is not yet implemented (STEP 8).";
        }

        private void OnWorkspaceChanged()
        {
            NotifyGateState();
        }

        private async Task LoadAsync()
        {
            var dataPath = _workspaceCoordinator.GetResolvedDataFilePath();
            if (string.IsNullOrEmpty(dataPath))
            {
                StatusMessage = "No database file found.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Loading send preview...";
                _template = GetCurrentTemplate();
                Subject = _template.Subject;

                var sendConfig = _workspaceCoordinator.GetSendConfiguration();
                IncludeSharedAttachments = sendConfig.IncludeSharedAttachments;
                IncludeIndividualAttachments = sendConfig.IncludeIndividualAttachments;
                AttachmentRenamePattern = sendConfig.AttachmentRenamePattern ?? string.Empty;
                OnPropertyChanged(nameof(RenamePatternPlaceholder));

                var headers = await _excelImporter.GetHeadersAsync(
                    dataPath,
                    _workspaceCoordinator.GetDataSheetName(),
                    _workspaceCoordinator.GetDataHeaderRow());

                EmailColumns.Clear();
                foreach (var header in headers)
                {
                    EmailColumns.Add(header);
                }

                SelectedEmailColumn = ResolveInitialEmailColumn(sendConfig.EmailColumn, headers);

                _recipients = new List<RecipientRow>();
                await foreach (var row in _excelImporter.ReadAllAsync(
                                   dataPath,
                                   _workspaceCoordinator.GetDataSheetName(),
                                   _workspaceCoordinator.GetDataHeaderRow()))
                {
                    _recipients.Add(row);
                }

                if (_recipients.Count == 0)
                {
                    StatusMessage = "No recipient data in the database.";
                    RefreshPreview();
                    return;
                }

                await LoadAttachmentIndexAsync();
                UpdateAttachmentSummaries();
                CurrentRowIndex = 0;
                RefreshPreview();
                StatusMessage = $"{_recipients.Count:N0} recipient(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load send preview: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                NotifyNavigationState();
                OnPropertyChanged(nameof(CanSendNow));
                SendNowCommand.NotifyCanExecuteChanged();
            }
        }

        private EmailTemplate GetCurrentTemplate()
        {
            var template = _workspaceCoordinator.Current?.Template;
            return template is null
                ? new EmailTemplate()
                : new EmailTemplate(template.Subject, template.HtmlBody);
        }

        private bool TryValidateForSend(out string error)
        {
            error = string.Empty;

            if (RecipientCount == 0)
            {
                error = "No recipient data available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedEmailColumn))
            {
                error = "Select an email column.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Subject))
            {
                error = "Enter an email subject.";
                return false;
            }

            if (!EmailColumns.Contains(SelectedEmailColumn))
            {
                error = "The selected email column is no longer available.";
                return false;
            }

            return true;
        }

        private async Task LoadAttachmentIndexAsync()
        {
            _individualMatches = new Dictionary<long, string?>();
            _sharedFiles = Array.Empty<string>();
            _individualFolderFileCount = 0;

            var sharedDir = _workspaceCoordinator.GetSharedAttachmentsDirectory();
            if (Directory.Exists(sharedDir))
            {
                _sharedFiles = await _attachmentService.ListAttachmentsAsync(sharedDir);
            }

            var link = _workspaceCoordinator.GetAttachmentConfiguration().Link;
            if (string.IsNullOrWhiteSpace(link.IndividualFolderName)
                || string.IsNullOrWhiteSpace(link.KeyColumn)
                || _recipients.Count == 0)
            {
                return;
            }

            var folderPath = Path.Combine(
                _workspaceCoordinator.GetIndividualAttachmentsDirectory(),
                link.IndividualFolderName);

            if (!Directory.Exists(folderPath))
            {
                return;
            }

            _individualFolderFileCount = await _attachmentService.CountAttachmentsAsync(folderPath);

            var matches = await _attachmentService.MatchByKeyColumnAsync(
                folderPath,
                _recipients,
                link.KeyColumn);

            foreach (var match in matches)
            {
                if (!long.TryParse(match.RecipientKey, out var rowNumber))
                {
                    continue;
                }

                _individualMatches[rowNumber] = match.Matched ? match.FileName : null;
            }
        }

        private void UpdateAttachmentSummaries()
        {
            SharedAttachmentSummary = _sharedFiles.Count == 0
                ? "No files in the shared folder."
                : $"{_sharedFiles.Count:N0} file(s) in shared folder";

            var link = _workspaceCoordinator.GetAttachmentConfiguration().Link;
            if (string.IsNullOrWhiteSpace(link.IndividualFolderName)
                || string.IsNullOrWhiteSpace(link.KeyColumn))
            {
                IndividualAttachmentSummary =
                    "Individual folder not configured. Set up matching in the Configuration panel.";
                return;
            }

            if (_recipients.Count == 0)
            {
                IndividualAttachmentSummary =
                    $"Folder \"{link.IndividualFolderName}\": {_individualFolderFileCount:N0} file(s).";
                return;
            }

            var matched = _individualMatches.Values.Count(file => !string.IsNullOrWhiteSpace(file));
            var missing = Math.Max(0, _recipients.Count - matched);
            IndividualAttachmentSummary =
                $"Folder \"{link.IndividualFolderName}\": {_individualFolderFileCount:N0} file(s) · {matched:N0} matched · {missing:N0} missing";
        }

        private static string? ResolveInitialEmailColumn(string? savedColumn, IReadOnlyList<string> headers)
        {
            if (!string.IsNullOrWhiteSpace(savedColumn)
                && headers.Any(header => string.Equals(header, savedColumn, StringComparison.OrdinalIgnoreCase)))
            {
                return headers.First(header => string.Equals(header, savedColumn, StringComparison.OrdinalIgnoreCase));
            }

            var preferred = new[] { "Email", "email", "E-mail", "E-Mail" };
            foreach (var candidate in preferred)
            {
                var match = headers.FirstOrDefault(header =>
                    string.Equals(header, candidate, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    return match;
                }
            }

            return headers.FirstOrDefault();
        }

        private void RefreshPreview()
        {
            var latestTemplate = GetCurrentTemplate();
            _template = new EmailTemplate(Subject, latestTemplate.HtmlBody);

            if (_recipients.Count == 0)
            {
                RowCaption = string.Empty;
                FromLine = "From: —";
                ToLine = "To: —";
                AttachmentLine = "Attachments: —";
                RenamePreviewLine = string.Empty;
                OnPropertyChanged(nameof(RenamePreviewVisibility));
                HtmlPreviewChanged?.Invoke(this, string.Empty);
                return;
            }

            var index = Math.Clamp(CurrentRowIndex, 0, _recipients.Count - 1);
            var recipient = _recipients[index];
            var rendered = _template.RenderForRecipient(recipient);
            var senderDisplay = _smtpCoordinator.Current.GetSenderDisplay();
            var senderEmail = _smtpCoordinator.Current.GetSenderEmail();

            RowCaption = $"Row {index + 1} of {_recipients.Count:N0}";
            var fromDisplay = string.IsNullOrWhiteSpace(senderDisplay)
                ? (string.IsNullOrWhiteSpace(senderEmail) ? "—" : senderEmail)
                : senderDisplay;
            var toEmail = GetRecipientEmail(recipient, SelectedEmailColumn) ?? "—";
            FromLine = $"From: {fromDisplay}";
            ToLine = $"To: {toEmail}";
            AttachmentLine = $"Attachments: {BuildAttachmentDisplay(recipient)}";
            UpdateRenamePreview(recipient);
            HtmlPreviewChanged?.Invoke(this, rendered.HtmlBody);
        }

        private void UpdateRenamePreview(RecipientRow recipient)
        {
            if (string.IsNullOrWhiteSpace(AttachmentRenamePattern)
                || !IncludeIndividualAttachments
                || !_individualMatches.TryGetValue(recipient.RowNumber, out var individualFile)
                || string.IsNullOrWhiteSpace(individualFile))
            {
                RenamePreviewLine = string.Empty;
                OnPropertyChanged(nameof(RenamePreviewVisibility));
                return;
            }

            var display = ResolveAttachmentDisplayName(individualFile, recipient);
            RenamePreviewLine = string.Equals(individualFile, display, StringComparison.OrdinalIgnoreCase)
                ? $"As: {display}"
                : $"As: {individualFile} → {display}";
            OnPropertyChanged(nameof(RenamePreviewVisibility));
        }

        private string BuildAttachmentDisplay(RecipientRow recipient)
        {
            var parts = new List<string>();

            if (IncludeSharedAttachments)
            {
                foreach (var file in _sharedFiles)
                {
                    parts.Add($"{file} (shared)");
                }
            }

            if (IncludeIndividualAttachments
                && _individualMatches.TryGetValue(recipient.RowNumber, out var individualFile)
                && !string.IsNullOrWhiteSpace(individualFile))
            {
                parts.Add(ResolveAttachmentDisplayName(individualFile, recipient));
            }

            return parts.Count == 0 ? "—" : string.Join(", ", parts);
        }

        private string ResolveAttachmentDisplayName(string originalFileName, RecipientRow recipient)
        {
            if (string.IsNullOrWhiteSpace(AttachmentRenamePattern))
            {
                return originalFileName;
            }

            var resolved = _attachmentService.ResolvePattern(AttachmentRenamePattern, recipient.Fields);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return originalFileName;
            }

            var sanitized = resolved
                .Replace('\\', '/')
                .Split('/')
                .LastOrDefault()?
                .Trim();

            return string.IsNullOrWhiteSpace(sanitized) ? originalFileName : sanitized;
        }

        private void NotifyNavigationState()
        {
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            PreviousRowCommand.NotifyCanExecuteChanged();
            NextRowCommand.NotifyCanExecuteChanged();
        }

        private void NotifyGateState()
        {
            OnPropertyChanged(nameof(HasWorkspace));
            OnPropertyChanged(nameof(HasDatabase));
            OnPropertyChanged(nameof(GateMessage));
            OnPropertyChanged(nameof(GateVisibility));
            OnPropertyChanged(nameof(FormVisibility));
            OnPropertyChanged(nameof(CanSendNow));
            SendNowCommand.NotifyCanExecuteChanged();
        }
    }
}
