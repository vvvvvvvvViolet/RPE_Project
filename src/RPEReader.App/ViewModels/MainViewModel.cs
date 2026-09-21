using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using RPEReader.App.Mvvm;
using RPEReader.Core.Abstractions;
using RPEReader.Core.Editing;
using RPEReader.Core.Export;
using RPEReader.Core.Models;
using RPEReader.Core.Services;

namespace RPEReader.App.ViewModels;

/// <summary>Drives the main window. Holds no file handles between operations
/// except the read-only hex view source.</summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly RpeFileService _fileService;
    private readonly SearchService _searchService;
    private readonly IAppLogger _logger;

    private HexViewSource? _hexSource;
    private CancellationTokenSource? _openCts;

    private RpeOpenResult? _current;
    private NodeViewModel? _selectedNode;
    private SearchHitViewModel? _selectedHit;
    private string _statusText = "Ready. Open a .rpe file, or drag one onto the window.";
    private string _searchQuery = string.Empty;
    private SearchMode _searchMode = SearchMode.Text;
    private bool _searchCaseSensitive;
    private bool _isBusy;
    private string _rawText = string.Empty;
    private long _hexPageIndex;
    private bool _isEditMode;

    public MainViewModel(RpeFileService? fileService = null, IAppLogger? logger = null)
    {
        _logger = logger ?? Core.Logging.NullLogger.Instance;
        _fileService = fileService ?? new RpeFileService(logger: _logger);
        _searchService = new SearchService(_fileService.Limits);

        OpenFileCommand = new AsyncRelayCommand(OpenFileDialogAsync, () => !IsBusy);
        CloseFileCommand = new RelayCommand(CloseFileInteractive, () => HasDocument);
        SearchCommand = new RelayCommand(RunSearch, () => HasDocument && !string.IsNullOrEmpty(SearchQuery));
        ClearSearchCommand = new RelayCommand(ClearSearch, () => SearchResults.Count > 0);
        ExportJsonCommand = new RelayCommand(() => Export(new JsonExporter()), () => HasDocument);
        ExportCsvCommand = new RelayCommand(() => Export(new CsvExporter()), () => HasDocument);
        ExportTextCommand = new RelayCommand(() => Export(new TextExporter()), () => HasDocument);
        CopySelectionCommand = new RelayCommand(CopySelection, () => SelectedNode is not null);
        CopySummaryCommand = new RelayCommand(CopySummary, () => HasDocument);
        ExpandAllCommand = new RelayCommand(() => SetExpansion(true), () => HasDocument);
        CollapseAllCommand = new RelayCommand(() => SetExpansion(false), () => HasDocument);
        NextHexPageCommand = new RelayCommand(() => ShowHexPage(HexPageIndex + 1), () => HasHexPages);
        PreviousHexPageCommand = new RelayCommand(() => ShowHexPage(HexPageIndex - 1), () => HasHexPages);

        SaveAsRpeCommand = new RelayCommand(SaveAsRpe, () => CanEdit);
        OverwriteRpeCommand = new RelayCommand(OverwriteRpe, () => CanEdit && IsDirty);
        RevertAllCommand = new RelayCommand(RevertAll, () => IsDirty);
        RevertSelectedCommand = new RelayCommand(RevertSelected, () => SelectedPendingEdit is not null);
    }

    /// <summary>Hex-dump rows rendered per page.</summary>
    public const int HexLinesPerPage = 2048;

    public ObservableCollection<NodeViewModel> RootNodes { get; } = new();

    public ObservableCollection<FieldRowViewModel> SelectedFields { get; } = new();

    public ObservableCollection<FieldRowViewModel> SummaryFields { get; } = new();

    public ObservableCollection<SearchHitViewModel> SearchResults { get; } = new();

    public ObservableCollection<HexLineViewModel> HexLines { get; } = new();

    public ObservableCollection<string> Messages { get; } = new();

    public ObservableCollection<PendingEditViewModel> PendingEdits { get; } = new();

    public AsyncRelayCommand OpenFileCommand { get; }

    public RelayCommand CloseFileCommand { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public RelayCommand ExportJsonCommand { get; }

    public RelayCommand ExportCsvCommand { get; }

    public RelayCommand ExportTextCommand { get; }

    public RelayCommand CopySelectionCommand { get; }

    public RelayCommand CopySummaryCommand { get; }

    public RelayCommand ExpandAllCommand { get; }

    public RelayCommand CollapseAllCommand { get; }

    public RelayCommand NextHexPageCommand { get; }

    public RelayCommand PreviousHexPageCommand { get; }

    public RelayCommand SaveAsRpeCommand { get; }

    public RelayCommand OverwriteRpeCommand { get; }

    public RelayCommand RevertAllCommand { get; }

    public RelayCommand RevertSelectedCommand { get; }

    public bool HasDocument => _current?.Document is not null;

    // ------------------------------------------------------------- editing

    private IRpeDocumentEditor? Editor => _current?.Document?.Editor;

    /// <summary>True when the open document's format can also be written.</summary>
    public bool CanEdit => Editor is not null;

    /// <summary>
    /// Whether the property grid accepts changes. Off on every open: a file is
    /// always inspected read-only first, and editing is a deliberate act.
    /// </summary>
    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (!CanEdit)
            {
                value = false;
            }

            if (!SetProperty(ref _isEditMode, value))
            {
                return;
            }

            foreach (var row in SelectedFields)
            {
                row.EditModeEnabled = value;
            }

            foreach (var row in SummaryFields)
            {
                row.EditModeEnabled = value;
            }

            OnPropertyChanged(nameof(EditModeLabel));
            OnPropertyChanged(nameof(IsDetailsReadOnly));
            StatusText = value
                ? "Editing enabled. The file on disk is still untouched — changes are written only when you save."
                : "Editing disabled. The document is open read-only.";
        }
    }

    public bool IsDetailsReadOnly => !IsEditMode;

    public bool IsDirty => Editor?.IsDirty ?? false;

    public int PendingEditCount => Editor?.PendingEdits.Count ?? 0;

    public string EditModeLabel => !CanEdit
        ? "Read-only (format cannot be written by this build)"
        : IsEditMode
            ? $"Editing — {PendingEditCount:N0} pending change(s)"
            : "Read-only";

    public string WindowTitle
    {
        get
        {
            var name = _current?.FileInfo?.FileName;
            var baseTitle = name is null ? "RPE Reader 1.0.0" : $"{name} — RPE Reader 1.0.0";
            return IsDirty ? "*" + baseTitle : baseTitle;
        }
    }

    private PendingEditViewModel? _selectedPendingEdit;

    public PendingEditViewModel? SelectedPendingEdit
    {
        get => _selectedPendingEdit;
        set => SetProperty(ref _selectedPendingEdit, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(NotBusy));
            }
        }
    }

    public bool NotBusy => !_isBusy;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string RawText
    {
        get => _rawText;
        private set => SetProperty(ref _rawText, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public SearchMode SearchMode
    {
        get => _searchMode;
        set => SetProperty(ref _searchMode, value);
    }

    public bool SearchCaseSensitive
    {
        get => _searchCaseSensitive;
        set => SetProperty(ref _searchCaseSensitive, value);
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!SetProperty(ref _selectedNode, value))
            {
                return;
            }

            SelectedFields.Clear();
            if (value is not null)
            {
                foreach (var field in value.Node.Fields)
                {
                    SelectedFields.Add(new FieldRowViewModel(field, Editor, value.Path, OnFieldEdited)
                    {
                        EditModeEnabled = IsEditMode
                    });
                }
            }

            OnPropertyChanged(nameof(SelectedNodePath));
        }
    }

    public string SelectedNodePath => SelectedNode?.Path ?? string.Empty;

    public SearchHitViewModel? SelectedHit
    {
        get => _selectedHit;
        set
        {
            if (!SetProperty(ref _selectedHit, value))
            {
                return;
            }

            if (value?.Hit.Node is { } node)
            {
                SelectNode(node);
            }
            else if (value?.Hit.Offset is { } offset)
            {
                ShowHexPage(offset / (HexLinesPerPage * (long)HexDumpService.BytesPerLine));
            }
        }
    }

    // --- File identity, bound to the info panel ---------------------------

    public string FileName => _current?.FileInfo?.FileName ?? "(no file open)";

    public string FilePath => _current?.FileInfo?.FullPath ?? string.Empty;

    public string FileSize => _current?.FileInfo?.SizeDisplay ?? string.Empty;

    public string FileModified => _current?.FileInfo is { } f
        ? f.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + $"  ({f.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} UTC)"
        : string.Empty;

    public string FileSha256 => _current?.FileInfo?.Sha256 ?? string.Empty;

    public string DetectedFormat => _current?.SelectedParser ?? string.Empty;

    public long HexPageIndex
    {
        get => _hexPageIndex;
        private set
        {
            if (SetProperty(ref _hexPageIndex, value))
            {
                OnPropertyChanged(nameof(HexPageLabel));
            }
        }
    }

    public long HexPageCount => _hexSource is null
        ? 0
        : Math.Max(1, (_hexSource.TotalLines + HexLinesPerPage - 1) / HexLinesPerPage);

    public bool HasHexPages => _hexSource is not null && HexPageCount > 1;

    public string HexPageLabel => _hexSource is null
        ? string.Empty
        : $"Page {HexPageIndex + 1:N0} of {HexPageCount:N0}  (offset 0x{HexPageIndex * HexLinesPerPage * HexDumpService.BytesPerLine:X8})";

    // --- Opening -----------------------------------------------------------

    private async Task OpenFileDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open RPE file",
            Filter = "RPE file (*.rpe)|*.rpe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            await OpenAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    public async Task OpenAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // Opening another file would drop unsaved edits, so ask first.
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        _openCts?.Cancel();
        _openCts?.Dispose();
        _openCts = new CancellationTokenSource();
        var token = _openCts.Token;

        IsBusy = true;
        StatusText = $"Reading {Path.GetFileName(path)}…";
        Messages.Clear();

        try
        {
            var result = await _fileService.OpenAsync(path, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            ApplyResult(result, path);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Open cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error($"Unhandled failure opening '{Path.GetFileName(path)}'.", ex);
            Messages.Add($"[Error] {ex.GetType().Name}: {ex.Message}");
            StatusText = "The file could not be opened.";
            MessageBox.Show(
                $"The file could not be opened.\n\n{ex.Message}",
                "RPE Reader",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResult(RpeOpenResult result, string path)
    {
        CloseFile();
        _current = result;

        foreach (var diagnostic in result.Diagnostics)
        {
            Messages.Add($"[{diagnostic.Severity}] {diagnostic.Message}");
        }

        if (!result.Success || result.Document is null)
        {
            StatusText = $"Could not read {Path.GetFileName(path)}.";
            RaiseFileProperties();

            var detail = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error)?.Message
                         ?? "The file format was not recognised.";

            MessageBox.Show(detail, "RPE Reader", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var document = result.Document;

        RootNodes.Add(new NodeViewModel(document.Root));
        RootNodes[0].IsExpanded = true;
        foreach (var child in RootNodes[0].Children)
        {
            child.IsExpanded = true;
        }

        foreach (var field in document.Summary)
        {
            SummaryFields.Add(new FieldRowViewModel(field, Editor, "Document summary", OnFieldEdited)
            {
                EditModeEnabled = IsEditMode
            });
        }

        RawText = document.RawText + (document.RawTextTruncated
            ? Environment.NewLine + Environment.NewLine + "— truncated at the raw-text display limit —"
            : string.Empty);

        try
        {
            _hexSource = HexViewSource.Open(path);
            ShowHexPage(0);
        }
        catch (Exception ex)
        {
            _logger.Error("Could not open the file for hex viewing.", ex);
            Messages.Add($"[Warning] The hex view is unavailable: {ex.Message}");
        }

        SelectedNode = RootNodes[0];
        RaiseFileProperties();

        var warnings = result.Diagnostics.Count(d => d.Severity != DiagnosticSeverity.Info);
        var editNote = document.IsEditable
            ? " Editing is available from the Edit menu."
            : " This document is read-only.";

        StatusText = document.Incomplete || warnings > 0
            ? $"Opened {_current.FileInfo?.FileName} with {warnings} message(s). Some content may be incomplete — see Messages."
            : $"Opened {_current.FileInfo?.FileName} as {document.FormatDisplayName}.{editNote}";
    }

    private void RaiseFileProperties()
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileSize));
        OnPropertyChanged(nameof(FileModified));
        OnPropertyChanged(nameof(FileSha256));
        OnPropertyChanged(nameof(DetectedFormat));
        OnPropertyChanged(nameof(HexPageCount));
        OnPropertyChanged(nameof(HasHexPages));
        OnPropertyChanged(nameof(HexPageLabel));
        RaiseEditingProperties();
    }

    private void RaiseEditingProperties()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsDetailsReadOnly));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(PendingEditCount));
        OnPropertyChanged(nameof(EditModeLabel));
        OnPropertyChanged(nameof(WindowTitle));
    }

    /// <summary>Close requested by the user, so unsaved changes are worth a prompt.</summary>
    private void CloseFileInteractive()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        CloseFile();
        StatusText = "File closed.";
    }

    /// <summary>
    /// Tears down the open document. Callers that can lose unsaved work go
    /// through <see cref="CloseFileInteractive"/> or check first.
    /// </summary>
    private void CloseFile()
    {
        _isEditMode = false;
        RootNodes.Clear();
        SelectedFields.Clear();
        SummaryFields.Clear();
        SearchResults.Clear();
        PendingEdits.Clear();
        SelectedPendingEdit = null;
        HexLines.Clear();
        RawText = string.Empty;
        SelectedNode = null;
        _selectedHit = null;
        _hexSource?.Dispose();
        _hexSource = null;
        HexPageIndex = 0;
        _current = null;
        RaiseFileProperties();
    }

    // --- Hex paging --------------------------------------------------------

    private void ShowHexPage(long pageIndex)
    {
        if (_hexSource is null)
        {
            return;
        }

        var maxPage = HexPageCount - 1;
        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, maxPage));

        HexLines.Clear();
        foreach (var line in _hexSource.ReadPage(pageIndex * HexLinesPerPage, HexLinesPerPage))
        {
            HexLines.Add(new HexLineViewModel(line));
        }

        HexPageIndex = pageIndex;
    }

    // --- Search ------------------------------------------------------------

    private void RunSearch()
    {
        SearchResults.Clear();

        if (_current?.Document is null || string.IsNullOrEmpty(SearchQuery))
        {
            return;
        }

        try
        {
            IReadOnlyList<SearchHit> hits;
            if (SearchMode == SearchMode.Hex)
            {
                if (_hexSource is null)
                {
                    StatusText = "The hex view is unavailable, so a byte search cannot run.";
                    return;
                }

                if (!SearchService.TryParsePattern(SearchQuery, out _, out _))
                {
                    StatusText = "Enter an even number of hex digits, for example \"50 4B 03 04\". Use ?? for any byte.";
                    return;
                }

                hits = _searchService.SearchHex(_hexSource, SearchQuery);
            }
            else
            {
                hits = _searchService.SearchTree(_current.Document.Root, SearchQuery, SearchMode, SearchCaseSensitive);
            }

            foreach (var hit in hits)
            {
                SearchResults.Add(new SearchHitViewModel(hit));
            }

            var capped = hits.Count >= _fileService.Limits.MaxSearchResults;
            StatusText = hits.Count == 0
                ? $"No match for \"{SearchQuery}\"."
                : $"{hits.Count:N0} match(es){(capped ? " — result limit reached" : string.Empty)}.";
        }
        catch (Exception ex)
        {
            _logger.Error("Search failed.", ex);
            StatusText = $"The search could not be completed: {ex.GetType().Name}.";
        }
    }

    private void ClearSearch()
    {
        SearchResults.Clear();
        SearchQuery = string.Empty;
        StatusText = "Search cleared.";
    }

    private void SelectNode(RpeNode target)
    {
        foreach (var root in RootNodes)
        {
            var found = root.Find(target);
            if (found is null)
            {
                continue;
            }

            found.ExpandToRoot();
            found.IsSelected = true;
            SelectedNode = found;
            return;
        }
    }

    private void SetExpansion(bool expanded)
    {
        foreach (var root in RootNodes)
        {
            Apply(root, expanded);
        }

        static void Apply(NodeViewModel node, bool value)
        {
            node.IsExpanded = value;
            foreach (var child in node.Children)
            {
                Apply(child, value);
            }
        }
    }

    // --- Export and clipboard ---------------------------------------------

    private void Export(IRpeExporter exporter)
    {
        if (_current?.Document is null || _current.FileInfo is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = $"Export as {exporter.Name}",
            Filter = exporter.FileFilter,
            FileName = Path.GetFileNameWithoutExtension(_current.FileInfo.FileName) + exporter.Extension,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            using var stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            exporter.Export(_current.Document, _current.FileInfo, writer, _fileService.Limits);

            StatusText = $"Exported to {Path.GetFileName(dialog.FileName)}.";
            _logger.Info($"Exported '{_current.FileInfo.FileName}' as {exporter.Name}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Export as {exporter.Name} failed.", ex);
            MessageBox.Show(
                $"The export could not be written.\n\n{ex.Message}",
                "RPE Reader",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CopySelection()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine(SelectedNode.Path);
        if (SelectedNode.Value is not null)
        {
            builder.AppendLine($"Value: {SelectedNode.Value}");
        }

        foreach (var field in SelectedNode.Node.Fields)
        {
            builder.Append(field.Name).Append('\t').AppendLine(field.Value);
        }

        SetClipboard(builder.ToString(), "Selected node copied to the clipboard.");
    }

    private void CopySummary()
    {
        if (_current?.Document is null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"File:    {FileName}");
        builder.AppendLine($"Size:    {FileSize}");
        builder.AppendLine($"SHA-256: {FileSha256}");
        builder.AppendLine($"Format:  {DetectedFormat}");
        builder.AppendLine();

        foreach (var field in _current.Document.Summary)
        {
            builder.Append(field.Name).Append('\t').AppendLine(field.Value);
        }

        SetClipboard(builder.ToString(), "Summary copied to the clipboard.");
    }

    // ------------------------------------------------------- editing actions

    private void OnFieldEdited(FieldRowViewModel row, EditValidationResult result)
    {
        if (result.Level == EditValidationLevel.Error)
        {
            Messages.Add($"[Error] {result.Message}");
            StatusText = "The change was rejected. See Messages for the reason.";
        }
        else if (result.Level == EditValidationLevel.Warning)
        {
            Messages.Add($"[Warning] {result.Message}");
            StatusText = "Change applied with a warning. See Messages.";
        }
        else
        {
            StatusText = $"'{row.Name}' changed. Nothing is written until you save.";
        }

        // A root attribute appears in both the tree and the summary as the same
        // field instance, so editing it in one grid has to redraw the other.
        // The row that raised the edit keeps its validation message.
        RefreshAllRows(except: row);
        RefreshPendingEdits();
        RefreshSelectedNodeLabel();
    }

    /// <summary>
    /// A node's label can be derived from a value the user just changed, so the
    /// tree and the path header are re-read after an edit.
    /// </summary>
    private void RefreshSelectedNodeLabel()
    {
        SelectedNode?.RefreshLabel();
        OnPropertyChanged(nameof(SelectedNodePath));
    }

    private void RefreshPendingEdits()
    {
        PendingEdits.Clear();
        if (Editor is not null)
        {
            foreach (var edit in Editor.PendingEdits)
            {
                PendingEdits.Add(new PendingEditViewModel(edit));
            }
        }

        RaiseEditingProperties();
    }

    private void RevertAll()
    {
        if (Editor is null || !Editor.IsDirty)
        {
            return;
        }

        var count = Editor.PendingEdits.Count;
        Editor.RevertAll();
        RefreshAllRows();
        RefreshPendingEdits();
        StatusText = $"Reverted {count:N0} change(s). The document matches the file on disk again.";
    }

    private void RevertSelected()
    {
        if (Editor is null || SelectedPendingEdit is null)
        {
            return;
        }

        var name = SelectedPendingEdit.Field;
        if (Editor.Revert(SelectedPendingEdit.Edit))
        {
            RefreshAllRows();
            RefreshPendingEdits();
            StatusText = $"Reverted the change to '{name}'.";
        }
    }

    private void RefreshAllRows(FieldRowViewModel? except = null)
    {
        foreach (var row in SelectedFields)
        {
            row.Refresh(keepMessage: ReferenceEquals(row, except));
        }

        foreach (var row in SummaryFields)
        {
            row.Refresh(keepMessage: ReferenceEquals(row, except));
        }
    }

    private void SaveAsRpe()
    {
        if (Editor is null || _current?.FileInfo is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save as RPE",
            Filter = "RPE file (*.rpe)|*.rpe",
            FileName = SuggestEditedName(_current.FileInfo.FileName),
            AddExtension = true,
            DefaultExt = ".rpe",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var target = Path.GetFullPath(dialog.FileName);
        if (string.Equals(target, _current.FileInfo.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "That is the file currently open. Use File \u25b8 Save (overwrite original) if you really mean to replace it — " +
                "that path takes a backup first.",
                "RPE Reader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PerformSave(target, allowOverwrite: false);
    }

    private void OverwriteRpe()
    {
        if (Editor is null || _current?.FileInfo is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Replace the original file?\n\n{_current.FileInfo.FullPath}\n\n" +
            $"{PendingEditCount:N0} change(s) will be written. A timestamped .bak copy is made first, " +
            "and the new file is verified by re-reading it before it replaces the original.",
            "RPE Reader — overwrite original",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        PerformSave(_current.FileInfo.FullPath, allowOverwrite: true);
    }

    private void PerformSave(string path, bool allowOverwrite)
    {
        if (Editor is null)
        {
            return;
        }

        try
        {
            var result = Editor.Save(path, allowOverwrite);

            foreach (var diagnostic in result.Diagnostics)
            {
                Messages.Add($"[{diagnostic.Severity}] {diagnostic.Message}");
            }

            if (!result.Success)
            {
                StatusText = "The file was not saved.";
                MessageBox.Show(
                    "The file was not saved.\n\n" +
                    (result.Diagnostics.LastOrDefault(d => d.Severity == DiagnosticSeverity.Error)?.Message
                     ?? "The reason was not reported."),
                    "RPE Reader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            _logger.Info($"Saved '{Path.GetFileName(path)}' with {PendingEditCount} change(s) applied.");

            var backupNote = result.BackupPath is null
                ? string.Empty
                : $"\n\nA backup of the original was written to:\n{result.BackupPath}";

            var fidelityNote = Editor.RoundTripsExactly
                ? string.Empty
                : "\n\nNote: this file could not be reproduced byte for byte — see Messages for why. " +
                  "Your changes were written correctly, but the file also differs in the way noted there.";

            MessageBox.Show(
                $"Saved {Path.GetFileName(path)}.\n\n" +
                $"Size: {RpeFileInfo.FormatSize(result.BytesWritten)}\n" +
                $"SHA-256: {result.Sha256}\n\n" +
                "The file was re-opened, re-parsed and compared with the document in memory; they match exactly. " +
                "It is ready to import into OPC Router." + fidelityNote + backupNote,
                "RPE Reader",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // The values in memory are now exactly what the saved file holds,
            // so they become the new baseline. Reverting here instead would
            // roll the document back to its pre-edit state while the file kept
            // the edits, and the next save would write that stale content.
            Editor.AcceptChanges();
            RefreshAllRows();
            RefreshPendingEdits();
            RefreshSelectedNodeLabel();

            if (allowOverwrite)
            {
                // The file the properties pane and hex view describe has just
                // been replaced, so its identity has to be re-read or they keep
                // reporting the pre-save size, timestamp, hash and bytes.
                ReloadFileIdentity(path, result.Sha256);
            }

            StatusText = allowOverwrite
                ? $"Saved over {Path.GetFileName(path)}."
                : $"Saved {Path.GetFileName(path)}. The open file is unchanged.";

            RaiseEditingProperties();
        }
        catch (Exception ex)
        {
            _logger.Error($"Save to '{Path.GetFileName(path)}' failed.", ex);
            Messages.Add($"[Error] {ex.GetType().Name}: {ex.Message}");
            MessageBox.Show($"The file could not be saved.\n\n{ex.Message}",
                "RPE Reader", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Re-reads the identity of a file this application has just replaced, and
    /// re-opens the hex view on it.
    /// </summary>
    private void ReloadFileIdentity(string path, string? sha256)
    {
        if (_current?.FileInfo is null)
        {
            return;
        }

        try
        {
            var info = new FileInfo(path);

            _current = new RpeOpenResult
            {
                Success = _current.Success,
                Document = _current.Document,
                Diagnostics = _current.Diagnostics,
                SelectedParser = _current.SelectedParser,
                FileInfo = new RpeFileInfo
                {
                    FileName = info.Name,
                    FullPath = info.FullName,
                    SizeBytes = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    CreationTimeUtc = info.CreationTimeUtc,
                    Sha256 = sha256 ?? _current.FileInfo.Sha256
                }
            };

            _hexSource?.Dispose();
            _hexSource = HexViewSource.Open(path);
            ShowHexPage(0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warn($"Could not re-read '{Path.GetFileName(path)}' after saving: {ex.GetType().Name}.");
            Messages.Add("[Warning] The file was saved, but its details could not be re-read. Re-open it to refresh them.");
        }

        RaiseFileProperties();
    }

    private static string SuggestEditedName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{stem}-edited.rpe";
    }

    /// <summary>
    /// Asks about unsaved changes. Returns false when the caller should stop.
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        if (!IsDirty)
        {
            return true;
        }

        var answer = MessageBox.Show(
            $"{PendingEditCount:N0} change(s) have not been saved. Discard them?",
            "RPE Reader",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return answer == MessageBoxResult.Yes;
    }

    private void SetClipboard(string text, string successMessage)
    {
        try
        {
            Clipboard.SetText(text);
            StatusText = successMessage;
        }
        catch (Exception ex)
        {
            // The clipboard can be locked by another process; that is not fatal.
            _logger.Warn($"Clipboard write failed: {ex.GetType().Name}.");
            StatusText = "The clipboard is in use by another application; nothing was copied.";
        }
    }

    public void Dispose()
    {
        _openCts?.Cancel();
        _openCts?.Dispose();
        _hexSource?.Dispose();
    }
}
