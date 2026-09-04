[CmdletBinding()]
param(
    [string]$Destination,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $root 'artifacts\SqlServerDiagnostics-portable'
}
$Destination = [IO.Path]::GetFullPath($Destination)
$output = Join-Path $root 'src\SqlServerDiagnostics.App\bin\x64\Release'

if (-not $SkipBuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio vswhere.exe was not found.' }
    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($msbuild)) { throw 'MSBuild was not found.' }
    & $msbuild (Join-Path $root 'SqlServerDiagnostics.sln') /m /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
}

$rootFiles = @(
    'SqlServerDiagnostics.exe',
    'SqlServerDiagnostics.exe.config',
    'TheMaxLab.SqlServerDiagnostics.Core.dll',
    'TheMaxLab.SqlServerDiagnostics.Diagnostics.dll',
    'TheMaxLab.SqlServerDiagnostics.Infrastructure.dll',
    'TheMaxLab.SqlServerDiagnostics.Reporting.dll'
)
foreach ($name in $rootFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $name))) { throw "Required runtime file is missing: $name" }
}

$manifestSource = Join-Path $output 'diagnostics\manifest.json'
if (-not (Test-Path -LiteralPath $manifestSource)) { throw 'The runtime diagnostic manifest is missing.' }
$manifest = Get-Content -Raw -LiteralPath $manifestSource | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.diagnostics.Count -ne 26) { throw 'The runtime diagnostic manifest is invalid or does not contain 26 diagnostics.' }

if (Test-Path -LiteralPath $Destination) { Remove-Item -LiteralPath $Destination -Recurse -Force }
New-Item -ItemType Directory -Path $Destination | Out-Null
foreach ($name in $rootFiles) { Copy-Item -LiteralPath (Join-Path $output $name) -Destination (Join-Path $Destination $name) }
New-Item -ItemType Directory -Path (Join-Path $Destination 'diagnostics') | Out-Null
Copy-Item -LiteralPath $manifestSource -Destination (Join-Path $Destination 'diagnostics\manifest.json')

foreach ($diagnostic in $manifest.diagnostics) {
    $relative = $diagnostic.scriptPath -replace '/', '\'
    if ([IO.Path]::IsPathRooted($relative) -or $relative.Split('\') -contains '..' -or [IO.Path]::GetExtension($relative) -ne '.sql') { throw "Unsafe runtime script path in manifest: $relative" }
    $source = [IO.Path]::GetFullPath((Join-Path (Join-Path $output 'diagnostics') $relative))
    $diagnosticsRoot = [IO.Path]::GetFullPath((Join-Path $output 'diagnostics')) + [IO.Path]::DirectorySeparatorChar
    if (-not $source.StartsWith($diagnosticsRoot, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $source)) { throw "Runtime SQL file is missing or unsafe: $relative" }
    $target = Join-Path (Join-Path $Destination 'diagnostics') $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) { throw "Runtime SQL hash mismatch: $relative" }
}

$sqlFiles = Get-ChildItem -LiteralPath (Join-Path $Destination 'diagnostics') -Filter *.sql -Recurse -File
if ($sqlFiles.Count -ne 26) { throw "Portable package contains $($sqlFiles.Count) SQL files instead of 26." }
$allowedRoot = @($rootFiles + 'diagnostics')
$unexpectedRoot = Get-ChildItem -LiteralPath $Destination | Where-Object { $_.Name -notin $allowedRoot }
if ($unexpectedRoot) { throw 'Portable package contains unexpected root content.' }
$forbidden = Get-ChildItem -LiteralPath $Destination -Recurse -File | Where-Object { $_.Extension -in @('.pdb', '.cs', '.csproj', '.trx', '.log', '.credential') -or $_.Name -match '(?i)test|password|secret' }
if ($forbidden) { throw 'Portable package contains forbidden development or sensitive material.' }

Write-Output "Portable package validated: $Destination"
Write-Output "Root runtime files: $($rootFiles.Count)"
Write-Output "Manifest diagnostics: $($manifest.diagnostics.Count)"
Write-Output "Runtime SQL files: $($sqlFiles.Count)"