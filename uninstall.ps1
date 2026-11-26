<#
.SYNOPSIS  Removes Data Kavach hosting (IIS site / Windows service, firewall rules). A final backup is taken first.
           Data, backups and the database are KEPT unless -RemoveData / -DropDatabase are given.
.EXAMPLE   .\uninstall.ps1
.EXAMPLE   .\uninstall.ps1 -RemoveData -DropDatabase    # complete removal (asks for confirmation)
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\DataKavach',
    [switch]$RemoveData,
    [switch]$DropDatabase,
    [switch]$SkipBackup,
    [switch]$Unattended,
    [SecureString]$SqlPassword
)
. (Join-Path $PSScriptRoot 'common.ps1')
try {
    Assert-Administrator
    $st = Get-InstallState $InstallDir
    if (-not $st) { throw "No installation found in $InstallDir" }
    if (-not (Confirm-Action "Uninstall $ProductName from ${InstallDir}?" -Unattended:$Unattended)) { throw 'Cancelled.' }
    $pw = ''
    if ($st.SqlUser) {
        if (-not $SqlPassword) { $SqlPassword = Read-Host "Password for SQL login $($st.SqlUser)" -AsSecureString }
        $pw = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword))
    }

    if (-not $SkipBackup) {
        Write-Step 'Final backup'
        $dir = Join-Path $InstallDir "backups\final_$(Get-Date -Format yyyyMMdd_HHmmss)"
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Grant-SqlServiceFolderAccess $st.SqlInstance $dir
        try { Write-Ok (Invoke-DatabaseBackup $st.SqlInstance $st.DatabaseName $dir $st.SqlUser $pw 'final') } catch { Write-Warn2 "Database backup failed: $($_.Exception.Message)" }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        try { [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $InstallDir 'data'), (Join-Path $dir 'files.zip')); Write-Ok 'Documents archived' } catch { Write-Warn2 $_.Exception.Message }
    }

    Write-Step 'Removing hosting'
    Stop-App $st.Mode
    if ($st.Mode -eq 'IIS') {
        Import-Module WebAdministration
        if (Test-Path "IIS:\Sites\$SiteName") { Remove-Website $SiteName; Write-Ok 'IIS site removed' }
        if (Test-Path "IIS:\AppPools\$PoolName") { Remove-WebAppPool $PoolName; Write-Ok 'Application pool removed' }
    } else {
        if (Get-Service $ServiceName -ErrorAction SilentlyContinue) { & sc.exe delete $ServiceName | Out-Null; Write-Ok 'Windows service removed' }
    }
    Get-NetFirewallRule -DisplayName 'Data Kavach TCP *' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Get-ScheduledTask -TaskName 'Data Kavach Daily Backup' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false
    Write-Ok 'Firewall rules and scheduled backup removed'
    Remove-Item (Join-Path $InstallDir 'app') -Recurse -Force -ErrorAction SilentlyContinue
    Write-Ok 'Application files removed'

    if ($DropDatabase -and (Confirm-Action "PERMANENTLY drop database [$($st.DatabaseName)]? A final backup was taken unless -SkipBackup was used." -Unattended:$Unattended)) {
        $d = $st.DatabaseName.Replace(']', ']]')
        Invoke-Sql (New-SqlConnectionString $st.SqlInstance 'master' $st.SqlUser $pw) "ALTER DATABASE [$d] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$d];" | Out-Null
        Write-Ok 'Database dropped'
    }
    if ($RemoveData -and (Confirm-Action "PERMANENTLY delete documents and evidence in $InstallDir\data?" -Unattended:$Unattended)) {
        Remove-Item (Join-Path $InstallDir 'data') -Recurse -Force
        Write-Ok 'Data folder removed (backups folder kept)'
    }
    Remove-Item (Join-Path $InstallDir 'install-state.json') -Force -ErrorAction SilentlyContinue
    Write-Host ""
    Write-Host "$ProductName uninstalled. Kept: $InstallDir\backups$(if (-not $RemoveData) { ", $InstallDir\data" })$(if (-not $DropDatabase) { ", database [$($st.DatabaseName)]" })." -ForegroundColor Green
    Write-Host 'Prerequisites (.NET, ODBC driver, SQL Server, LibreOffice, IIS) were left installed.'
}
catch { Write-Err2 $_.Exception.Message; exit 1 }
exit 0
