<#
.SYNOPSIS  Upgrades an existing Data Kavach installation from this package, preserving database, documents and settings.
           Takes a full backup first and rolls back application files (and optionally the database) on failure.
.EXAMPLE   .\upgrade.ps1
.EXAMPLE   .\upgrade.ps1 -Rollback      # restore the application files and database from the last upgrade backup
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\DataKavach',
    [switch]$Rollback,
    [switch]$Unattended,
    [SecureString]$SqlPassword
)
. (Join-Path $PSScriptRoot 'common.ps1')
$log = Start-InstallLog $InstallDir 'upgrade'
$appDir = Join-Path $InstallDir 'app'
$rbRoot = Join-Path $InstallDir 'backups\upgrade'

function Restore-AppFiles([string]$from) {
    Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($from, $appDir)
}

try {
    Assert-Administrator
    $st = Get-InstallState $InstallDir
    if (-not $st) { throw "No installation found in $InstallDir. Use install.ps1." }
    $pw = ''
    if ($st.SqlUser) {
        if (-not $SqlPassword) { $SqlPassword = Read-Host "Password for SQL login $($st.SqlUser)" -AsSecureString }
        $pw = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword))
    }

    if ($Rollback) {
        $last = Get-ChildItem $rbRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
        if (-not $last) { throw 'No upgrade backup found to roll back to.' }
        if (-not (Confirm-Action "Roll back to the state saved at $($last.Name) (application files AND database)?" -Unattended:$Unattended)) { throw 'Cancelled.' }
        Stop-App $st.Mode
        Restore-AppFiles (Join-Path $last.FullName 'app.zip')
        $bak = Get-ChildItem $last.FullName -Filter '*.bak' | Select-Object -First 1
        if ($bak) { Invoke-DatabaseRestore $st.SqlInstance $st.DatabaseName $bak.FullName $st.SqlUser $pw; Write-Ok 'Database restored' }
        Set-FolderAcl $appDir (Get-AppIdentity $st.Mode) 'ReadAndExecute'
        Start-App $st.Mode
        if (Wait-Health "http://localhost:$($st.HttpPort)" 120) { Write-Ok "Rolled back to $($last.Name)" } else { throw 'Application not healthy after rollback.' }
        exit 0
    }

    $newVer = (Get-Item (Join-Path $PSScriptRoot 'app\DataKavach.Api.dll')).VersionInfo.FileVersion
    Write-Step "Upgrading $($st.Version) -> $newVer"

    Write-Step 'Creating pre-upgrade backup (application files + database)'
    $rb = Join-Path $rbRoot (Get-Date -Format 'yyyyMMdd_HHmmss')
    New-Item -ItemType Directory -Force -Path $rb | Out-Null
    Grant-SqlServiceFolderAccess $st.SqlInstance $rb
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($appDir, (Join-Path $rb 'app.zip'))
    $bakFile = Invoke-DatabaseBackup $st.SqlInstance $st.DatabaseName $rb $st.SqlUser $pw 'pre-upgrade'
    Write-Ok "Saved to $rb"

    Write-Step 'Stopping application'
    Stop-App $st.Mode

    try {
        Write-Step 'Deploying new application files (settings and data preserved)'
        $keep = @('appsettings.Local.json', 'web.config')
        Get-ChildItem $appDir -Force | Where-Object { $keep -notcontains $_.Name } | Remove-Item -Recurse -Force
        Get-ChildItem (Join-Path $PSScriptRoot 'app') -Force | Where-Object { $keep -notcontains $_.Name } | ForEach-Object { Copy-Item $_.FullName -Destination $appDir -Recurse -Force }
        Set-FolderAcl $appDir (Get-AppIdentity $st.Mode) 'ReadAndExecute'

        Write-Step 'Applying database migrations'
        Invoke-AppCli $appDir @('--migrate')

        Write-Step 'Starting and health-checking'
        Start-App $st.Mode
        $h = Wait-Health "http://localhost:$($st.HttpPort)" 120
        if (-not $h) { throw 'Health check failed after upgrade.' }
        $st.Version = $newVer
        $ht = @{}; $st.PSObject.Properties | ForEach-Object { $ht[$_.Name] = $_.Value }; $ht['UpgradedAt'] = (Get-Date).ToString('s')
        Save-InstallState $InstallDir $ht
        Write-Ok "Upgrade complete - version $newVer, schema v$($h.schema)"
    }
    catch {
        Write-Err2 "Upgrade failed: $($_.Exception.Message) - rolling back application files"
        Stop-App $st.Mode
        Restore-AppFiles (Join-Path $rb 'app.zip')
        Set-FolderAcl $appDir (Get-AppIdentity $st.Mode) 'ReadAndExecute'
        if (Confirm-Action 'Also restore the database from the pre-upgrade backup (needed if migrations were partially applied)?' -Unattended:$Unattended) {
            Invoke-DatabaseRestore $st.SqlInstance $st.DatabaseName $bakFile $st.SqlUser $pw
            Write-Ok 'Database restored'
        }
        Start-App $st.Mode
        throw
    }
}
catch { Write-Err2 $_.Exception.Message; try { Stop-Transcript | Out-Null } catch { }; exit 1 }
try { Stop-Transcript | Out-Null } catch { }
exit 0
