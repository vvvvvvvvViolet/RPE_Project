using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using RPEReader.App.Mvvm;
using RPEReader.Core.Abstractions;
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

    public MainViewModel(RpeFileService? fileService = null, IAppLogger? logger = null)
    {
        _logger = logger ?? Core.Logging.NullLogger.Instance;
        _fileService = fileService ?? new RpeFileService(logger: _logger);
        _searchService = new SearchService(_fileService.Limits);

        OpenFileCommand = new AsyncRelayCommand(OpenFileDialogAsync, () => !IsBusy);
        CloseFileCommand = new RelayCommand(CloseFile, () => HasDocument);
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
    }

    /// <summary>Hex-dump rows rendered per page.</summary>
    public const int HexLinesPerPage = 2048;

    public ObservableCollection<NodeViewModel> RootNodes { get; } = new();

    public ObservableCollection<FieldRowViewModel> SelectedFields { get; } = new();

    public ObservableCollection<FieldRowViewModel> SummaryFields { get; } = new();

    public ObservableCollection<SearchHitViewModel> SearchResults { get; } = new();

    public ObservableCollection<HexLineViewModel> HexLines { get; } = new();

    public ObservableCollection<string> Messages { get; } = new();

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

    public bool HasDocument => _current?.Document is not null;

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
                    SelectedFields.Add(new FieldRowViewModel(field));
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
            SummaryFields.Add(new FieldRowViewModel(field));
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
        StatusText = document.Incomplete || warnings > 0
            ? $"Opened {_current.FileInfo?.FileName} with {warnings} message(s). Some content may be incomplete — see Messages."
            : $"Opened {_current.FileInfo?.FileName} as {document.FormatDisplayName}.";
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
    }

    private void CloseFile()
    {
        RootNodes.Clear();
        SelectedFields.Clear();
        SummaryFields.Clear();
        SearchResults.Clear();
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
