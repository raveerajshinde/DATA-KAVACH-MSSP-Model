<#
.SYNOPSIS  Repairs a Data Kavach installation: re-checks prerequisites, permissions, hosting, migrations and health.
.EXAMPLE   .\repair.ps1
.EXAMPLE   .\repair.ps1 -InstallLibreOffice
.EXAMPLE   .\repair.ps1 -ResetAdminEmail admin@corp.in   # sets a new temporary password for a locked-out administrator
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\DataKavach',
    [switch]$InstallLibreOffice,
    [string]$ResetAdminEmail = '',
    [switch]$Unattended
)
. (Join-Path $PSScriptRoot 'common.ps1')
$log = Start-InstallLog $InstallDir 'repair'
try {
    Assert-Administrator
    $st = Get-InstallState $InstallDir
    if (-not $st) { throw "No installation found in $InstallDir. Use install.ps1." }
    $appDir = Join-Path $InstallDir 'app'
    $identity = Get-AppIdentity $st.Mode

    Write-Step 'Checking prerequisites'
    if (Get-AspNetRuntimeVersion) { Write-Ok "ASP.NET Core runtime $(Get-AspNetRuntimeVersion)" } else { Write-Err2 'ASP.NET Core 10 runtime missing - re-run install.ps1' }
    if ($st.Mode -eq 'IIS') { if (Test-HostingBundle) { Write-Ok 'Hosting bundle present' } else { Write-Err2 'ASP.NET Core Module missing - re-run install.ps1' } }
    $odbc = Get-OdbcSqlDriver; if ($odbc) { Write-Ok $odbc } else { Write-Err2 'ODBC driver missing - re-run install.ps1' }

    $lo = Get-LibreOfficePath
    if (-not $lo -and $InstallLibreOffice) {
        $done = Invoke-OfflinePrereq 'LibreOffice*.msi' '/qn /norestart ALLUSERS=1 CREATEDESKTOPLINK=0 REGISTER_ALL_MSO_TYPES=0 ISCHECKFORPRODUCTUPDATES=0' 'LibreOffice'
        if (-not $done) { Invoke-Winget 'TheDocumentFoundation.LibreOffice' 'LibreOffice' }
        $lo = Get-LibreOfficePath
    }
    if ($lo) {
        Write-Ok "LibreOffice: $lo"
        $cfgPath = Join-Path $appDir 'appsettings.Local.json'
        $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
        if ($cfg.Pdf.SofficePath -ne $lo) { $cfg.Pdf.SofficePath = $lo; ($cfg | ConvertTo-Json -Depth 8) | Set-Content $cfgPath -Encoding UTF8; Write-Ok 'PDF engine path updated in settings' }
    } else { Write-Warn2 'LibreOffice not installed - PDF export unavailable (use -InstallLibreOffice).' }

    Write-Step 'Re-applying folder permissions'
    foreach ($d in @('data', 'logs', 'backups')) { Set-FolderAcl (Join-Path $InstallDir $d) $identity 'Modify' }
    Set-FolderAcl $appDir $identity 'ReadAndExecute'
    Grant-SqlServiceFolderAccess $st.SqlInstance (Join-Path $InstallDir 'backups')
    Write-Ok "Permissions for $identity"

    Write-Step 'Checking hosting'
    Stop-App $st.Mode
    if ($st.Mode -eq 'IIS') {
        Import-Module WebAdministration
        if (-not (Test-Path "IIS:\AppPools\$PoolName")) { throw "IIS application pool missing - re-run install.ps1 to recreate the site." }
        if (-not (Test-Path "IIS:\Sites\$SiteName")) { throw "IIS site missing - re-run install.ps1 to recreate the site." }
        Write-Ok 'IIS site and application pool present'
    } else {
        if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) { throw 'Windows service missing - re-run install.ps1 -Mode Service.' }
        Write-Ok 'Windows service present'
    }
    if (-not $st.SqlUser) { try { Grant-SqlAccess $st.SqlInstance $st.DatabaseName $identity '' ''; Write-Ok 'SQL access re-applied' } catch { Write-Warn2 $_.Exception.Message } }

    Write-Step 'Applying database migrations / re-seeding reference data'
    Invoke-AppCli $appDir @('--migrate')

    if ($ResetAdminEmail) {
        Write-Step "Resetting password for $ResetAdminEmail"
        $p = Read-Host 'New temporary password' -AsSecureString
        Invoke-AppCli $appDir @('--set-password', $ResetAdminEmail, [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($p)))
    }

    Write-Step 'Starting and health-checking'
    Start-App $st.Mode
    $h = Wait-Health "http://localhost:$($st.HttpPort)" 120
    if (-not $h) { throw 'Application is not healthy. See logs and the Windows Application event log.' }
    Invoke-AppCli $appDir @('--health')
    Write-Ok "Repair complete - schema v$($h.schema), PDF $(if ($h.pdf) { 'available' } else { 'unavailable' })"
}
catch { Write-Err2 $_.Exception.Message; try { Stop-Transcript | Out-Null } catch { }; exit 1 }
try { Stop-Transcript | Out-Null } catch { }
exit 0
