param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\MockUpAi.App\MockUpAi.App.csproj"
$solution = Join-Path $repoRoot "MockUpAi.sln"
$output = Join-Path $repoRoot "artifacts\InterviewFlo-$Runtime"

if (-not $SkipTests) {
    dotnet test $solution -c $Configuration
}

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    -o $output

$localSettings = Join-Path $output "appsettings.Local.json"
if (Test-Path $localSettings) {
    Remove-Item $localSettings -Force
}

$localSettingsExample = Join-Path $repoRoot "src\MockUpAi.App\appsettings.Local.example.json"
if (Test-Path $localSettingsExample) {
    Copy-Item $localSettingsExample (Join-Path $output "appsettings.Local.example.json") -Force
}

Write-Host "InterviewFlo package created at $output"
