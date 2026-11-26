# Data Kavach - shared installer functions (Windows PowerShell 5.1 compatible)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Script:ProductName = 'Data Kavach'
$Script:ServiceName = 'DataKavach'
$Script:SiteName    = 'DataKavach'
$Script:PoolName    = 'DataKavach'
$Script:PackageRoot = $PSScriptRoot

function Write-Step([string]$msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "    [OK] $msg" -ForegroundColor Green }
function Write-Warn2([string]$msg){ Write-Host "    [!]  $msg" -ForegroundColor Yellow }
function Write-Err2([string]$msg) { Write-Host "    [X]  $msg" -ForegroundColor Red }

function Start-InstallLog([string]$installDir, [string]$name) {
    $logDir = Join-Path $installDir 'logs'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $log = Join-Path $logDir ("{0}_{1:yyyyMMdd_HHmmss}.log" -f $name, (Get-Date))
    try { Start-Transcript -Path $log -Append | Out-Null } catch { }
    return $log
}

function Assert-Administrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Administrator rights are required. Right-click INSTALL.bat (or EnterpriseDPDP_Setup.exe) and choose 'Run as administrator'."
    }
    Write-Ok "Running as administrator ($($id.Name))"
}

function Confirm-Action([string]$question, [switch]$Unattended) {
    if ($Unattended) { return $true }
    $a = Read-Host "$question [Y/n]"
    return ($a -eq '' -or $a -match '^(y|yes)$')
}

function Test-Command([string]$name) { return [bool](Get-Command $name -ErrorAction SilentlyContinue) }

function Get-AspNetRuntimeVersion {
    if (-not (Test-Command 'dotnet')) { return $null }
    $rt = & dotnet --list-runtimes 2>$null | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 10\.' }
    if ($rt) { return ($rt | Select-Object -Last 1).Split(' ')[1] }
    return $null
}

function Test-HostingBundle {
    $p = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'
    return (Test-Path $p)
}

function Get-OdbcSqlDriver {
    $drivers = @()
    try { $drivers = (Get-OdbcDriver -Platform '64-bit' -ErrorAction Stop | Select-Object -ExpandProperty Name) } catch {
        $key = 'HKLM:\SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers'
        if (Test-Path $key) { $drivers = (Get-Item $key).Property }
    }
    foreach ($d in @('ODBC Driver 18 for SQL Server', 'ODBC Driver 17 for SQL Server')) { if ($drivers -contains $d) { return $d } }
    return $null
}

function Get-LibreOfficePath {
    foreach ($p in @("$env:ProgramFiles\LibreOffice\program\soffice.exe", "${env:ProgramFiles(x86)}\LibreOffice\program\soffice.exe")) { if ($p -and (Test-Path $p)) { return $p } }
    return $null
}

function Test-Winget { return (Test-Command 'winget') }

function Invoke-Winget([string]$id, [string]$label) {
    if (-not (Test-Winget)) { throw "winget is not available. Install '$label' manually, or place its installer in the 'prereqs' folder and re-run." }
    Write-Host "    Installing $label via winget ($id)..."
    & winget install --id $id --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne -1978335189) { throw "winget failed to install $label (exit $LASTEXITCODE)." }   # -1978335189 = already installed
}

