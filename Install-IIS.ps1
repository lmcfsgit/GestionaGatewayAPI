<#
.SYNOPSIS
Installs or updates a published Gestiona Gateway API ZIP as an IIS website.

.PARAMETER ZipPath
Path to the ZIP file containing the published application.

.PARAMETER Port
HTTP port to bind to the IIS website.

.PARAMETER InstallPath
Directory where the published application will be installed or updated.

.PARAMETER SiteName
Name of the IIS website and application pool. Defaults to GestionaGatewayAPI.

.EXAMPLE
.\Install-IIS.ps1 -ZipPath C:\publish\GestionaGatewayAPI.zip -Port 8080 -InstallPath C:\inetpub\GestionaGatewayAPI

.EXAMPLE
.\Install-IIS.ps1 -ZipPath C:\publish\GestionaGatewayAPI.zip -Port 8081 -InstallPath C:\inetpub\GestionaGatewayAPI-Test -SiteName GestionaGatewayAPI-Test

.NOTES
When the named website and application pool already exist and match the supplied
path and port, the script performs an update. It preserves logs and
appsettings*.json, preserves web.config, and restores the previous application
if deployment fails.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ZipPath,

    [Parameter(Mandatory)]
    [ValidateRange(1, 65535)]
    [int]$Port,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$InstallPath,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^[^\\/:*?"<>|]+$')]
    [string]$SiteName = "GestionaGatewayAPI"
)

$ErrorActionPreference = "Stop"

$applicationName = "GestionaGatewayAPI"
$applicationPoolName = $SiteName
$stagingPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "$SiteName-$([guid]::NewGuid().ToString('N'))"
$backupPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "$SiteName-backup-$([guid]::NewGuid().ToString('N'))"
$websiteCreated = $false
$applicationPoolCreated = $false
$installDirectoryCreated = $false
$installPathInitiallyExisted = $false
$isUpdate = $false
$updateDeploymentStarted = $false
$backupCreated = $false
$websiteWasStarted = $false
$applicationPoolWasStarted = $false

# IIS configuration requires an elevated PowerShell session.
function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    throw "Run this script from an elevated PowerShell session."
}

$resolvedZipPath = (Resolve-Path -LiteralPath $ZipPath).Path
if ([System.IO.Path]::GetExtension($resolvedZipPath) -ne ".zip") {
    throw "ZipPath must point to a .zip file: $resolvedZipPath"
}

