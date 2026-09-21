#Requires -Version 7.0

<#
.SYNOPSIS
    Builds, tests and publishes RPE Reader as a self-contained Windows x64 application.

.DESCRIPTION
    Produces RPEReader.exe plus its runtime in the output folder. The default
    layout is a self-contained folder rather than a single file; see the
    -SingleFile switch and README.md for why.

.PARAMETER Configuration
    MSBuild configuration. Defaults to Release.

.PARAMETER OutputPath
    Where to write the publish output. Defaults to <repo>\Release.

.PARAMETER SingleFile
    Produce a single-file executable instead of the folder layout. Note that WPF
    keeps its native libraries beside the executable unless they are bundled for
    self-extraction, which costs start-up time and extracts to a temporary
    folder at run time. The folder layout is the supported default.

.PARAMETER SkipTests
    Skip the unit test run. Not recommended.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -OutputPath C:\dist\RPEReader
    .\publish.ps1 -SingleFile
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputPath,
    [switch]$SingleFile,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot  = Split-Path -Parent $PSScriptRoot
$solution  = Join-Path $repoRoot 'RPEReader.sln'
$appProj   = Join-Path $repoRoot 'src\RPEReader.App\RPEReader.App.csproj'
$testProj  = Join-Path $repoRoot 'tests\RPEReader.Core.Tests\RPEReader.Core.Tests.csproj'

if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot 'Release' }

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# --- Prerequisites -----------------------------------------------------------

Write-Step 'Checking the .NET SDK'
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'The .NET SDK was not found on PATH. Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 and retry.'
}

$sdkVersion = (& dotnet --version).Trim()
Write-Host "    dotnet $sdkVersion"
if ([version]($sdkVersion -split '-')[0] -lt [version]'8.0.100') {
    throw "The .NET 8.0 SDK or newer is required; found $sdkVersion."
}

# --- Clean -------------------------------------------------------------------

Write-Step "Cleaning $OutputPath"
if (Test-Path $OutputPath) { Remove-Item $OutputPath -Recurse -Force }
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Normalise to a full, canonically-separated path. Callers may pass mixed
# separators (a GitHub Actions workspace expands to 'D:\a\repo\repo/Release'),
# which would otherwise corrupt the relative paths written to the manifest.
$OutputPath = (Resolve-Path -LiteralPath $OutputPath).ProviderPath.TrimEnd('\', '/')

# --- Build -------------------------------------------------------------------

Write-Step "Building the solution ($Configuration)"
& dotnet build $solution -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

# --- Test --------------------------------------------------------------------

if ($SkipTests) {
    Write-Warning 'Unit tests were skipped because -SkipTests was supplied.'
} else {
    Write-Step 'Running unit tests'
    & dotnet test $testProj -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed with exit code $LASTEXITCODE." }
}

# --- Publish -----------------------------------------------------------------

$publishArgs = @(
    'publish', $appProj,
    '-c', $Configuration,
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishReadyToRun=true',
    '-o', $OutputPath,
    '--nologo'
)

if ($SingleFile) {
    Write-Step 'Publishing as a single file (self-extracting)'
    Write-Warning 'WPF extracts its native libraries to a temporary folder at start-up in this mode. Application-allowlisting policies often block that. Prefer the default folder layout.'
    $publishArgs += '-p:PublishSingleFile=true'
    $publishArgs += '-p:EnableCompressionInSingleFile=true'
    $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
} else {
    Write-Step 'Publishing as a self-contained folder'
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

# --- Verify ------------------------------------------------------------------

Write-Step 'Verifying the output'

$exe = Join-Path $OutputPath 'RPEReader.exe'
if (-not (Test-Path $exe)) { throw "Expected $exe but it was not produced." }

$info  = Get-Item $exe
$hash  = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$count = (Get-ChildItem $OutputPath -Recurse -File).Count
$bytes = (Get-ChildItem $OutputPath -Recurse -File | Measure-Object -Property Length -Sum).Sum

Write-Host ''
Write-Host "    Executable : $exe"
Write-Host "    Version    : $($info.VersionInfo.FileVersion) (product $($info.VersionInfo.ProductVersion))"
Write-Host "    SHA-256    : $hash"
Write-Host "    Files      : $count"
Write-Host ("    Total size : {0:N1} MB" -f ($bytes / 1MB))

# A manifest makes a published drop auditable after the fact. The lines are
# collected before anything is written, so the manifest can never end up
# hashing itself while it is still being created.
$manifest = Join-Path $OutputPath 'SHA256SUMS.txt'
$lines = Get-ChildItem -LiteralPath $OutputPath -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $rel = [System.IO.Path]::GetRelativePath($OutputPath, $_.FullName)
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $rel
    }

Set-Content -LiteralPath $manifest -Value $lines -Encoding ascii

Write-Host "    Manifest   : $manifest"
Write-Host ''
Write-Host 'Publish completed.' -ForegroundColor Green
