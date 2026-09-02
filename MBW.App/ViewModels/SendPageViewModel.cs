using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.App.Composition;
using MBW.App.Platform;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class SendPageViewModel : ObservableObject
    {
        private readonly IExcelImporter _excelImporter;
        private readonly WorkspaceCoordinator _workspaceCoordinator;
        private readonly IAttachmentService _attachmentService;
        private readonly SmtpSettingsCoordinator _smtpCoordinator;
        private readonly IEmailSender _emailSender;
        private readonly WinUiSendGateway _sendGateway;

        private List<RecipientRow> _recipients = new();
        private Dictionary<long, string?> _individualMatches = new();
        private IReadOnlyList<string> _sharedFiles = Array.Empty<string>();
        private int _individualFolderFileCount;
        private EmailTemplate _template = new();

        public SendPageViewModel(
            IExcelImporter excelImporter,
            WorkspaceCoordinator workspaceCoordinator,
            IAttachmentService attachmentService,
            SmtpSettingsCoordinator smtpCoordinator,
            IEmailSender emailSender,
            WinUiSendGateway sendGateway)
        {
            _excelImporter = excelImporter;
            _workspaceCoordinator = workspaceCoordinator;
            _attachmentService = attachmentService;
            _smtpCoordinator = smtpCoordinator;
            _emailSender = emailSender;
            _sendGateway = sendGateway;
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
        public partial double RangeFrom { get; set; } = 1;

        [ObservableProperty]
        public partial double RangeTo { get; set; } = 1;

        [ObservableProperty]
        public partial double DelaySeconds { get; set; } = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SendCustomRange), nameof(SendRangeFieldsVisibility), nameof(RangeSummary))]
        public partial bool SendAllRecipients { get; set; } = true;

        [ObservableProperty]
        public partial int CurrentRowIndex { get; set; }

        private bool _suppressSave;
        private bool _isLoading;
        private bool _suppressWorkspaceReload;

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

        [ObservableProperty]
        public partial bool IsSending { get; set; }

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

        public bool SendCustomRange
        {
            get => !SendAllRecipients;
            set
            {
                if (value == SendCustomRange)
                {
                    return;
                }

                SendAllRecipients = !value;
            }
        }

        public Visibility SendRangeFieldsVisibility =>
            SendAllRecipients ? Visibility.Collapsed : Visibility.Visible;

        public int RecipientCount => _recipients.Count;

        public double MaxRecipientCount => Math.Max(RecipientCount, 1);

        public string RangeSummary =>
            RecipientCount == 0
                ? string.Empty
                : SendAllRecipients
                    ? $"All {RecipientCount:N0} recipient(s)"
                    : $"of {RecipientCount:N0} recipient(s)";

        public bool CanGoPrevious => !IsBusy && !IsSending && CurrentRowIndex > 0;

        public bool CanGoNext => !IsBusy && !IsSending && CurrentRowIndex < RecipientCount - 1;

        public bool CanSendNow => !IsBusy && !IsSending && GateMessage is null && RecipientCount > 0;

        public string RenamePatternPlaceholder => "Enter file name pattern";

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
                ApplySettingsFromConfig(_workspaceCoordinator.GetSendConfiguration());
                _template = GetCurrentTemplate();
                Subject = _template.Subject;
                OnPropertyChanged(nameof(MaxRecipientCount));
                OnPropertyChanged(nameof(RangeSummary));
                RefreshPreview();
                return;
            }

            await LoadAsync();
        }

        public async Task PersistSettingsAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace || _suppressSave)
            {
                return;
            }

            try
            {
                _suppressWorkspaceReload = true;
                _suppressSave = true;

                var current = _workspaceCoordinator.GetSendConfiguration();
                var (rangeFrom, rangeTo) = GetPersistedSendRange();
                _workspaceCoordinator.UpdateSendConfiguration(new SendConfiguration
                {
                    SmtpAccountId = current.SmtpAccountId,
                    Concurrency = current.Concurrency,
                    FromName = current.FromName,
                    FromEmail = current.FromEmail,
                    TestMode = false,
                    EmailColumn = SelectedEmailColumn ?? string.Empty,
                    IncludeSharedAttachments = IncludeSharedAttachments,
                    IncludeIndividualAttachments = IncludeIndividualAttachments,
                    AttachmentRenamePattern = AttachmentRenamePattern ?? string.Empty,
                    SendAllRecipients = SendAllRecipients,
                    SendRangeFrom = rangeFrom,
                    SendRangeTo = rangeTo,
                    DelayMilliseconds = (int)Math.Round(Math.Max(0, DelaySeconds) * 1000)
                });

                SyncTemplateSubjectToWorkspace();

                await _workspaceCoordinator.SaveCurrentAsync();
            }
            finally
            {
                _suppressSave = false;
                _suppressWorkspaceReload = false;
            }
        }

        private void ApplySettingsFromConfig(SendConfiguration sendConfig)
        {
            IncludeSharedAttachments = sendConfig.IncludeSharedAttachments;
            IncludeIndividualAttachments = sendConfig.IncludeIndividualAttachments;
            AttachmentRenamePattern = sendConfig.AttachmentRenamePattern ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(sendConfig.EmailColumn)
                && EmailColumns.Any(column => string.Equals(column, sendConfig.EmailColumn, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedEmailColumn = EmailColumns.First(column =>
                    string.Equals(column, sendConfig.EmailColumn, StringComparison.OrdinalIgnoreCase));
            }

            ApplySendRangeFromConfig(sendConfig);
        }

        private void ScheduleSave()
        {
            if (_suppressSave || _isLoading || !_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            AppServices.Shell?.NotifyWorkspaceUnsaved();
            _ = PersistSettingsAsync();
        }

        private (int From, int To) GetPersistedSendRange()
        {
            if (SendAllRecipients || RecipientCount == 0)
            {
                var max = Math.Max(RecipientCount, 1);
                return (1, max);
            }

            ClampSendRange();
            return ((int)Math.Round(RangeFrom), (int)Math.Round(RangeTo));
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
            ScheduleSave();
        }

        partial void OnIncludeSharedAttachmentsChanged(bool value)
        {
            RefreshPreview();
            ScheduleSave();
        }

        partial void OnIncludeIndividualAttachmentsChanged(bool value)
        {
            RefreshPreview();
            ScheduleSave();
        }

        partial void OnAttachmentRenamePatternChanged(string value)
        {
            RefreshPreview();
            ScheduleSave();
        }

        partial void OnSubjectChanged(string value)
        {
            SyncTemplateSubjectToWorkspace();
            ScheduleSave();
        }

        partial void OnRangeFromChanged(double value)
        {
            ClampSendRange();
            ScheduleSave();
        }

        partial void OnRangeToChanged(double value)
        {
            ClampSendRange();
            ScheduleSave();
        }

        partial void OnDelaySecondsChanged(double value)
        {
            if (value < 0)
            {
                DelaySeconds = 0;
                return;
            }

            ScheduleSave();
        }

        partial void OnSendAllRecipientsChanged(bool value)
        {
            if (value && RecipientCount > 0)
            {
                RangeFrom = 1;
                RangeTo = RecipientCount;
            }

            OnPropertyChanged(nameof(RangeSummary));
            ScheduleSave();
        }

        partial void OnCurrentRowIndexChanged(int value)
        {
            RefreshPreview();
            NotifyNavigationState();
        }

        partial void OnIsBusyChanged(bool value)
        {
            NotifyCommandState();
        }

        partial void OnIsSendingChanged(bool value)
        {
            NotifyCommandState();
        }

        private void NotifyCommandState()
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

            var (from, to, count) = GetValidatedSendRange();
            var delaySeconds = (int)Math.Round(Math.Max(0, DelaySeconds));
            if (!await _sendGateway.ConfirmSendAsync(count, from, to, delaySeconds))
            {
                return;
            }

            try
            {
                IsSending = true;
                var summary = await _sendGateway.RunProgressAsync(ExecuteSendAsync);
                StatusMessage = summary ?? "Send finished.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Send failed: {ex.Message}";
            }
            finally
            {
                IsSending = false;
            }
        }

        private async Task ExecuteSendAsync(
            SendProgressViewModel progress,
            CancellationToken cancellationToken,
            IReadOnlyList<int>? rowNumbers = null)
        {
            var delaySeconds = (int)Math.Round(Math.Max(0, DelaySeconds));
            IReadOnlyList<int> rowsToSend;

            if (rowNumbers is not null)
            {
                if (rowNumbers.Count == 0)
                {
                    return;
                }

                rowsToSend = rowNumbers;
            }
            else
            {
                var (from, to, _) = GetValidatedSendRange();
                progress.Reset();

                var rows = new List<(int RowNumber, string Email)>();
                for (var rowNumber = from; rowNumber <= to; rowNumber++)
                {
                    var recipient = _recipients[rowNumber - 1];
                    var email = GetRecipientEmail(recipient, SelectedEmailColumn) ?? "(no email)";
                    rows.Add((rowNumber, email));
                }

                await progress.InitializeEntriesAsync(rows);
                rowsToSend = rows.ConvertAll(row => row.RowNumber);
            }

            var total = rowsToSend.Count;
            var sentInBatch = 0;
            await progress.ReportAsync(sentInBatch, total);

            var sendConfig = _workspaceCoordinator.GetSendConfiguration();
            var template = new EmailTemplate(Subject, GetCurrentTemplate().HtmlBody);

            try
            {
                for (var index = 0; index < rowsToSend.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var rowNumber = rowsToSend[index];
                    var recipient = _recipients[rowNumber - 1];
                    var email = GetRecipientEmail(recipient, SelectedEmailColumn);
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        await progress.SetStatusAsync(rowNumber, SendProgressStatus.Skipped, "No email address");
                        continue;
                    }

                    await progress.SetStatusAsync(rowNumber, SendProgressStatus.Sending);

                    var attachments = BuildAttachmentsForRecipient(recipient);
                    var result = await _emailSender.SendAsync(recipient, template, sendConfig, attachments, cancellationToken);

                    if (result.Success)
                    {
                        await progress.SetStatusAsync(rowNumber, SendProgressStatus.Succeeded);
                        sentInBatch++;
                    }
                    else
                    {
                        await progress.SetStatusAsync(rowNumber, SendProgressStatus.Failed, result.ErrorMessage);
                    }

                    await progress.ReportAsync(sentInBatch, total);

                    if (index < rowsToSend.Count - 1 && delaySeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await progress.MarkIncompleteAsCancelledAsync();
                throw;
            }
        }

        private void OnWorkspaceChanged()
        {
            if (_suppressWorkspaceReload)
            {
                return;
            }

            NotifyGateState();
            _ = EnsureLoadedAsync(force: true);
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
                _isLoading = true;
                IsBusy = true;
                StatusMessage = "Loading send preview...";
                _template = GetCurrentTemplate();
                Subject = _template.Subject;

                var sendConfig = _workspaceCoordinator.GetSendConfiguration();

                var headers = await _excelImporter.GetHeadersAsync(
                    dataPath,
                    _workspaceCoordinator.GetDataSheetName(),
                    _workspaceCoordinator.GetDataHeaderRow());

                EmailColumns.Clear();
                foreach (var header in headers)
                {
                    EmailColumns.Add(header);
                }

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
                ApplySettingsFromConfig(sendConfig);
                SelectedEmailColumn = ResolveInitialEmailColumn(sendConfig.EmailColumn, headers);
                OnPropertyChanged(nameof(MaxRecipientCount));
                OnPropertyChanged(nameof(RangeSummary));
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
                _isLoading = false;
                IsBusy = false;
                NotifyCommandState();
            }
        }

        private void ApplySendRangeFromConfig(SendConfiguration config)
        {
            SendAllRecipients = config.SendAllRecipients;
            var max = Math.Max(RecipientCount, 1);
            RangeFrom = config.SendRangeFrom > 0 ? Math.Min(config.SendRangeFrom, max) : 1;
            RangeTo = config.SendRangeTo > 0 ? Math.Min(config.SendRangeTo, max) : max;
            DelaySeconds = Math.Max(0, config.DelayMilliseconds / 1000.0);

            if (SendAllRecipients && RecipientCount > 0)
            {
                RangeFrom = 1;
                RangeTo = RecipientCount;
            }

            ClampSendRange();
            OnPropertyChanged(nameof(SendCustomRange));
            OnPropertyChanged(nameof(SendRangeFieldsVisibility));
            OnPropertyChanged(nameof(RangeSummary));
        }

        private void ClampSendRange()
        {
            if (RecipientCount == 0)
            {
                RangeFrom = 1;
                RangeTo = 1;
                return;
            }

            var max = RecipientCount;
            if (RangeFrom < 1)
            {
                RangeFrom = 1;
            }

            if (RangeFrom > max)
            {
                RangeFrom = max;
            }

            if (RangeTo < RangeFrom)
            {
                RangeTo = RangeFrom;
            }

            if (RangeTo > max)
            {
                RangeTo = max;
            }
        }

        private (int From, int To, int Count) GetValidatedSendRange()
        {
            if (SendAllRecipients && RecipientCount > 0)
            {
                return (1, RecipientCount, RecipientCount);
            }

            ClampSendRange();
            var from = (int)Math.Round(RangeFrom);
            var to = (int)Math.Round(RangeTo);
            return (from, to, Math.Max(0, to - from + 1));
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

            var (from, to, _) = GetValidatedSendRange();
            if (from < 1 || to > RecipientCount || from > to)
            {
                error = "Enter a valid send range.";
                return false;
            }

            if (DelaySeconds < 0)
            {
                error = "Delay cannot be negative.";
                return false;
            }

            if (!_smtpCoordinator.Current.IsConfigured)
            {
                error = "Configure SMTP settings first.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_smtpCoordinator.Current.GetSenderEmail()))
            {
                error = "Set a from email in SMTP settings.";
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

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return originalFileName;
            }

            if (!Path.HasExtension(sanitized) && Path.HasExtension(originalFileName))
            {
                sanitized += Path.GetExtension(originalFileName);
            }

            return sanitized;
        }

        private IReadOnlyList<SendEmailAttachment> BuildAttachmentsForRecipient(RecipientRow recipient)
        {
            var attachments = new List<SendEmailAttachment>();

            if (IncludeSharedAttachments && _sharedFiles.Count > 0)
            {
                var sharedDir = _workspaceCoordinator.GetSharedAttachmentsDirectory();
                foreach (var fileName in _sharedFiles)
                {
                    var filePath = Path.Combine(sharedDir, fileName);
                    if (File.Exists(filePath))
                    {
                        attachments.Add(new SendEmailAttachment(filePath, fileName));
                    }
                }
            }

            if (IncludeIndividualAttachments
                && _individualMatches.TryGetValue(recipient.RowNumber, out var individualFile)
                && !string.IsNullOrWhiteSpace(individualFile))
            {
                var link = _workspaceCoordinator.GetAttachmentConfiguration().Link;
                if (!string.IsNullOrWhiteSpace(link.IndividualFolderName))
                {
                    var folderPath = Path.Combine(
                        _workspaceCoordinator.GetIndividualAttachmentsDirectory(),
                        link.IndividualFolderName);
                    var filePath = Path.Combine(folderPath, individualFile);
                    if (File.Exists(filePath))
                    {
                        var displayName = ResolveAttachmentDisplayName(individualFile, recipient);
                        attachments.Add(new SendEmailAttachment(filePath, displayName));
                    }
                }
            }

            return attachments;
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
