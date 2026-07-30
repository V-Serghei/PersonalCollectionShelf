param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath
)

$resolvedConfig = (Resolve-Path -LiteralPath $ConfigPath).Path
$configuration = Get-Content -LiteralPath $resolvedConfig -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$configuration.tmdbReadAccessToken)) {
    throw "Metadata configuration is missing tmdbReadAccessToken."
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$destination = Join-Path $projectRoot "PersonalCollectionShelf.App\Resources\Raw\metadata.json"
Copy-Item -LiteralPath $resolvedConfig -Destination $destination -Force
Write-Host "Metadata configuration copied to $destination"
Write-Host "Rebuild the Windows and Android apps. Kinopoisk is optional."
