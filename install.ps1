<#
.SYNOPSIS
  Data Kavach - DPDP Compliance Management Platform installer for Windows (Server 2019+/Windows 10+).

.DESCRIPTION
  1. Checks administrator rights          9. Configures document storage
  2. Detects required software           10. Configures authentication (JWT signing key)
  3. Installs missing dependencies       11. Configures application service (IIS or Windows service)
  4. Configures SQL Server / connects    12. Configures firewall and HTTPS
  5. Creates the database                13. Runs health checks
  6. Applies database migrations         14. Creates the initial administrator
  7. Configures the backend              15. Opens the portal
  8. Deploys the frontend

  Re-running install.ps1 on an existing installation is safe: data, database and settings are preserved.

.EXAMPLE
  .\install.ps1
  .\install.ps1 -Unattended -SqlInstance "DBSRV01\SQL2022" -AdminEmail admin@corp.in -AdminPassword (Read-Host -AsSecureString)
  .\install.ps1 -Mode Service -HttpPort 8080 -HttpsPort 8443
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\DataKavach',
    [ValidateSet('IIS', 'Service')][string]$Mode = 'IIS',
    [string]$SqlInstance = 'localhost\SQLEXPRESS',
    [string]$DatabaseName = 'DataKavach',
    [string]$SqlUser = '',
    [SecureString]$SqlPassword,
    [switch]$InstallSqlExpress,
    [switch]$SkipLibreOffice,
    [int]$HttpPort = 8080,
    [int]$HttpsPort = 8443,
    [string]$HostName = $env:COMPUTERNAME,
    [string]$CertificateThumbprint = '',
    [switch]$NoHttps,
    [switch]$OpenFirewall = $true,
    [string]$AdminEmail = '',
    [string]$AdminName = 'Administrator',
    [SecureString]$AdminPassword,
    [int]$SessionMinutes = 60,
    [switch]$BuildFrontendFromSource,
    [switch]$Unattended,
    [switch]$NoBrowser
)

. (Join-Path $PSScriptRoot 'common.ps1')
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$log = Start-InstallLog $InstallDir 'install'
Write-Host "$ProductName installer - log: $log" -ForegroundColor White

function Plain([SecureString]$s) { if (-not $s) { return '' } return [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)) }

