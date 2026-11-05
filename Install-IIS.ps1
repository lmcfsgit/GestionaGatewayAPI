<#
.SYNOPSIS
Installs or updates a published Gestiona Gateway API ZIP as an IIS website.

.PARAMETER ZipPath
Path to the ZIP file containing the published application.

.PARAMETER Operation
Operation to perform. Defaults to Install and also accepts Update.

.PARAMETER Port
HTTP port to bind to the IIS website. Required only for installation.

.PARAMETER InstallPath
Directory where the published application will be installed. Required only for
installation; updates obtain it from the existing IIS website.

.PARAMETER SiteName
Name of the IIS website. Defaults to GestionaGatewayAPI. During installation,
the application pool is created with the same name. During an update, the
application pool assigned to the existing website is used.

.EXAMPLE
.\Install-IIS.ps1 -ZipPath C:\publish\GestionaGatewayAPI.zip -Port 8080 -InstallPath C:\inetpub\GestionaGatewayAPI

.EXAMPLE
.\Install-IIS.ps1 -ZipPath C:\publish\GestionaGatewayAPI.zip -Port 8081 -InstallPath C:\inetpub\GestionaGatewayAPI-Test -SiteName GestionaGatewayAPI-Test

.EXAMPLE
.\Install-IIS.ps1 -Operation Update -ZipPath C:\publish\GestionaGatewayAPI.zip -SiteName GestionaGatewayAPI

.NOTES
Update mode obtains the installation path, application pool, and HTTP port from
the existing website. It preserves logs, appsettings*.json, and web.config, and
restores the previous application if deployment fails.
#>

#requires -Version 4.0

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ZipPath,

    [Parameter()]
    [ValidateSet("Install", "Update")]
    [string]$Operation = "Install",

    [Parameter()]
    [ValidateRange(1, 65535)]
    [int]$Port,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$InstallPath,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^[^\\/:*?"<>|]+$')]
    [string]$SiteName = "GestionaGatewayAPI"
)

$ErrorActionPreference = "Stop"

$applicationName = "GestionaGatewayAPI"
$applicationPoolName = $null
$portWasSpecified = $PSBoundParameters.ContainsKey("Port")
$installPathWasSpecified = $PSBoundParameters.ContainsKey("InstallPath")

# Staging holds the extracted package; backup holds the currently deployed app
# long enough to restore it if an update fails.
$stagingPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "$SiteName-$([guid]::NewGuid().ToString('N'))"
$backupPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "$SiteName-backup-$([guid]::NewGuid().ToString('N'))"

# These flags record which resources this invocation owns and which service
# states must be restored. They prevent rollback from removing pre-existing IIS
# resources or starting services that were stopped before the update.
$websiteCreated = $false
$applicationPoolCreated = $false
$installDirectoryCreated = $false
$installPathInitiallyExisted = $false
$isUpdate = $Operation -eq "Update"
$updateDeploymentStarted = $false
$updateShutdownStarted = $false
$preserveBackup = $false
$backupCreated = $false
$websiteWasStarted = $false
$applicationPoolWasStarted = $false

