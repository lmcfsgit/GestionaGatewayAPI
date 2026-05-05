[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "GestionaGatewayAPI.csproj"
$publishPath = "C:\publish\GestionaGatewayAPI"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --output $publishPath

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Published to $publishPath"
