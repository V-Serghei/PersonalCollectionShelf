param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath
)

$resolvedConfig = (Resolve-Path -LiteralPath $ConfigPath).Path
$configuration = Get-Content -LiteralPath $resolvedConfig -Raw | ConvertFrom-Json
$requiredFields = @("apiKey", "projectId", "googleClientId")
$missingFields = @($requiredFields | Where-Object {
    $property = $configuration.PSObject.Properties[$_]
    $null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)
})
if ($missingFields.Count -gt 0) {
    throw "Cloud configuration is missing: $($missingFields -join ', '). Use firebase.example.json as the template."
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$rawDirectory = Join-Path $projectRoot "PersonalCollectionShelf.App\Resources\Raw"
$destination = Join-Path $rawDirectory "firebase.json"

New-Item -ItemType Directory -Path $rawDirectory -Force | Out-Null
Copy-Item -LiteralPath $resolvedConfig -Destination $destination -Force
Write-Host "Cloud configuration copied to $destination"
Write-Host "Rebuild the Windows and Android apps so the same configuration is packaged for both devices."