$resolvedInstallPath = [System.IO.Path]::GetFullPath($InstallPath)
$installRoot = [System.IO.Path]::GetPathRoot($resolvedInstallPath)
if ($resolvedInstallPath.TrimEnd("\") -eq $installRoot.TrimEnd("\")) {
    throw "InstallPath cannot be the root of a drive."
}

# Load the IIS provider and management cmdlets.
Import-Module WebAdministration -ErrorAction Stop

# ASP.NET Core applications hosted in IIS require the Hosting Bundle module.
$aspNetCoreModule = Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue
if ($null -eq $aspNetCoreModule) {
    throw @"
ASP.NET Core Module V2 is not installed in IIS.
Install the .NET 8 Hosting Bundle, restart IIS, and run this script again.
"@
}

$websiteExists = Test-Path "IIS:\Sites\$SiteName"
$applicationPoolExists = Test-Path "IIS:\AppPools\$applicationPoolName"

if ($websiteExists -ne $applicationPoolExists) {
    throw "Website and application pool '$SiteName' must either both exist for an update or both be absent for a new installation."
}

if ($websiteExists) {
    $isUpdate = $true
    $existingWebsite = Get-Item "IIS:\Sites\$SiteName"
    $existingPhysicalPath = [Environment]::ExpandEnvironmentVariables(
        $existingWebsite.physicalPath)
    $resolvedExistingPhysicalPath = [System.IO.Path]::GetFullPath(
        $existingPhysicalPath)

    if (-not $resolvedExistingPhysicalPath.Equals(
        $resolvedInstallPath,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Existing website '$SiteName' uses '$resolvedExistingPhysicalPath', not '$resolvedInstallPath'."
    }

    if ($existingWebsite.applicationPool -ne $applicationPoolName) {
        throw "Existing website '$SiteName' does not use application pool '$applicationPoolName'."
    }

    $matchingBinding = Get-WebBinding -Name $SiteName -Protocol "http" |
        Where-Object {
            $bindingParts = $_.bindingInformation -split ":", 3
            $bindingParts.Count -ge 2 -and $bindingParts[1] -eq $Port.ToString()
        } |
        Select-Object -First 1

    if ($null -eq $matchingBinding) {
        throw "Existing website '$SiteName' does not have an HTTP binding on port $Port."
    }
}

# New installations refuse to replace an existing binding or destination files.
if (-not $isUpdate) {
    $conflictingBinding = Get-WebBinding |
        Where-Object {
            $bindingParts = $_.bindingInformation -split ":", 3
            $bindingParts.Count -ge 2 -and $bindingParts[1] -eq $Port.ToString()
        } |
        Select-Object -First 1

    if ($null -ne $conflictingBinding) {
        throw "HTTP port $Port is already used by an IIS binding."
    }

    if (Test-Path -LiteralPath $resolvedInstallPath) {
        $installPathInitiallyExisted = $true
        $existingContent = Get-ChildItem -LiteralPath $resolvedInstallPath -Force |
            Select-Object -First 1
        if ($null -ne $existingContent) {
            throw "InstallPath must be empty: $resolvedInstallPath"
        }
    }
}
elseif (-not (Test-Path -LiteralPath $resolvedInstallPath)) {
    throw "The existing website installation path does not exist: $resolvedInstallPath"
}

function Copy-ApplicationContent {
    param(
        [Parameter(Mandatory)]
        [string]$Source,

        [Parameter(Mandatory)]
        [string]$Destination
    )

    Get-ChildItem -LiteralPath $Source -Force |
        Copy-Item -Destination $Destination -Recurse -Force
}

function Remove-ApplicationContent {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Get-ChildItem -LiteralPath $Path -Force |
        Where-Object { $_.Name -ne "logs" } |
        Remove-Item -Recurse -Force
}

function Restore-ApplicationBackup {
    if (-not $backupCreated) {
        return
    }

    Remove-ApplicationContent -Path $resolvedInstallPath
    Copy-ApplicationContent -Source $backupPath -Destination $resolvedInstallPath
}

try {
    # Extract to a unique staging directory and locate the published app root.
    New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
    Expand-Archive -LiteralPath $resolvedZipPath -DestinationPath $stagingPath

    $applicationDlls = @(
        Get-ChildItem -LiteralPath $stagingPath -Filter "$applicationName.dll" -File -Recurse
    )

    if ($applicationDlls.Count -ne 1) {
        throw "The ZIP must contain exactly one $applicationName.dll file."
    }

    $publishedRoot = $applicationDlls[0].Directory.FullName
    $webConfigPath = Join-Path $publishedRoot "web.config"
    if (-not (Test-Path -LiteralPath $webConfigPath)) {
        throw "The published application does not contain web.config."
    }

    if ($isUpdate) {
        New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
        $backupCreated = $true
        Get-ChildItem -LiteralPath $resolvedInstallPath -Force |
            Where-Object { $_.Name -ne "logs" } |
            Copy-Item -Destination $backupPath -Recurse -Force

        $websiteWasStarted =
            (Get-Website -Name $SiteName).State -eq "Started"
        $applicationPoolWasStarted =
            (Get-WebAppPoolState -Name $applicationPoolName).Value -eq "Started"

        $updateDeploymentStarted = $true
        if ($websiteWasStarted) {
            Stop-Website -Name $SiteName
        }
        if ($applicationPoolWasStarted) {
            Stop-WebAppPool -Name $applicationPoolName
        }

        Remove-ApplicationContent -Path $resolvedInstallPath
        Copy-ApplicationContent -Source $publishedRoot -Destination $resolvedInstallPath

        # Keep installation-specific configuration while deploying new binaries.
        Get-ChildItem -LiteralPath $backupPath -Filter "appsettings*.json" -File |
            Copy-Item -Destination $resolvedInstallPath -Force
        $backupWebConfigPath = Join-Path $backupPath "web.config"
        if (Test-Path -LiteralPath $backupWebConfigPath) {
            Copy-Item -LiteralPath $backupWebConfigPath -Destination $resolvedInstallPath -Force
        }
    }
    else {
        if (-not (Test-Path -LiteralPath $resolvedInstallPath)) {
            New-Item -ItemType Directory -Path $resolvedInstallPath -Force | Out-Null
            $installDirectoryCreated = $true
        }

        Copy-ApplicationContent -Source $publishedRoot -Destination $resolvedInstallPath

        # ASP.NET Core runs in an IIS application pool without the .NET CLR.
        New-WebAppPool -Name $applicationPoolName | Out-Null
        $applicationPoolCreated = $true
        Set-ItemProperty "IIS:\AppPools\$applicationPoolName" -Name managedRuntimeVersion -Value ""
        Set-ItemProperty "IIS:\AppPools\$applicationPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
    }

    $logsPath = Join-Path $resolvedInstallPath "logs"
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null

    $applicationPoolIdentity = "IIS AppPool\$applicationPoolName"
    # Application files are read-only; only the logs directory is writable.
    & icacls.exe $resolvedInstallPath /grant "${applicationPoolIdentity}:(OI)(CI)(RX)" /T /C |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to grant read permissions to the application pool."
    }

    & icacls.exe $logsPath /grant "${applicationPoolIdentity}:(OI)(CI)(M)" /T /C |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to grant write permissions to the logs directory."
    }

    if ($isUpdate) {
        if ($applicationPoolWasStarted) {
            Start-WebAppPool -Name $applicationPoolName
        }
        if ($websiteWasStarted) {
            Start-Website -Name $SiteName
        }
    }
    else {
        # Create an HTTP website bound to all local IP addresses on the requested port.
        New-Website -Name $SiteName -PhysicalPath $resolvedInstallPath -Port $Port -IPAddress "*" -ApplicationPool $applicationPoolName |
            Out-Null
        $websiteCreated = $true

        Start-WebAppPool -Name $applicationPoolName
        Start-Website -Name $SiteName
    }

    Write-Host ""
    $completedAction = if ($isUpdate) { "update" } else { "installation" }
    Write-Host "IIS $completedAction completed successfully." -ForegroundColor Green
    Write-Host "Website:      $SiteName"
    Write-Host "App pool:     $applicationPoolName"
    Write-Host "Install path: $resolvedInstallPath"
    Write-Host "Binding:      http://localhost:$Port"
}
catch {
    $deploymentError = $_

    if ($isUpdate -and $updateDeploymentStarted) {
        try {
            if ((Get-Website -Name $SiteName).State -eq "Started") {
                Stop-Website -Name $SiteName
            }
            if ((Get-WebAppPoolState -Name $applicationPoolName).Value -eq "Started") {
                Stop-WebAppPool -Name $applicationPoolName
            }

            Restore-ApplicationBackup

            if ($applicationPoolWasStarted) {
                Start-WebAppPool -Name $applicationPoolName
            }
            if ($websiteWasStarted) {
                Start-Website -Name $SiteName
            }

            Write-Warning "The update failed and the previous application version was restored."
        }
        catch {
            throw "Update failed: $($deploymentError.Exception.Message) Rollback also failed: $($_.Exception.Message)"
        }

        throw $deploymentError
    }

    # Roll back only resources and files created by this installation attempt.
    if ($websiteCreated -and (Test-Path "IIS:\Sites\$SiteName")) {
        Remove-Website -Name $SiteName
    }

    if (
        $applicationPoolCreated -and
        (Test-Path "IIS:\AppPools\$applicationPoolName")
    ) {
        Remove-WebAppPool -Name $applicationPoolName
    }

    if ($installDirectoryCreated -and (Test-Path -LiteralPath $resolvedInstallPath)) {
        Remove-Item -LiteralPath $resolvedInstallPath -Recurse -Force
    }
    elseif (
        $installPathInitiallyExisted -and
        (Test-Path -LiteralPath $resolvedInstallPath)
    ) {
        Get-ChildItem -LiteralPath $resolvedInstallPath -Force |
            Remove-Item -Recurse -Force
    }

    throw
}
finally {
    # The expanded staging copy is never needed after installation.
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    if (Test-Path -LiteralPath $backupPath) {
        Remove-Item -LiteralPath $backupPath -Recurse -Force
    }
}
