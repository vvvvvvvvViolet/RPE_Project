# RPE Reader

A read-only Windows desktop viewer and analyser for `.rpe` files, built with
C# / .NET 8 and WPF (MVVM).

The supplied `.rpe` samples turned out to be **Inray OPC Router 4 project
exports** — a ZIP container holding a single `OpcRouter4.xml` document. RPE
Reader parses that format structurally and falls back to a generic inspector
(signature, encoding, entropy, strings, hex) for anything it does not recognise,
so an unknown revision is still examinable rather than rejected.

See **[docs/RPE_FORMAT_FINDINGS.md](docs/RPE_FORMAT_FINDINGS.md)** for the full
format analysis and the evidence behind it.

---

## Features

**Opening**
- Open via toolbar, `File ▸ Open` (Ctrl+O), or drag-and-drop of a single file
- Double-click a `.rpe` in Explorer once the installer registers the association
- Files are opened **read-only**; the source is never modified

**File identity**
- Name, full path, size, modification time (local and UTC), and **SHA-256**
- Detected format and the parser that handled it

**Views**
- **Structure tree** of the parsed document, virtualised for large files
- **Details** grid — every field of the selected node, with declared type and decoded notes
- **Summary** — document-level properties and counts
- **Raw text** — the decoded text payload
- **Hex** — paged offset / hexadecimal / ASCII dump, read straight from disk
- **Messages** — every warning raised while reading, including anything truncated by a limit

**Search**
- **Text** — node names, field names and field values
- **Field name** — field names only
- **Hex bytes** — byte patterns against the raw file (`50 4B 03 04`, `504B0304`, `50-4B-03-04`; `??` matches any byte)
- Selecting a result jumps to the node, or to that page of the hex dump

**Output**
- Export to **JSON** (tree shape), **CSV** (one row per field) or **TXT** (indented outline)
- Every export records the source file's SHA-256
- Copy the selected node or the summary to the clipboard; grids support Ctrl+C

**Other**
- About, version information and a built-in quick guide
- Error log under `Logs\`, containing **no data read out of a `.rpe` file**

---

## Requirements

**To run:** Windows 10 (1809 / build 17763) or Windows 11, 64-bit.
No .NET installation is needed — the build is self-contained.

**To build:** the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
or newer. Visual Studio 2022 (17.8+) with the *.NET desktop development* workload
opens `RPEReader.sln` directly.

`build/publish.ps1` requires **PowerShell 7** (`pwsh`) — it uses APIs that
Windows PowerShell 5.1 does not provide. `dotnet build` and `dotnet test` have
no such requirement.

---

## Build, test, publish

```powershell
git clone https://github.com/vvvvvvvvViolet/RPE_Project.git
cd RPE_Project

dotnet build RPEReader.sln -c Release      # build everything
dotnet test  RPEReader.sln -c Release      # run the unit tests
pwsh .\build\publish.ps1                   # build + test + publish to .\Release
```

`publish.ps1` refuses to publish if the tests fail, prints the executable's
version and SHA-256, and writes a `SHA256SUMS.txt` manifest of the whole drop.

### Getting a built `RPEReader.exe`

The published output is **not committed to this repository** — a self-contained
WPF drop is 146 MB across 245 files, which does not belong in version control.
There are two ways to get the binary:

1. **Build it:** `pwsh .\build\publish.ps1` produces `.\Release\RPEReader.exe`
   plus its runtime.
2. **Download it:** the `build-release` GitHub Actions workflow publishes the
   same folder on a `windows-latest` runner and uploads it as the
   **`RPEReader-win-x64`** artifact.

[docs/RELEASE_MANIFEST.md](docs/RELEASE_MANIFEST.md) records the exact contents,
sizes and SHA-256 values of a verified build, so a drop you were handed can be
checked against it.

| Switch | Effect |
|---|---|
| `-OutputPath <dir>` | Publish somewhere other than `.\Release` |
| `-Configuration <cfg>` | Build configuration (default `Release`) |
| `-SingleFile` | Produce a single self-extracting executable (see below) |
| `-SkipTests` | Skip the test run — not recommended |

### Running the tests directly

```powershell
dotnet test tests\RPEReader.Core.Tests\RPEReader.Core.Tests.csproj -c Release

