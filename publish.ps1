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
$publishedTestArtifactsPath = Join-Path $publishPath "GestionaGatewayAPI.Tests"
$publishedArtifactsPath = Join-Path $publishPath "artifacts"
$publishBuildPath = Join-Path $PSScriptRoot ".publish-build"

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

if (Test-Path -LiteralPath $publishBuildPath) {
    Get-ChildItem -LiteralPath $publishBuildPath -Force | Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Path $publishBuildPath -Force | Out-Null

# Publish the web API project explicitly. The core library is included through
# the ProjectReference and will be built and copied automatically.
dotnet publish $apiProjectPath `
    --configuration $Configuration `
    --output $publishPath `
    --artifacts-path $publishBuildPath `
    /p:UseAppHost=false `
    --disable-build-servers

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path -LiteralPath $publishedCoreArtifactsPath) {
    Remove-Item -LiteralPath $publishedCoreArtifactsPath -Recurse -Force
}

if (Test-Path -LiteralPath $publishedTestArtifactsPath) {
    Remove-Item -LiteralPath $publishedTestArtifactsPath -Recurse -Force
}

if (Test-Path -LiteralPath $publishedArtifactsPath) {
    Remove-Item -LiteralPath $publishedArtifactsPath -Recurse -Force
}

if (Test-Path -LiteralPath $publishBuildPath) {
    Get-ChildItem -LiteralPath $publishBuildPath -Force | Remove-Item -Recurse -Force
}

Write-Host "Published API project to $publishPath"
