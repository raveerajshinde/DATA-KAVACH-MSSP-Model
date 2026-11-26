<#
.SYNOPSIS  Restores a Data Kavach backup set created by backup.ps1 (database + documents + configuration).
.EXAMPLE   .\restore.ps1 -BackupSet C:\DataKavach\backups\set_20261126_013000
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BackupSet,
    [string]$InstallDir = 'C:\DataKavach',
    [switch]$SkipFiles,
    [switch]$Force,
    [SecureString]$SqlPassword
)
. (Join-Path $PSScriptRoot 'common.ps1')
$log = Start-InstallLog $InstallDir 'restore'
try {
    Assert-Administrator
    $st = Get-InstallState $InstallDir
    if (-not $st) { throw "No installation found in $InstallDir. Run install.ps1 first, then restore." }
    $m = Read-JsonFile (Join-Path $BackupSet 'manifest.json')
    if (-not $m) { throw "manifest.json not found in $BackupSet" }
    $bak = Join-Path $BackupSet $m.Database
    $zip = Join-Path $BackupSet $m.Files
    Write-Step 'Verifying backup integrity'
    if ((Get-FileHash $bak -Algorithm SHA256).Hash -ne $m.Sha256.db) { throw 'Database backup checksum mismatch - backup is corrupt or altered.' }
    if (-not $SkipFiles -and (Get-FileHash $zip -Algorithm SHA256).Hash -ne $m.Sha256.files) { throw 'Files archive checksum mismatch.' }
    Write-Ok "Backup from $($m.CreatedAt) verified"
    if (-not $Force -and -not (Confirm-Action "Restore will REPLACE the current database and documents with the backup from $($m.CreatedAt). Continue?")) { throw 'Cancelled by user.' }

    $pw = ''
    if ($st.SqlUser) {
        if (-not $SqlPassword) { $SqlPassword = Read-Host "Password for SQL login $($st.SqlUser)" -AsSecureString }
        $pw = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword))
    }

    Write-Step 'Stopping application'
    Stop-App $st.Mode

    Write-Step 'Taking safety backup of current state'
    $safety = Join-Path $InstallDir "backups\pre-restore_$(Get-Date -Format yyyyMMdd_HHmmss)"
    New-Item -ItemType Directory -Force -Path $safety | Out-Null
    Grant-SqlServiceFolderAccess $st.SqlInstance $safety
    try { $sb = Invoke-DatabaseBackup $st.SqlInstance $st.DatabaseName $safety $st.SqlUser $pw 'pre-restore'; Write-Ok $sb } catch { Write-Warn2 "Safety backup failed: $($_.Exception.Message)" }

    Write-Step 'Restoring database'
    Invoke-DatabaseRestore $st.SqlInstance $st.DatabaseName $bak $st.SqlUser $pw
    Write-Ok 'Database restored'
    if (-not $st.SqlUser) { try { Grant-SqlAccess $st.SqlInstance $st.DatabaseName (Get-AppIdentity $st.Mode) '' ''; Write-Ok 'Application database access re-applied' } catch { Write-Warn2 $_.Exception.Message } }

    if (-not $SkipFiles) {
        Write-Step 'Restoring documents and evidence'
        $data = Join-Path $InstallDir 'data'
        if (Test-Path $data) { Rename-Item $data "data.pre-restore_$(Get-Date -Format yyyyMMdd_HHmmss)" }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $data)
        Set-FolderAcl $data (Get-AppIdentity $st.Mode) 'Modify'
        Write-Ok "Files restored to $data (previous copy renamed)"
    }

    Write-Step 'Starting application and applying any pending migrations'
    Invoke-AppCli (Join-Path $InstallDir 'app') @('--migrate')
    Start-App $st.Mode
    $h = Wait-Health "http://localhost:$($st.HttpPort)" 120
    if (-not $h) { throw 'Application did not become healthy after restore. Check logs.' }
    Write-Ok "Restore complete - schema v$($h.schema)"
}
catch { Write-Err2 $_.Exception.Message; try { Stop-Transcript | Out-Null } catch { }; exit 1 }
try { Stop-Transcript | Out-Null } catch { }
exit 0