function Invoke-OfflinePrereq([string]$pattern, [string]$arguments, [string]$label) {
    $dir = Join-Path $Script:PackageRoot 'prereqs'
    if (-not (Test-Path $dir)) { return $false }
    $f = Get-ChildItem -Path $dir -Filter $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $f) { return $false }
    Write-Host "    Installing $label from $($f.Name)..."
    if ($f.Extension -eq '.msi') { $p = Start-Process msiexec.exe -ArgumentList "/i `"$($f.FullName)`" $arguments" -Wait -PassThru }
    else { $p = Start-Process $f.FullName -ArgumentList $arguments -Wait -PassThru }
    if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) { throw "$label installer exited with code $($p.ExitCode)" }
    return $true
}

function Update-SessionPath {
    $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}

# ------------------------------------------------------------------ SQL Server (System.Data.SqlClient ships with Windows PowerShell 5.1)
function New-SqlConnectionString([string]$instance, [string]$database, [string]$user, [string]$password) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $instance
    $b['Initial Catalog'] = $database
    if ($user) { $b['User ID'] = $user; $b['Password'] = $password } else { $b['Integrated Security'] = $true }
    $b['Connect Timeout'] = 15
    return $b.ConnectionString
}

function Invoke-Sql([string]$connectionString, [string]$sql, [int]$timeout = 600) {
    $c = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $c.Open()
        $cmd = $c.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = $timeout
        return $cmd.ExecuteScalar()
    } finally { $c.Dispose() }
}

function Test-SqlConnection([string]$instance, [string]$user, [string]$password) {
    try { Invoke-Sql (New-SqlConnectionString $instance 'master' $user $password) 'SELECT @@VERSION' | Out-Null; return $true } catch { Write-Warn2 $_.Exception.Message; return $false }
}

function Get-OdbcConnectionString([string]$driver, [string]$instance, [string]$database, [string]$user, [string]$password) {
    $cs = "Driver={$driver};Server=$instance;Database=$database;"
    if ($user) { $cs += "UID=$user;PWD={$($password -replace '}', '}}')};" } else { $cs += 'Trusted_Connection=Yes;' }
    if ($driver -like '*18*' -or $driver -like '*17*') { $cs += 'Encrypt=Yes;TrustServerCertificate=Yes;' }
    return $cs
}

function Grant-SqlAccess([string]$instance, [string]$database, [string]$login, [string]$adminUser, [string]$adminPassword) {
    $master = New-SqlConnectionString $instance 'master' $adminUser $adminPassword
    $q = $login.Replace("'", "''"); $b = $login.Replace(']', ']]')
    Invoke-Sql $master "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$q') CREATE LOGIN [$b] FROM WINDOWS WITH DEFAULT_DATABASE=[$database];" | Out-Null
    $db = New-SqlConnectionString $instance $database $adminUser $adminPassword
    Invoke-Sql $db "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$q') CREATE USER [$b] FOR LOGIN [$b]; ALTER ROLE db_owner ADD MEMBER [$b]; ALTER ROLE db_backupoperator ADD MEMBER [$b];" | Out-Null
}

# ------------------------------------------------------------------ files & config
function New-RandomSecret([int]$bytes = 48) {
    $b = New-Object byte[] $bytes
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    return [Convert]::ToBase64String($b)
}

function Set-FolderAcl([string]$path, [string]$identity, [string]$rights = 'Modify') {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    $acl = Get-Acl $path
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, $rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.SetAccessRule($rule)
    Set-Acl -Path $path -AclObject $acl
}

function Read-JsonFile([string]$path) { if (Test-Path $path) { return (Get-Content $path -Raw | ConvertFrom-Json) } return $null }

function Write-LocalSettings([string]$appDir, [hashtable]$s) {
    $path = Join-Path $appDir 'appsettings.Local.json'
    $json = [ordered]@{
        Database = [ordered]@{ Provider = 'SqlServer'; ConnectionString = $s.ConnectionString; AutoMigrate = $true }
        Jwt      = [ordered]@{ Issuer = 'DataKavach'; SigningKey = $s.JwtKey; SessionMinutes = $s.SessionMinutes }
        Storage  = [ordered]@{ Root = $s.StorageRoot; MaxUploadBytes = 52428800 }
        Pdf      = [ordered]@{ SofficePath = $s.SofficePath; PythonPath = ''; TimeoutSeconds = 180 }
        Backup   = [ordered]@{ Directory = $s.BackupDir }
        Https    = [ordered]@{ Redirect = $s.HttpsRedirect }
    }
    if ($s.ContainsKey('Kestrel') -and $s.Kestrel) { $json['Kestrel'] = $s.Kestrel }
    ($json | ConvertTo-Json -Depth 8) | Set-Content -Path $path -Encoding UTF8
    # restrict the settings file (contains the signing key and possibly SQL credentials)
    $acl = Get-Acl $path
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($id in @('BUILTIN\Administrators', 'NT AUTHORITY\SYSTEM', $s.AppIdentity)) {
        if ($id) { $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($id, $(if ($id -eq $s.AppIdentity) { 'Read' } else { 'FullControl' }), 'Allow'))) }
    }
    Set-Acl -Path $path -AclObject $acl
}

function Invoke-AppCli([string]$appDir, [string[]]$cliArgs) {
    Push-Location $appDir
    try {
        & dotnet (Join-Path $appDir 'DataKavach.Api.dll') @cliArgs
        if ($LASTEXITCODE -ne 0) { throw "Application command '$($cliArgs[0])' failed (exit $LASTEXITCODE)." }
    } finally { Pop-Location }
}

# ------------------------------------------------------------------ hosting
function Get-AppIdentity([string]$mode) { if ($mode -eq 'Service') { return "NT SERVICE\$Script:ServiceName" } return "IIS APPPOOL\$Script:PoolName" }

function Stop-App([string]$mode) {
    if ($mode -eq 'Service') {
        $s = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
        if ($s -and $s.Status -ne 'Stopped') { Stop-Service $Script:ServiceName -Force; $s.WaitForStatus('Stopped', '00:01:00') }
    } else {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        if (Test-Path "IIS:\AppPools\$Script:PoolName") { if ((Get-WebAppPoolState $Script:PoolName).Value -ne 'Stopped') { Stop-WebAppPool $Script:PoolName } }
        if (Test-Path "IIS:\Sites\$Script:SiteName") { Stop-Website $Script:SiteName -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 3
    }
}

function Start-App([string]$mode) {
    if ($mode -eq 'Service') { Start-Service $Script:ServiceName }
    else {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        Start-WebAppPool $Script:PoolName -ErrorAction SilentlyContinue
        Start-Website $Script:SiteName -ErrorAction SilentlyContinue
    }
}

function Get-InstallState([string]$installDir) {
    $p = Join-Path $installDir 'install-state.json'
    return Read-JsonFile $p
}

function Save-InstallState([string]$installDir, [hashtable]$state) {
    ($state | ConvertTo-Json -Depth 5) | Set-Content -Path (Join-Path $installDir 'install-state.json') -Encoding UTF8
}

function Wait-Health([string]$url, [int]$seconds = 90) {
    $deadline = (Get-Date).AddSeconds($seconds)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    if (-not ([System.Management.Automation.PSTypeName]'DkTrustAll').Type) {
        Add-Type -TypeDefinition 'using System.Net; using System.Security.Cryptography.X509Certificates; public class DkTrustAll : ICertificatePolicy { public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; } }'
    }
    [Net.ServicePointManager]::CertificatePolicy = New-Object DkTrustAll
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-RestMethod -Uri "$url/api/health" -TimeoutSec 10
            if ($r.status -eq 'ok') { return $r }
        } catch { Start-Sleep -Seconds 3 }
    }
    return $null
}

function Invoke-DatabaseBackup([string]$instance, [string]$database, [string]$targetDir, [string]$user, [string]$password, [string]$label = 'backup') {
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    $file = Join-Path $targetDir ("{0}_{1}_{2:yyyyMMdd_HHmmss}.bak" -f $database, $label, (Get-Date))
    $master = New-SqlConnectionString $instance 'master' $user $password
    $f = $file.Replace("'", "''")
    Invoke-Sql $master "BACKUP DATABASE [$($database.Replace(']', ']]'))] TO DISK = N'$f' WITH INIT, CHECKSUM, NAME = N'Data Kavach $label'" 1800 | Out-Null
    return $file
}

function Invoke-DatabaseRestore([string]$instance, [string]$database, [string]$bakFile, [string]$user, [string]$password) {
    $master = New-SqlConnectionString $instance 'master' $user $password
    $f = $bakFile.Replace("'", "''"); $d = $database.Replace(']', ']]')
    Invoke-Sql $master "IF DB_ID(N'$($database.Replace("'", "''"))') IS NOT NULL ALTER DATABASE [$d] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$d] FROM DISK = N'$f' WITH REPLACE, CHECKSUM; ALTER DATABASE [$d] SET MULTI_USER;" 3600 | Out-Null
}

function Grant-SqlServiceFolderAccess([string]$instance, [string]$folder) {
    # SQL Server writes .bak files with its own service account - grant it rights on the backup folder
    $svc = if ($instance -match '\\(.+)$') { "MSSQL`$$($Matches[1])" } else { 'MSSQLSERVER' }
    try { Set-FolderAcl $folder "NT SERVICE\$svc" 'Modify'; Write-Ok "SQL Server service ($svc) can write to $folder" }
    catch { Write-Warn2 "Could not grant SQL Server service rights on $folder ($($_.Exception.Message)). Database backups may fail if SQL Server runs under a different account." }
}
