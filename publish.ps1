[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$apiProjectPath = Join-Path $PSScriptRoot "GestionaGatewayAPI.csproj"
$coreProjectPath = Join-Path $PSScriptRoot "GestionaGateway.Core\GestionaGateway.Core.csproj"
$publishPath = "C:\publish\GestionaGatewayAPI"
$publishedCoreArtifactsPath = Join-Path $publishPath "GestionaGateway.Core"

if (-not (Test-Path -LiteralPath $apiProjectPath)) {
    throw "API project file not found: $apiProjectPath"
}

if (-not (Test-Path -LiteralPath $coreProjectPath)) {
    throw "Core project file not found: $coreProjectPath"
}

if (Test-Path -LiteralPath $publishPath) {
    Get-ChildItem -LiteralPath $publishPath -Force | Remove-Item -Recurse -Force
}
else {
    New-Item -ItemType Directory -Path $publishPath -Force | Out-Null
}

# Publish the web API project explicitly. The core library is included through
# the ProjectReference and will be built and copied automatically.
dotnet publish $apiProjectPath `
    --configuration $Configuration `
    --output $publishPath

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path -LiteralPath $publishedCoreArtifactsPath) {
    Remove-Item -LiteralPath $publishedCoreArtifactsPath -Recurse -Force
}

Write-Host "Published API project to $publishPath"