try {
    # ------------------------------------------------------------ 1. administrator
    Write-Step '1/15 Checking administrator permissions'
    Assert-Administrator

    $appDir      = Join-Path $InstallDir 'app'
    $dataDir     = Join-Path $InstallDir 'data'
    $storageDir  = Join-Path $dataDir 'storage'
    $backupDir   = Join-Path $InstallDir 'backups'
    $existing    = Get-InstallState $InstallDir
    if ($existing) { Write-Warn2 "Existing installation found (v$($existing.Version), installed $($existing.InstalledAt)). Data and settings will be preserved. Use upgrade.ps1 for version upgrades." }

    # ------------------------------------------------------------ 2. detect
    Write-Step '2/15 Detecting required software'
    $os = Get-CimInstance Win32_OperatingSystem
    Write-Ok "OS: $($os.Caption) ($($os.OSArchitecture))"
    if ($os.OSArchitecture -notmatch '64') { throw '64-bit Windows is required.' }
    $aspnet = Get-AspNetRuntimeVersion
    $bundle = Test-HostingBundle
    $odbc   = Get-OdbcSqlDriver
    $lo     = Get-LibreOfficePath
    $sqlSvc = Get-Service -Name 'MSSQL*' -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^MSSQL(\$|SERVER$)' }
    $iis    = (Get-Service W3SVC -ErrorAction SilentlyContinue) -ne $null
    $status = @(
        @{ n = 'ASP.NET Core 10 runtime'; ok = [bool]$aspnet; v = $aspnet },
        @{ n = 'ASP.NET Core Hosting Bundle (IIS module)'; ok = $bundle -or $Mode -eq 'Service'; v = $(if ($bundle) { 'installed' } elseif ($Mode -eq 'Service') { 'not needed (Service mode)' } else { '' }) },
        @{ n = 'IIS'; ok = $iis -or $Mode -eq 'Service'; v = $(if ($iis) { 'installed' } elseif ($Mode -eq 'Service') { 'not needed' } else { '' }) },
        @{ n = 'Microsoft ODBC Driver for SQL Server'; ok = [bool]$odbc; v = $odbc },
        @{ n = 'SQL Server (local services)'; ok = [bool]$sqlSvc; v = (($sqlSvc | ForEach-Object { $_.Name }) -join ', ') },
        @{ n = 'LibreOffice (PDF engine)'; ok = [bool]$lo -or $SkipLibreOffice; v = $(if ($lo) { $lo } elseif ($SkipLibreOffice) { 'skipped' } else { '' }) }
    )
    foreach ($s in $status) { if ($s.ok) { Write-Ok "$($s.n): $($s.v)" } else { Write-Warn2 "$($s.n): missing" } }

    # ------------------------------------------------------------ 3. install missing dependencies
    Write-Step '3/15 Installing missing dependencies (with approval)'
    if ($Mode -eq 'IIS' -and -not $iis) {
        if (Confirm-Action 'Enable IIS (Web Server) with required features?' -Unattended:$Unattended) {
            if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
                Install-WindowsFeature Web-Server, Web-Default-Doc, Web-Static-Content, Web-Http-Errors, Web-Http-Logging, Web-Stat-Compression, Web-Filtering, Web-Mgmt-Console, Web-Scripting-Tools, Web-WebSockets | Out-Null
            } else {
                $features = 'IIS-WebServerRole', 'IIS-WebServer', 'IIS-CommonHttpFeatures', 'IIS-DefaultDocument', 'IIS-StaticContent', 'IIS-HttpErrors', 'IIS-HttpLogging', 'IIS-RequestFiltering', 'IIS-HttpCompressionStatic', 'IIS-ManagementConsole', 'IIS-ManagementScriptingTools', 'IIS-WebSockets'
                foreach ($f in $features) { Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart | Out-Null }
            }
            Write-Ok 'IIS enabled'
        } else { throw 'IIS is required for IIS mode. Re-run with -Mode Service to host as a Windows service instead.' }
    }
    if (($Mode -eq 'IIS' -and -not $bundle) -or -not $aspnet) {
        $label = $(if ($Mode -eq 'IIS') { 'ASP.NET Core 10 Hosting Bundle' } else { 'ASP.NET Core 10 Runtime' })
        if (Confirm-Action "Install ${label}?" -Unattended:$Unattended) {
            $done = Invoke-OfflinePrereq 'dotnet-hosting-10*.exe' '/install /quiet /norestart' $label
            if (-not $done) {
                if (Test-Winget) { Invoke-Winget $(if ($Mode -eq 'IIS') { 'Microsoft.DotNet.HostingBundle.10' } else { 'Microsoft.DotNet.AspNetCore.10' }) $label }
                else {
                    $tmp = Join-Path $env:TEMP 'dotnet-hosting-10-win.exe'
                    Write-Host '    Downloading hosting bundle from Microsoft...'
                    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
                    Invoke-WebRequest -Uri 'https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe' -OutFile $tmp -UseBasicParsing
                    $p = Start-Process $tmp -ArgumentList '/install /quiet /norestart' -Wait -PassThru
                    if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) { throw "Hosting bundle installer failed ($($p.ExitCode))" }
                }
            }
            Update-SessionPath
            if ($Mode -eq 'IIS') { & net stop was /y | Out-Null; & net start w3svc | Out-Null }
            Write-Ok "$label installed"
        } else { throw "$label is required." }
    }
    if (-not (Get-AspNetRuntimeVersion)) { throw 'ASP.NET Core 10 runtime still not detected. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and re-run.' }
    if (-not $odbc) {
        if (Confirm-Action 'Install Microsoft ODBC Driver 18 for SQL Server?' -Unattended:$Unattended) {
            $done = Invoke-OfflinePrereq 'msodbcsql*.msi' '/quiet /norestart IACCEPTMSODBCSQLLICENSETERMS=YES' 'ODBC Driver 18'
            if (-not $done) { Invoke-Winget 'Microsoft.msodbcsql.18' 'ODBC Driver 18 for SQL Server' }
            $odbc = Get-OdbcSqlDriver
            if (-not $odbc) { throw 'ODBC Driver for SQL Server not detected after installation.' }
            Write-Ok "Installed $odbc"
        } else { throw 'Microsoft ODBC Driver 17 or 18 for SQL Server is required.' }
    }
    if (-not $sqlSvc -and $SqlInstance -match '^(localhost|\.|\(local\)|' + [regex]::Escape($env:COMPUTERNAME) + ')(\\|$)') {
        if ($InstallSqlExpress -or (Confirm-Action 'No local SQL Server found. Install SQL Server 2022 Express?' -Unattended:$Unattended)) {
            $done = Invoke-OfflinePrereq 'SQL2022*Express*.exe' '/QS /ACTION=Install /FEATURES=SQLENGINE /INSTANCENAME=SQLEXPRESS /IACCEPTSQLSERVERLICENSETERMS /SQLSYSADMINACCOUNTS="BUILTIN\Administrators"' 'SQL Server Express'
            if (-not $done) { Invoke-Winget 'Microsoft.SQLServer.2022.Express' 'SQL Server 2022 Express' }
            $SqlInstance = "localhost\SQLEXPRESS"
            Start-Sleep -Seconds 10
            Write-Ok 'SQL Server Express installed'
        }
    }
    if (-not $lo -and -not $SkipLibreOffice) {
        if (Confirm-Action 'Install LibreOffice (required for PDF export; DOCX works without it)?' -Unattended:$Unattended) {
            try {
                $done = Invoke-OfflinePrereq 'LibreOffice*.msi' '/qn /norestart ALLUSERS=1 CREATEDESKTOPLINK=0 REGISTER_ALL_MSO_TYPES=0 ISCHECKFORPRODUCTUPDATES=0' 'LibreOffice'
                if (-not $done) { Invoke-Winget 'TheDocumentFoundation.LibreOffice' 'LibreOffice' }
                $lo = Get-LibreOfficePath
                if ($lo) { Write-Ok "LibreOffice: $lo" } else { Write-Warn2 'LibreOffice not detected; PDF export will be unavailable until it is installed (repair.ps1 -InstallLibreOffice).' }
            } catch { Write-Warn2 "LibreOffice installation failed: $($_.Exception.Message). PDF export can be enabled later with repair.ps1 -InstallLibreOffice." }
        } else { Write-Warn2 'PDF export disabled until LibreOffice is installed.' }
    }

    # ------------------------------------------------------------ 4. SQL Server connection
    Write-Step "4/15 Connecting to SQL Server ($SqlInstance)"
    $sqlPw = Plain $SqlPassword
    if (-not $Unattended -and -not (Test-SqlConnection $SqlInstance $SqlUser $sqlPw)) {
        Write-Warn2 "Cannot connect to '$SqlInstance' with $(if ($SqlUser) { "SQL login '$SqlUser'" } else { 'Windows authentication' })."
        $SqlInstance = Read-Host 'SQL Server instance (e.g. localhost\SQLEXPRESS or DBSERVER\INSTANCE)'
        $SqlUser = Read-Host 'SQL login (leave blank for Windows authentication)'
        if ($SqlUser) { $SqlPassword = Read-Host 'SQL password' -AsSecureString; $sqlPw = Plain $SqlPassword }
    }
    if (-not (Test-SqlConnection $SqlInstance $SqlUser $sqlPw)) { throw "Cannot connect to SQL Server '$SqlInstance'. Check that the service is running, TCP/named pipes are enabled and the account has sysadmin or dbcreator rights." }
    $ver = Invoke-Sql (New-SqlConnectionString $SqlInstance 'master' $SqlUser $sqlPw) "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(100))"
    Write-Ok "Connected: SQL Server $ver"

    # ------------------------------------------------------------ 5. database
    Write-Step "5/15 Creating application database [$DatabaseName]"
    $dbq = $DatabaseName.Replace("'", "''")
    $exists = Invoke-Sql (New-SqlConnectionString $SqlInstance 'master' $SqlUser $sqlPw) "SELECT COUNT(*) FROM sys.databases WHERE name = N'$dbq'"
    if ([int]$exists -eq 0) {
        Invoke-Sql (New-SqlConnectionString $SqlInstance 'master' $SqlUser $sqlPw) "CREATE DATABASE [$($DatabaseName.Replace(']', ']]'))]" | Out-Null
        Write-Ok 'Database created'
    } else {
        Write-Ok 'Database already exists - preserved'
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        Grant-SqlServiceFolderAccess $SqlInstance $backupDir
        try { $bak = Invoke-DatabaseBackup $SqlInstance $DatabaseName $backupDir $SqlUser $sqlPw 'pre-install'; Write-Ok "Safety backup: $bak" } catch { Write-Warn2 "Safety backup skipped: $($_.Exception.Message)" }
    }

    # ------------------------------------------------------------ 7/8. deploy backend + frontend
    Write-Step '7/15 Deploying backend'
    $srcApp = Join-Path $PSScriptRoot 'app'
    if (-not (Test-Path (Join-Path $srcApp 'DataKavach.Api.dll'))) { throw "Application files not found in $srcApp" }
    if ($existing) { Stop-App $existing.Mode }
    New-Item -ItemType Directory -Force -Path $appDir | Out-Null
    Get-ChildItem $srcApp -Force | Where-Object { $_.Name -ne 'appsettings.Local.json' } | ForEach-Object { Copy-Item $_.FullName -Destination $appDir -Recurse -Force }
    Write-Ok "Application files copied to $appDir"

    Write-Step '8/15 Deploying frontend'
    $src = Join-Path $PSScriptRoot 'source\frontend'
    if ($BuildFrontendFromSource -and (Test-Path $src) -and (Test-Command 'npm')) {
        Push-Location $src
        try { & npm ci; & npm run build; if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' } } finally { Pop-Location }
        Remove-Item (Join-Path $appDir 'wwwroot') -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item (Join-Path $src 'dist') (Join-Path $appDir 'wwwroot') -Recurse -Force
        Write-Ok 'Frontend built from source and deployed'
    } else {
        if (-not (Test-Path (Join-Path $appDir 'wwwroot\index.html'))) { throw 'Pre-built frontend (wwwroot) missing from package.' }
        Write-Ok 'Pre-built frontend deployed (wwwroot)'
    }

    # ------------------------------------------------------------ 9. storage
    Write-Step '9/15 Configuring document storage'
    $identity = Get-AppIdentity $Mode
    foreach ($d in @($dataDir, $storageDir, $backupDir, (Join-Path $InstallDir 'logs'))) { New-Item -ItemType Directory -Force -Path $d | Out-Null }
    Grant-SqlServiceFolderAccess $SqlInstance $backupDir
    Write-Ok "Storage: $storageDir  Backups: $backupDir"

    # ------------------------------------------------------------ 11 (part 1). service identity so we can grant it rights
    Write-Step '11/15 Configuring application service'
    if ($Mode -eq 'IIS') {
        Import-Module WebAdministration
        if (-not (Test-Path "IIS:\AppPools\$PoolName")) { New-WebAppPool -Name $PoolName | Out-Null }
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ''
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name startMode -Value 'AlwaysRunning'
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.identityType -Value 'ApplicationPoolIdentity'
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.loadUserProfile -Value $true
        Write-Ok "Application pool '$PoolName' (No Managed Code, AlwaysRunning)"
    } else {
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        $dotnet = (Get-Command dotnet).Source
        $bin = "`"$dotnet`" `"$(Join-Path $appDir 'DataKavach.Api.dll')`""
        if (-not $svc) {
            & sc.exe create $ServiceName binPath= $bin start= delayed-auto obj= "NT SERVICE\$ServiceName" DisplayName= "Data Kavach DPDP Compliance" | Out-Null
            & sc.exe description $ServiceName 'Data Kavach - DPDP Compliance Management and Report Generator' | Out-Null
            & sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
        } else { & sc.exe config $ServiceName binPath= $bin | Out-Null }
        Write-Ok "Windows service '$ServiceName' (virtual account NT SERVICE\$ServiceName)"
    }
    Set-FolderAcl $dataDir $identity 'Modify'
    Set-FolderAcl (Join-Path $InstallDir 'logs') $identity 'Modify'
    Set-FolderAcl $backupDir $identity 'Modify'
    Set-FolderAcl $appDir $identity 'ReadAndExecute'
    Write-Ok "Granted $identity access to data, logs and backups"

    # SQL access for the app identity (Windows auth only)
    if (-not $SqlUser) {
        try { Grant-SqlAccess $SqlInstance $DatabaseName $identity '' ''; Write-Ok "SQL login + db_owner for $identity" }
        catch { Write-Warn2 "Could not grant SQL access to $identity on a remote server ($($_.Exception.Message)). For a remote SQL Server use a SQL login (-SqlUser) or grant the computer account access." }
    }

    # ------------------------------------------------------------ 12 (part 1). certificate for HTTPS
    $pfxPath = Join-Path $dataDir 'https.pfx'; $pfxPw = ''; $thumb = $CertificateThumbprint
    if (-not $NoHttps) {
        Write-Step '12/15 Configuring HTTPS certificate'
        if (-not $thumb) {
            $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.FriendlyName -eq 'Data Kavach' -and $_.NotAfter -gt (Get-Date).AddDays(30) } | Select-Object -First 1
            if (-not $cert) {
                $dns = @($HostName, 'localhost') + @([System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName) | Select-Object -Unique
                $cert = New-SelfSignedCertificate -DnsName $dns -CertStoreLocation 'Cert:\LocalMachine\My' -FriendlyName 'Data Kavach' -NotAfter (Get-Date).AddYears(3) -KeyExportPolicy Exportable -KeyAlgorithm RSA -KeyLength 2048
                # trust it on this machine
                $store = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine'); $store.Open('ReadWrite'); $store.Add($cert); $store.Close()
                Write-Ok "Self-signed certificate created for $($dns -join ', ') (replace with a CA certificate for production via -CertificateThumbprint)"
            }
            $thumb = $cert.Thumbprint
        }
        if ($Mode -eq 'Service') {
            $pfxPw = New-RandomSecret 24
            $sec = ConvertTo-SecureString $pfxPw -AsPlainText -Force
            Export-PfxCertificate -Cert "Cert:\LocalMachine\My\$thumb" -FilePath $pfxPath -Password $sec | Out-Null
        }
        Write-Ok "Certificate thumbprint $thumb"
    }

    # ------------------------------------------------------------ 6/7/10. configure backend, migrations, authentication
    Write-Step '10/15 Configuring authentication and backend settings'
    $prev = Read-JsonFile (Join-Path $appDir 'appsettings.Local.json')
    $jwt = $(if ($prev -and $prev.Jwt -and $prev.Jwt.SigningKey) { $prev.Jwt.SigningKey } else { New-RandomSecret 48 })
    $odbcCs = Get-OdbcConnectionString $odbc $SqlInstance $DatabaseName $SqlUser $sqlPw
    $kestrel = $null
    if ($Mode -eq 'Service') {
        $endpoints = [ordered]@{ Http = [ordered]@{ Url = "http://*:$HttpPort" } }
        if (-not $NoHttps) { $endpoints['Https'] = [ordered]@{ Url = "https://*:$HttpsPort"; Certificate = [ordered]@{ Path = $pfxPath; Password = $pfxPw } } }
        $kestrel = [ordered]@{ Endpoints = $endpoints }
    }
    Write-LocalSettings $appDir @{ ConnectionString = $odbcCs; JwtKey = $jwt; SessionMinutes = $SessionMinutes; StorageRoot = $storageDir; SofficePath = $(if ($lo) { $lo } else { '' }); BackupDir = $backupDir; HttpsRedirect = $false; AppIdentity = $identity; Kestrel = $kestrel }
    Write-Ok "JWT signing key $(if ($prev) { 'preserved' } else { 'generated' }); session timeout $SessionMinutes min; settings file ACL restricted"

    Write-Step '6/15 Applying database migrations'
    Invoke-AppCli $appDir @('--migrate')
    Write-Ok 'Database schema up to date; requirement library and roles seeded'

    # ------------------------------------------------------------ 11 (part 2). IIS site / service start
    if ($Mode -eq 'IIS') {
        $webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers><add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" /></handlers>
      <aspNetCore processPath="dotnet" arguments=".\DataKavach.Api.dll" stdoutLogEnabled="false" stdoutLogFile="..\logs\stdout" hostingModel="inprocess" requestTimeout="00:20:00">
        <environmentVariables><environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" /></environmentVariables>
      </aspNetCore>
      <security><requestFiltering><requestLimits maxAllowedContentLength="62914560" /></requestFiltering></security>
      <httpProtocol><customHeaders><remove name="X-Powered-By" /></customHeaders></httpProtocol>
    </system.webServer>
  </location>
</configuration>
"@
        Set-Content -Path (Join-Path $appDir 'web.config') -Value $webConfig -Encoding UTF8
        if (-not (Test-Path "IIS:\Sites\$SiteName")) {
            New-Website -Name $SiteName -PhysicalPath $appDir -ApplicationPool $PoolName -Port $HttpPort -Force | Out-Null
        } else {
            Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $appDir
            Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $PoolName
        }
        if (-not (Get-WebBinding -Name $SiteName -Protocol http -Port $HttpPort)) { New-WebBinding -Name $SiteName -Protocol http -Port $HttpPort | Out-Null }
        if (-not $NoHttps) {
            if (-not (Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort)) { New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort | Out-Null }
            $b = Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort
            $b.AddSslCertificate($thumb, 'My')
        }
        Write-Ok "IIS site '$SiteName' on http:$HttpPort$(if (-not $NoHttps) { " and https:$HttpsPort" })"
    }
    Start-App $Mode

    # ------------------------------------------------------------ 12 (part 2). firewall
    if ($OpenFirewall) {
        Write-Step '12/15 Configuring Windows Firewall'
        foreach ($p in @($HttpPort) + $(if (-not $NoHttps) { @($HttpsPort) } else { @() })) {
            $name = "Data Kavach TCP $p"
            if (-not (Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue)) { New-NetFirewallRule -DisplayName $name -Direction Inbound -Protocol TCP -LocalPort $p -Action Allow -Profile Domain, Private | Out-Null }
            Write-Ok "Inbound TCP $p allowed (Domain, Private profiles)"
        }
    }

    # ------------------------------------------------------------ 13. health
    Write-Step '13/15 Running health checks'
    $url = "http://localhost:$HttpPort"
    $h = Wait-Health $url 120
    if (-not $h) { throw "Health check failed at $url/api/health. See $InstallDir\logs and Windows Event Viewer (Application)." }
    Write-Ok "API healthy - schema v$($h.schema), PDF engine $(if ($h.pdf) { 'available' } else { 'NOT available' })"
    if (-not $NoHttps) { $hs = Wait-Health "https://localhost:$HttpsPort" 30; if ($hs) { Write-Ok 'HTTPS endpoint healthy' } else { Write-Warn2 'HTTPS endpoint did not respond; HTTP works.' } }

    # ------------------------------------------------------------ 14. administrator
    Write-Step '14/15 Creating initial administrator'
    if (-not $AdminEmail -and -not $Unattended) { $AdminEmail = Read-Host 'Administrator email (leave blank to create it in the browser on first visit)' }
    if ($AdminEmail) {
        if (-not $AdminPassword) { $AdminPassword = Read-Host 'Temporary administrator password (min 10 chars, upper, lower, digit)' -AsSecureString }
        Invoke-AppCli $appDir @('--create-admin', $AdminEmail, $AdminName, (Plain $AdminPassword))
    } else { Write-Ok 'The first visitor will be asked to create the Super Administrator (first-run setup screen).' }

    Save-InstallState $InstallDir @{ Version = '1.0.0'; Mode = $Mode; InstallDir = $InstallDir; SqlInstance = $SqlInstance; DatabaseName = $DatabaseName; SqlUser = $SqlUser; HttpPort = $HttpPort; HttpsPort = $(if ($NoHttps) { 0 } else { $HttpsPort }); InstalledAt = (Get-Date).ToString('s'); CertificateThumbprint = $thumb }

    # ------------------------------------------------------------ 15. open
    Write-Step '15/15 Done'
    $portal = $(if ($NoHttps) { "http://$($HostName):$HttpPort" } else { "https://$($HostName):$HttpsPort" })
    Write-Host ""
    Write-Host "  $ProductName is installed." -ForegroundColor Green
    Write-Host "  Portal:   $portal   (also http://$($HostName):$HttpPort)"
    Write-Host "  Data:     $dataDir"
    Write-Host "  Backups:  $backupDir   (schedule with: backup.ps1 -Schedule)"
    Write-Host "  Log:      $log"
    if (-not $NoBrowser) { Start-Process $portal }
}
catch {
    Write-Err2 $_.Exception.Message
    Write-Host "Installation did not complete. Fix the issue above and re-run install.ps1 (it is safe to re-run). Log: $log" -ForegroundColor Red
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}
try { Stop-Transcript | Out-Null } catch { }
exit 0