function Invoke-IisInstallation {
    if (-not (Test-IsAdministrator)) {
        throw "Run this script from an elevated PowerShell session."
    }

    $resolvedZipPath = (Resolve-Path -LiteralPath $ZipPath).Path
    if ([System.IO.Path]::GetExtension($resolvedZipPath) -ne ".zip") {
        throw "ZipPath must point to a .zip file: $resolvedZipPath"
    }

    if ($isUpdate) {
        if ($portWasSpecified -or $installPathWasSpecified) {
            throw "Port and InstallPath must not be supplied for an update; they are read from the existing IIS website."
        }
    }
    else {
        if (-not $portWasSpecified) {
            throw "Port is required for an installation."
        }
        if (-not $installPathWasSpecified) {
            throw "InstallPath is required for an installation."
        }

        $resolvedInstallPath = [System.IO.Path]::GetFullPath($InstallPath)
        $installRoot = [System.IO.Path]::GetPathRoot($resolvedInstallPath)
        if ($resolvedInstallPath.TrimEnd("\") -eq $installRoot.TrimEnd("\")) {
            throw "InstallPath cannot be the root of a drive."
        }
    }

    # Load the IIS provider and management cmdlets.
    Import-Module WebAdministration -ErrorAction Stop

    if ($isUpdate) {
        Initialize-IisAdministrationAssembly
    }

    # ASP.NET Core applications hosted in IIS require the Hosting Bundle module.
    $aspNetCoreModule = Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue
    if ($null -eq $aspNetCoreModule) {
        $missingModuleMessage =
            "ASP.NET Core Module V2 is not installed in IIS." +
            [Environment]::NewLine +
            "Install the .NET 8 Hosting Bundle, restart IIS, and run this script again."
        throw $missingModuleMessage
    }

    if ($isUpdate) {
        if (-not (Test-Path "IIS:\Sites\$SiteName")) {
            throw "Website '$SiteName' does not exist and cannot be updated."
        }

        # Derive deployment details from IIS so update callers only identify the site.
        $existingWebsite = Get-Item "IIS:\Sites\$SiteName"
        $applicationPoolName = $existingWebsite.applicationPool
        if ([string]::IsNullOrWhiteSpace($applicationPoolName) -or
            -not (Test-Path "IIS:\AppPools\$applicationPoolName")) {
            throw "The application pool assigned to website '$SiteName' does not exist."
        }

        $existingPhysicalPath = [Environment]::ExpandEnvironmentVariables(
            $existingWebsite.physicalPath)
        $resolvedInstallPath = [System.IO.Path]::GetFullPath(
            $existingPhysicalPath)

        if (-not (Test-Path -LiteralPath $resolvedInstallPath)) {
            throw "The existing website installation path does not exist: $resolvedInstallPath"
        }

        $httpBinding = Get-WebBinding -Name $SiteName -Protocol "http" |
            Select-Object -First 1
        if ($null -eq $httpBinding -or
            $httpBinding.bindingInformation -notmatch ':(?<Port>\d+):[^:]*$') {
            throw "Existing website '$SiteName' does not have a valid HTTP binding."
        }
        $Port = [int]$Matches.Port
    }
    else {
        $applicationPoolName = $SiteName
        if (Test-Path "IIS:\Sites\$SiteName") {
            throw "Website '$SiteName' already exists. Use -Operation Update to update it."
        }
        if (Test-Path "IIS:\AppPools\$applicationPoolName") {
            throw "Application pool '$applicationPoolName' already exists."
        }

        # New installations refuse to replace an existing binding or destination files.
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

    try {
        # Extract to a unique staging directory and locate the published app root.
        New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($resolvedZipPath, $stagingPath)

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
            # Back up everything except logs, which remain in place throughout the
            # update and may continue to contain operational history.
            New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
            Get-ChildItem -LiteralPath $resolvedInstallPath -Force |
                Where-Object { $_.Name -ne "logs" } |
                Copy-Item -Destination $backupPath -Recurse -Force
            $backupCreated = $true

            $websiteWasStarted =
                (Get-Website -Name $SiteName).State -eq "Started"
            $applicationPoolWasStarted =
                (Get-WebAppPoolState -Name $applicationPoolName).Value -eq "Started"

            # Record the original states before stopping IIS so they can be restored
            # after either a successful deployment or a rollback.
            $updateShutdownStarted = $true
            if ($websiteWasStarted) {
                Stop-Website -Name $SiteName
            }
            Stop-ApplicationPoolAndWait -Name $applicationPoolName

            $updateDeploymentStarted = $true
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
            # Do not change the operator's original running/stopped state.
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

        if ($isUpdate -and $updateShutdownStarted -and -not $updateDeploymentStarted) {
            # Shutdown failed before any application files were replaced.
            if ($websiteWasStarted) {
                Start-Website -Name $SiteName
            }
            throw $deploymentError
        }

        # Once an update has started replacing files, recover the previous snapshot
        # and service states before returning the original deployment error.
        if ($isUpdate -and $updateDeploymentStarted) {
            try {
                if ((Get-Website -Name $SiteName).State -eq "Started") {
                    Stop-Website -Name $SiteName
                }
                Stop-ApplicationPoolAndWait -Name $applicationPoolName

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
                $preserveBackup = $true
                throw "Update failed: $($deploymentError.Exception.Message) Rollback also failed: $($_.Exception.Message) Backup retained at: $backupPath"
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
        # Keep the backup for manual recovery if rollback failed.
        if (Test-Path -LiteralPath $stagingPath) {
            Remove-Item -LiteralPath $stagingPath -Recurse -Force
        }
        if (-not $preserveBackup -and (Test-Path -LiteralPath $backupPath)) {
            Remove-Item -LiteralPath $backupPath -Recurse -Force
        }
    }
}

function Initialize-IisAdministrationAssembly {
    if ("Microsoft.Web.Administration.ServerManager" -as [type]) {
        return
    }

    try {
        Add-Type -AssemblyName Microsoft.Web.Administration -ErrorAction Stop
    }
    catch {
        $assemblyPath = Join-Path $env:windir "System32\inetsrv\Microsoft.Web.Administration.dll"
        if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) {
            $assemblyPath = Join-Path $env:windir "Sysnative\inetsrv\Microsoft.Web.Administration.dll"
        }
        if (-not (Test-Path -LiteralPath $assemblyPath)) {
            throw "Cannot find the IIS management assembly at '$assemblyPath'. Install IIS Management Scripts and Tools and run this script in Windows PowerShell."
        }
        Add-Type -Path $assemblyPath -ErrorAction Stop
    }

    if (-not ("Microsoft.Web.Administration.ServerManager" -as [type])) {
        throw "The IIS management assembly was loaded, but ServerManager is unavailable. Run this script in Windows PowerShell with IIS Management Scripts and Tools installed."
    }
}

# Wait for IIS state and worker exit before touching deployed files.
function Stop-ApplicationPoolAndWait {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [ValidateRange(1, 3600)]
        [int]$TimeoutSeconds = 90
    )

    $workerIds = @{}
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $stopRequested = $false
    while ($true) {
        $manager = New-Object Microsoft.Web.Administration.ServerManager
        try {
            $workers = @($manager.WorkerProcesses | Where-Object { $_.AppPoolName -eq $Name })
            foreach ($worker in $workers) {
                $workerIds[[int]$worker.ProcessId] = $true
            }
        }
        finally {
            $manager.Dispose()
        }

        $state = (Get-WebAppPoolState -Name $Name).Value
        if (-not $stopRequested -and $state -ne "Stopped" -and $state -ne "Stopping") {
            Stop-WebAppPool -Name $Name
            $stopRequested = $true
        }

        $remainingWorkers = @($workerIds.Keys | ForEach-Object {
            Get-Process -Id $_ -ErrorAction SilentlyContinue
        })
        if ($state -eq "Stopped" -and $workers.Count -eq 0 -and $remainingWorkers.Count -eq 0) {
            return
        }

        if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "Timed out after $TimeoutSeconds seconds waiting for application pool '$Name' and its worker processes to stop. No files will be replaced by this operation. Pool state: $state. Investigate IIS shutdown before retrying."
        }

        Start-Sleep -Milliseconds 500
    }
}

# IIS configuration requires an elevated PowerShell session.
function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object -TypeName Security.Principal.WindowsPrincipal -ArgumentList $identity
    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Copy the children rather than the source directory itself, so the published
# files are placed directly in the installation root.
function Copy-ApplicationContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    Get-ChildItem -LiteralPath $Source -Force |
        Copy-Item -Destination $Destination -Recurse -Force
}

# Preserve logs while removing all replaceable application content.
function Remove-ApplicationContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Get-ChildItem -LiteralPath $Path -Force |
        Where-Object { $_.Name -ne "logs" } |
        Remove-Item -Recurse -Force
}

# Restore the complete pre-update snapshot if deployment does not finish.
function Restore-ApplicationBackup {
    if (-not $backupCreated) {
        return
    }

    Remove-ApplicationContent -Path $resolvedInstallPath
    Copy-ApplicationContent -Source $backupPath -Destination $resolvedInstallPath
}

Invoke-IisInstallation
