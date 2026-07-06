param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "PersonalCollectionShelf.App\PersonalCollectionShelf.App.csproj"
$logDirectory = Join-Path $env:LOCALAPPDATA "PersonalCollectionShelf\PersonalCollectionShelf.App\Data\logs"

dotnet build $project `
    -t:Run `
    -f net10.0-windows10.0.19041.0 `
    -r win-x64 `
    -c $Configuration

$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "PersonalCollectionShelf exited with code $exitCode." -ForegroundColor Red

    if (Test-Path $logDirectory) {
        $latestLog = Get-ChildItem -LiteralPath $logDirectory -Filter "*.log" |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($latestLog) {
            Write-Host ""
            Write-Host "Latest app log: $($latestLog.FullName)" -ForegroundColor Yellow
            Get-Content -LiteralPath $latestLog.FullName -Tail 80
        }
    }

    Write-Host ""
    Write-Host "Recent Windows crash events:" -ForegroundColor Yellow
    Get-WinEvent -FilterHashtable @{ LogName = "Application"; StartTime = (Get-Date).AddMinutes(-10) } -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match "PersonalCollectionShelf" } |
        Select-Object -First 3 TimeCreated, ProviderName, Id, Message |
        Format-List
}

exit $exitCode