# with detail, or filtered
dotnet test tests\RPEReader.Core.Tests\RPEReader.Core.Tests.csproj -v normal
dotnet test tests\RPEReader.Core.Tests\RPEReader.Core.Tests.csproj --filter FullyQualifiedName~CorruptFileTests
```

The test project targets `net8.0` (not `net8.0-windows`), so the suite runs on
Windows, Linux and macOS build agents.

### Why the default output is a folder, not a single file

The requirement allowed a self-contained folder if single-file publishing causes
problems. It does, and the numbers below were measured on this codebase:

| Mode | Files produced | Size |
|---|---|---|
| Self-contained folder *(default)* | 245 | 146 MB |
| `PublishSingleFile=true` | **6** — `RPEReader.exe` plus 5 loose native DLLs | 72 MB |
| `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract` | 1 | 67 MB |

Two reasons the folder is the default:

1. **Plain single-file publishing does not actually produce one file for WPF.**
   `wpfgfx_cor3.dll`, `PresentationNative_cor3.dll`, `D3DCompiler_47_cor3.dll`,
   `PenImc_cor3.dll` and `vcruntime140_cor3.dll` remain beside the executable,
   so the deployment is still multi-file — with none of the simplicity that
   motivated the switch.
2. **Bundling them forces self-extraction at start-up.** The runtime unpacks the
   native libraries to a temporary directory on every launch. That costs start-up
   time and, more importantly for an OT environment, is routinely blocked by
   application-allowlisting policy (AppLocker / WDAC), which is exactly where a
   tool like this gets used.

Trimming is also disabled: WPF resolves types reflectively from XAML, and the
trimmer removes types the parser still needs at run time.

`-SingleFile` remains available if your deployment prefers it.

---

## Installer

Requires [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
pwsh .\build\publish.ps1                                              # populate .\Release first
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\build\installer.iss
```

Produces `dist\RPEReader-Setup-1.0.0-x64.exe`, which:

- installs per-machine when elevated, per-user otherwise (no forced elevation)
- registers the `.rpe` association under the matching hive, with an icon, so
  double-clicking a `.rpe` opens it
