param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "PersonalCollectionShelf.App\PersonalCollectionShelf.App.csproj"

dotnet build $project `
    -t:Run `
    -f net10.0-windows10.0.19041.0 `
    -r win-x64 `
    -c $Configuration
