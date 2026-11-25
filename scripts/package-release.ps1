<#
.SYNOPSIS
    One-shot release pipeline for StarPie-PromptPaste.

.DESCRIPTION
    1. Checks the .NET SDK.
    2. Runs the pure-BCL unit tests (tests/StarPie.Plugin.PromptPaste.Tests).
    3. Builds the plugin in Release.
    4. Verifies the community red lines on the output folder:
         - StarPie.Plugin.PromptPaste.dll and plugin.json exist;
         - NO StarPie.Plugin.Abstractions.dll / StarPie.dll / other DLLs / debug files;
         - csproj AssemblyMetadata + version == plugin.json.
    5. (Optional) Runs: StarPie.exe --plugin-selftest <dll> <report> --skip-invoke
    6. Packs dist/StarPie-PromptPaste-v<version>.zip
           StarPie-PromptPaste/
             StarPie.Plugin.PromptPaste.dll
             plugin.json
             LICENSE
             README.md
       and writes the SHA256 to dist/*.sha256 and dist/RELEASE_NOTES.md.

.EXAMPLE
    .\scripts\package-release.ps1

.EXAMPLE
    .\scripts\package-release.ps1 -StarPieExe 'C:\Tools\StarPie\StarPie.exe'

.EXAMPLE
    .\scripts\package-release.ps1 -StarPieAbstractionsProject 'D:\src\StarPie\plugin\sdk\StarPie.Plugin.Abstractions\StarPie.Plugin.Abstractions.csproj'
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',

    # Override the SDK project path (default: ..\StarPie next to this repository).
    [string]$StarPieAbstractionsProject = '',

    # Path to StarPie.exe. When given, the headless self-test is run and must pass.
    [string]$StarPieExe = '',

    [switch]$SkipTests,

    # Build + checks only, do not create the ZIP.
    [switch]$NoPackage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$text) { Write-Host ''; Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Ok([string]$text) { Write-Host "    [OK] $text" -ForegroundColor Green }
function Fail([string]$text) { Write-Host "    [FAIL] $text" -ForegroundColor Red; exit 1 }

$repoRoot     = Split-Path -Parent $PSScriptRoot
$projectDir   = Join-Path $repoRoot 'src\StarPie.Plugin.PromptPaste'
$projectFile  = Join-Path $projectDir 'StarPie.Plugin.PromptPaste.csproj'
$manifestFile = Join-Path $projectDir 'plugin.json'
$testsProject = Join-Path $repoRoot 'tests\StarPie.Plugin.PromptPaste.Tests\StarPie.Plugin.PromptPaste.Tests.csproj'
$mainDll      = 'StarPie.Plugin.PromptPaste.dll'

$manifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
$version  = [string]$manifest.version

# ---------------------------------------------------------------- 1. SDK
Write-Step 'Checking .NET SDK'
$sdkVersion = (& dotnet --version) 2>$null
if ($LASTEXITCODE -ne 0 -or -not $sdkVersion) { Fail 'dotnet not found. Install the .NET 8 SDK.' }
if (-not $sdkVersion.StartsWith('8.')) {
    Write-Warning "dotnet --version reports $sdkVersion (expected 8.x). The plugin still targets net8.0-windows."
}
Write-Ok "dotnet $sdkVersion"

# ---------------------------------------------------------------- 2. Tests
if (-not $SkipTests) {
    Write-Step 'Running unit tests'
    & dotnet run --project $testsProject -c $Configuration
    if ($LASTEXITCODE -ne 0) { Fail 'Unit tests failed.' }
    Write-Ok 'Unit tests passed'
}

# ---------------------------------------------------------------- 3. Build
Write-Step "Building plugin ($Configuration)"
$buildArgs = @('build', $projectFile, '-c', $Configuration, '--nologo')
if ($StarPieAbstractionsProject) { $buildArgs += "-p:StarPieAbstractionsProject=$StarPieAbstractionsProject" }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { Fail 'Build failed.' }

$candidates = @(
    (Join-Path $projectDir "bin\$Configuration\net8.0-windows"),
    (Join-Path $projectDir "bin\x64\$Configuration\net8.0-windows")
)
$outDir = $candidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ $mainDll) } | Select-Object -First 1
if (-not $outDir) { Fail "Build output not found in: $($candidates -join '; ')" }
Write-Ok "Output: $outDir"

# ---------------------------------------------------------------- 4. Red lines
Write-Step 'Checking output red lines'
$files = Get-ChildItem -LiteralPath $outDir -File -Recurse
foreach ($f in $files) { Write-Host "    - $($f.FullName.Substring($outDir.Length).TrimStart('\'))" }

foreach ($required in @($mainDll, 'plugin.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $outDir $required))) { Fail "Missing $required" }
}
if (Test-Path -LiteralPath (Join-Path $outDir 'StarPie.Plugin.Abstractions.dll')) {
    Fail 'StarPie.Plugin.Abstractions.dll is in the output. Keep <Private>false</Private> on the SDK reference.'
}
if (Test-Path -LiteralPath (Join-Path $outDir 'StarPie.dll')) { Fail 'StarPie.dll is in the output.' }

$extraDlls = $files | Where-Object { $_.Extension -ieq '.dll' -and $_.Name -ine $mainDll }
if ($extraDlls) { Fail "Unexpected DLLs in output: $($extraDlls.Name -join ', ')" }
$debugFiles = $files | Where-Object { $_.Extension -in @('.pdb', '.deps.json') -or $_.Name -like '*.deps.json' }
if ($debugFiles) { Write-Warning "Debug/dependency files present (not packaged): $($debugFiles.Name -join ', ')" }
Write-Ok 'No StarPie.Plugin.Abstractions.dll, no StarPie.dll, no third-party DLLs'

# metadata consistency: csproj vs plugin.json
[xml]$csproj = Get-Content -LiteralPath $projectFile -Raw -Encoding UTF8
$meta = @{}
foreach ($node in $csproj.SelectNodes('//AssemblyMetadata')) { $meta[$node.GetAttribute('Include')] = $node.GetAttribute('Value') }
$projVersion = ($csproj.SelectNodes('//Version') | Select-Object -First 1).InnerText

$capsJson = (@($manifest.capabilities) | Sort-Object) -join ','
$capsMeta = (($meta['StarPiePluginCapabilities'] -split '[,;|]') | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object) -join ','

$checks = @(
    @('StarPiePluginId',           $meta['StarPiePluginId'],       [string]$manifest.id),
    @('StarPiePluginName',         $meta['StarPiePluginName'],     [string]$manifest.name),
    @('StarPiePluginCapabilities', $capsMeta,                      $capsJson),
    @('StarPiePluginLicense',      $meta['StarPiePluginLicense'],  [string]$manifest.license),
    @('StarPiePluginHomepage',     $meta['StarPiePluginHomepage'], [string]$manifest.homepage),
    @('Version',                   $projVersion,                   $version)
)
foreach ($c in $checks) {
    if ($c[1] -cne $c[2]) { Fail "$($c[0]) mismatch: csproj='$($c[1])' plugin.json='$($c[2])'" }
}
Write-Ok 'csproj AssemblyMetadata and version match plugin.json'

$dllPath = Join-Path $outDir $mainDll

# ---------------------------------------------------------------- 5. Self-test
if ($StarPieExe) {
    Write-Step 'Running StarPie headless self-test (--skip-invoke)'
    if (-not (Test-Path -LiteralPath $StarPieExe)) { Fail "StarPie.exe not found: $StarPieExe" }
    $distDir = Join-Path $repoRoot 'dist'
    New-Item -ItemType Directory -Force -Path $distDir | Out-Null
    $report = Join-Path $distDir 'selftest-report.txt'
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }

    # StarPie.exe is a GUI-subsystem app: use Start-Process -Wait so we get its exit code.
    $proc = Start-Process -FilePath $StarPieExe `
        -ArgumentList @('--plugin-selftest', "`"$dllPath`"", "`"$report`"", '--skip-invoke') `
        -Wait -PassThru -WindowStyle Hidden
    if (Test-Path -LiteralPath $report) {
        Get-Content -LiteralPath $report -Encoding UTF8 | ForEach-Object { Write-Host "    $_" }
    }
    if ($proc.ExitCode -ne 0) { Fail "Self-test failed (exit code $($proc.ExitCode)). Report: $report" }
    Write-Ok "Self-test passed. Report: $report"
}
else {
    Write-Warning 'Self-test skipped. Pass -StarPieExe <path to StarPie.exe> to run it.'
}

if ($NoPackage) { Write-Host ''; Write-Host 'Done (no package).' -ForegroundColor Green; exit 0 }

# ---------------------------------------------------------------- 6. Package
Write-Step 'Packaging'
$distDir = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$zipName = "StarPie-PromptPaste-v$version.zip"
$zipPath = Join-Path $distDir $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

$payload = [ordered]@{
    $mainDll      = $dllPath
    'plugin.json' = (Join-Path $outDir 'plugin.json')
    'LICENSE'     = (Join-Path $repoRoot 'LICENSE')
    'README.md'   = (Join-Path $repoRoot 'README.md')
}
foreach ($p in $payload.Values) { if (-not (Test-Path -LiteralPath $p)) { Fail "Missing package file: $p" } }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $payload.GetEnumerator()) {
        # forward slashes so the archive is portable
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $entry.Value, "StarPie-PromptPaste/$($entry.Key)",
            [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $zip.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$dllHash = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$zipPath.sha256" -Value "$zipHash  $zipName" -Encoding ASCII

$notes = @"
# StarPie-PromptPaste v$version

提示词快贴 / Prompt Paste — StarPie 社区插件 / community plugin.

- Plugin ID: ``$($manifest.id)``
- Action ID: ``$($manifest.id).pastePrompt``
- Target: ``$($manifest.targetFramework)`` / ``$($manifest.platform)``, SDK API ``$($manifest.apiVersion)``, StarPie >= ``$($manifest.minHostVersion)``
- Capabilities: $(@($manifest.capabilities) -join ', ')

## SHA256

| File | SHA256 |
| --- | --- |
| ``$zipName`` | ``$zipHash`` |
| ``$mainDll`` | ``$dllHash`` |

## Install / 安装

1. 解压 ZIP / Extract the ZIP.
2. StarPie 设置 → 插件与扩展 → 手动安装社区插件 → 选择 ``StarPie-PromptPaste\$mainDll``。
   Settings → Plugins → Install community plugin manually → pick ``StarPie-PromptPaste\$mainDll``.
3. 确认能力声明后启用插件 / Review the capabilities and enable the plugin.
"@
$notesPath = Join-Path $distDir 'RELEASE_NOTES.md'
[System.IO.File]::WriteAllText($notesPath, $notes, (New-Object System.Text.UTF8Encoding($false)))

Write-Ok "Package : $zipPath"
Write-Ok "SHA256  : $zipHash"
Write-Ok "Notes   : $notesPath"
Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