- adds Start menu entries and an optional desktop shortcut
- removes the association and the `Logs\` folder on uninstall

---

## Project layout

```
RPE_Project/
├── RPEReader.sln
├── Directory.Build.props              Shared version, language and analysis settings
├── src/
│   ├── RPEReader.Core/                net8.0 — no UI, no third-party packages
│   │   ├── Abstractions/              IRpeParser, RpeProbe, RpeParseContext, IAppLogger
│   │   ├── Models/                    RpeDocument, RpeNode, RpeField, RpeLimits, …
│   │   ├── Detection/                 Signatures, probe factory, SafeZipReader
│   │   ├── Parsing/                   OpcRouter4Parser, GenericRpeParser, registry
│   │   │   └── OpcRouter4/            Schema knowledge, XML mapper, DateTime decoder
│   │   ├── Services/                  Hashing, hex dump/paging, search, entropy, strings
│   │   ├── Export/                    JSON, CSV, text exporters
│   │   └── Logging/                   FileLogger, NullLogger
│   └── RPEReader.App/                 net8.0-windows — WPF, MVVM
│       ├── Mvvm/                      ObservableObject, RelayCommand, AsyncRelayCommand
│       ├── ViewModels/                MainViewModel and row view models
│       ├── Views/                     MainWindow, AboutWindow, HelpWindow
│       ├── Converters/                Value converters
│       └── Resources/app.ico          Application icon (16–128 px)
├── tests/RPEReader.Core.Tests/        xUnit — detection, parsing, corruption, export
├── build/
│   ├── publish.ps1                    Build + test + publish + manifest
│   └── installer.iss                  Inno Setup script
└── docs/RPE_FORMAT_FINDINGS.md        Format analysis
```

The layering is deliberate: `RPEReader.Core` contains no UI code and no WPF
reference, so the parsers, exporters and services are testable on any platform
and reusable from a CLI or service if that is ever wanted.

### Adding support for a new RPE revision

1. Implement `IRpeParser` (see `src/RPEReader.Core/Abstractions/IRpeParser.cs`).
2. Match cheaply in `CanParse` using the header sample and container listing in `RpeProbe`.
3. Report problems through `ParseResult` rather than throwing.
4. Register it in `RpeParserRegistry.CreateDefault()` with a `Priority` above `GenericRpeParser`.

The registry picks the highest-priority parser that accepts the probe. If a
parser throws or refuses a file, the service falls back to the generic inspector
rather than leaving the user with nothing.

---

## Security posture

| Commitment | How it is enforced |
|---|---|
| Read-only | Every handle is `FileAccess.Read`; a test asserts the file's bytes and timestamp are unchanged after opening |
| Offline | No HTTP client, no socket, no telemetry anywhere in the codebase |
| No code execution | Nothing in a file is executed — no macros, no scripts, no embedded executables |
| No unsafe deserialisation | No `BinaryFormatter`; JSON is only ever *written*, never read back |
| No type resolution | `Type="…"` attributes are shown as text; `Type.GetType` is never called on file content |
| XXE / entity expansion | `DtdProcessing = Prohibit`, `XmlResolver = null`, `MaxCharactersFromEntities = 0` |
| Decompression bombs | Per-entry, total-size and 200:1 ratio ceilings checked before reading |
| Path traversal | Absolute, drive-rooted and `..` entry names rejected; nothing is extracted to disk |
| Out-of-memory | Caps on file size, node count, field count, value length, raw text and search hits |
| Large files | The hex viewer pages from disk; the whole file is never loaded to view it |
| Safe logs | The logger writes file names, sizes and error text only — never file content |

Limits live in `RpeLimits` and cannot be changed from inside a file being read.

Defaults: 512 MB maximum file, 256 MB total uncompressed, 128 MB per entry,
2,000 entries, 200:1 ratio, 200 XML levels, 500,000 nodes, 5,000 search hits.

### A note on the sample files

Real `.rpe` exports describe live OT infrastructure: OPC UA and MQTT endpoints,
database servers and service accounts, full tag browse paths, and the
installation's licence identifier. They contain no passwords — OPC Router stores
those by reference — but they are still sensitive.

The samples used for this analysis are therefore **not committed** to this
repository, and `.gitignore` excludes `*.rpe`. The test suite builds its own
synthetic fixtures with invented values instead.

---

## Known limitations

- Only `ExportType="Templates"` and `FileVersion="Version_3"` exports were
  available to analyse. Other shapes are handled by the generic section walk but
  are unverified.
- `ConfigType` and numeric `GroupType` codes are shown as raw values; the samples
  do not establish a complete mapping and no label is invented.
- Exports taken *without* `EncryptedFieldHandling="Skip"` would contain
  ciphertext this build does not attempt to decrypt, by design.
- The connection graph (`ConnectionLine` → `LocalId`) is exposed as data but not
  drawn as a diagram.
- The build is x64 only. No ARM64 or x86 configuration is provided.

`docs/RPE_FORMAT_FINDINGS.md` §7 lists exactly which additional sample files
would close each gap.

---

## Licence and notices

Third-party components and their licences are listed in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). The shipped application has no
third-party runtime dependencies — only the .NET 8 base class library and WPF.

"OPC Router" is a product of Inray Industriesoftware GmbH. This project is an
independent, read-only viewer and is not affiliated with or endorsed by Inray.
