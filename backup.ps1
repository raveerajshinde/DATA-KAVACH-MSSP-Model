<#
.SYNOPSIS  Backs up the Data Kavach database, document storage and configuration.
.EXAMPLE   .\backup.ps1                      # one-off backup
.EXAMPLE   .\backup.ps1 -Schedule -At 01:30  # daily scheduled task (SYSTEM)
.EXAMPLE   .\backup.ps1 -RetentionDays 30
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\DataKavach',
    [string]$BackupDir = '',
    [int]$RetentionDays = 30,
    [switch]$Schedule,
    [string]$At = '01:30',
    [SecureString]$SqlPassword
)
. (Join-Path $PSScriptRoot 'common.ps1')
$log = Start-InstallLog $InstallDir 'backup'
try {
    Assert-Administrator
    $st = Get-InstallState $InstallDir
    if (-not $st) { throw "No installation found in $InstallDir" }
    if (-not $BackupDir) { $BackupDir = Join-Path $InstallDir 'backups' }

    if ($Schedule) {
        $script = Join-Path $PSScriptRoot 'backup.ps1'
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -InstallDir `"$InstallDir`" -BackupDir `"$BackupDir`" -RetentionDays $RetentionDays"
        $trigger = New-ScheduledTaskTrigger -Daily -At $At
        Register-ScheduledTask -TaskName 'Data Kavach Daily Backup' -Action $action -Trigger $trigger -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
        Write-Ok "Scheduled daily backup at $At (task 'Data Kavach Daily Backup', retention $RetentionDays days)"
        exit 0
    }

    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $set = Join-Path $BackupDir "set_$stamp"
    New-Item -ItemType Directory -Force -Path $set | Out-Null
    Grant-SqlServiceFolderAccess $st.SqlInstance $set
    $pw = ''
    if ($st.SqlUser) {
        if (-not $SqlPassword) { $SqlPassword = Read-Host "Password for SQL login $($st.SqlUser)" -AsSecureString }
        $pw = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword))
    }

    Write-Step 'Backing up database'
    $bak = Invoke-DatabaseBackup $st.SqlInstance $st.DatabaseName $set $st.SqlUser $pw 'full'
    Write-Ok $bak

    Write-Step 'Backing up documents, evidence and configuration'
    $data = Join-Path $InstallDir 'data'
    $zip = Join-Path $set "files_$stamp.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($data, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
    Copy-Item (Join-Path $InstallDir 'app\appsettings.Local.json') (Join-Path $set 'appsettings.Local.json') -Force
    Copy-Item (Join-Path $InstallDir 'install-state.json') (Join-Path $set 'install-state.json') -Force
    $manifest = @{ CreatedAt = (Get-Date).ToString('s'); Database = (Split-Path $bak -Leaf); Files = (Split-Path $zip -Leaf); Version = $st.Version
                   Sha256 = @{ db = (Get-FileHash $bak -Algorithm SHA256).Hash; files = (Get-FileHash $zip -Algorithm SHA256).Hash } }
    ($manifest | ConvertTo-Json -Depth 4) | Set-Content (Join-Path $set 'manifest.json') -Encoding UTF8
    Write-Ok "Backup set: $set"

    if ($RetentionDays -gt 0) {
        Get-ChildItem $BackupDir -Directory -Filter 'set_*' | Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-$RetentionDays) } | ForEach-Object { Remove-Item $_.FullName -Recurse -Force; Write-Ok "Removed old backup $($_.Name)" }
    }
    Write-Host "Note: the configuration copy contains the JWT signing key and DPAPI-protected keys are machine-bound. Store backup sets securely." -ForegroundColor Yellow
}
catch { Write-Err2 $_.Exception.Message; try { Stop-Transcript | Out-Null } catch { }; exit 1 }
try { Stop-Transcript | Out-Null } catch { }
exit 0
