param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "PersonalCollectionShelf.App\PersonalCollectionShelf.App.csproj"

dotnet build $project `
    -t:Run `
    -f net10.0-android `
    -c $Configuration `
    -p:EnableAndroidTarget=true
