# NauTilus Monitor - Script de compilacion
# No necesita Visual Studio ni el SDK de .NET: usa el compilador de C# que
# viene incluido en Windows 10 y 11 (.NET Framework 4.x).
#
# Uso:
#   powershell -ExecutionPolicy Bypass -File build.ps1
#   powershell -ExecutionPolicy Bypass -File build.ps1 -NoAdvanced   (no baja la DLL de sensores)
#   powershell -ExecutionPolicy Bypass -File build.ps1 -RefreshAdvanced   (fuerza re-descarga)

param(
    [switch]$NoAdvanced,        # omite la descarga de LibreHardwareMonitor
    [switch]$RefreshAdvanced,   # vuelve a descargar aunque ya exista
    [string]$LhmVersion = '0.9.4', # version de LibreHardwareMonitorLib (layout net472 simple: Lib + HidSharp)
    # ---- Firma de codigo (opcional): si no se indica certificado, NO se firma ----
    [string]$SignThumbprint = '',  # huella del certificado en el almacen (Cert:\CurrentUser\My o LocalMachine\My)
    [string]$SignPfx = '',         # o ruta a un .pfx
    [string]$SignPfxPassword = '', # contrasena del .pfx (si la tiene)
    [string]$TimestampUrl = 'http://timestamp.sectigo.com'  # sellado de tiempo (sobrevive a la caducidad del cert)
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

# ---------------------------------------------------------------------------
#  1) Compilacion del ejecutable
# ---------------------------------------------------------------------------
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $fw 'csc.exe'))) {
    $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) {
    Write-Host "ERROR: No se encontro csc.exe. Instala .NET Framework 4.x." -ForegroundColor Red
    exit 1
}
$wpf = Join-Path $fw 'WPF'

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$out = Join-Path $dist 'NautilusMonitor.exe'

$refs = @(
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $fw  'System.Xaml.dll'),
    (Join-Path $fw  'System.dll'),
    (Join-Path $fw  'System.Core.dll'),
    (Join-Path $fw  'System.Xml.dll'),
    (Join-Path $fw  'System.Management.dll'),
    (Join-Path $fw  'System.Windows.Forms.dll'),
    (Join-Path $fw  'System.Drawing.dll')
)
$refArgs = $refs | ForEach-Object { "/reference:$_" }

$srcs = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$manifest = Join-Path $root 'build\app.manifest'
$icon     = Join-Path $root 'build\appicon.ico'
$iconPng  = Join-Path $root 'build\appicon.png'

Write-Host "Compilando NauTilus Monitor..." -ForegroundColor Cyan
& $csc /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ "/out:$out" "/win32manifest:$manifest" "/win32icon:$icon" "/resource:$iconPng,NautilusMonitor.appicon.png" "/resource:$icon,NautilusMonitor.appicon.ico" $refArgs $srcs

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $out)) {
    Write-Host "La compilacion ha fallado." -ForegroundColor Red
    exit 1
}
$kb = [int]((Get-Item $out).Length / 1024)
Write-Host "OK - Compilado: $out ($kb KB)" -ForegroundColor Green

# ---------------------------------------------------------------------------
#  Firma de codigo (opcional). Con -SignThumbprint <huella> o -SignPfx <ruta>.
#  Sin certificado no se firma (comportamiento por defecto). Usa el cmdlet
#  Set-AuthenticodeSignature, asi que no necesita el SDK de Windows (signtool).
# ---------------------------------------------------------------------------
function Sign-Exe {
    param([string]$Path)
    $cert = $null
    if ($SignThumbprint) {
        $cert = Get-ChildItem "Cert:\CurrentUser\My\$SignThumbprint" -ErrorAction SilentlyContinue
        if (-not $cert) { $cert = Get-ChildItem "Cert:\LocalMachine\My\$SignThumbprint" -ErrorAction SilentlyContinue }
        if (-not $cert) { Write-Host "AVISO: no se encontro el certificado con huella $SignThumbprint." -ForegroundColor Yellow; return }
    }
    elseif ($SignPfx) {
        if (-not (Test-Path $SignPfx)) { Write-Host "AVISO: no se encontro el PFX '$SignPfx'." -ForegroundColor Yellow; return }
        try { $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($SignPfx, $SignPfxPassword) }
        catch { Write-Host ("AVISO: no se pudo abrir el PFX (" + $_.Exception.Message + ").") -ForegroundColor Yellow; return }
    }
    else {
        Write-Host "Firma: omitida (sin certificado). Pasa -SignThumbprint o -SignPfx para firmar." -ForegroundColor DarkGray
        return
    }
    try {
        $res = Set-AuthenticodeSignature -FilePath $Path -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampUrl -ErrorAction Stop
        if ($res.Status -eq 'Valid') { Write-Host ("Firmado OK - " + $cert.Subject) -ForegroundColor Green }
        else { Write-Host ("Firma con estado '" + $res.Status + "': " + $res.StatusMessage) -ForegroundColor Yellow }
    }
    catch { Write-Host ("AVISO: fallo al firmar (" + $_.Exception.Message + ").") -ForegroundColor Yellow }
}
Sign-Exe $out

# ---------------------------------------------------------------------------
#  2) Sensores avanzados (opcional): LibreHardwareMonitorLib.dll + dependencias
#     Se descargan del registro oficial de NuGet (paquetes MIT) y se colocan
#     junto al .exe. Si no hay red, se avisa pero el build NO falla: la app
#     sigue funcionando en su modo ligero sin driver.
# ---------------------------------------------------------------------------
function Get-NuGetDll {
    param([string]$Id, [string]$Version, [string]$DestDir, [string]$TmpDir)

    $idl = $Id.ToLower()
    if ([string]::IsNullOrEmpty($Version)) {
        $idx = Invoke-RestMethod -Uri "https://api.nuget.org/v3-flatcontainer/$idl/index.json" -UseBasicParsing
        $stable = @($idx.versions | Where-Object { $_ -notmatch '-' })
        if ($stable.Count -eq 0) { $stable = @($idx.versions) }
        $Version = $stable[-1]
    }

    $nupkg = Join-Path $TmpDir "$idl.$Version.nupkg"
    $url = "https://api.nuget.org/v3-flatcontainer/$idl/$Version/$idl.$Version.nupkg"
    Invoke-WebRequest -Uri $url -OutFile $nupkg -UseBasicParsing

    $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg)
    try {
        # Elige el mejor TFM para un host .NET Framework 4.x (evita la referencia ref/)
        $tfms = @('net472','net471','net47','net462','net461','net46','net45','net40','net35','netstandard2.0','netstandard2.1')
        $target = $null
        # 1) layout plano lib/<tfm>/<id>.dll
        foreach ($tfm in $tfms) {
            $want = "lib/$tfm/$idl.dll"
            $e = $zip.Entries | Where-Object { $_.FullName.ToLower() -eq $want } | Select-Object -First 1
            if ($e) { $target = $e; break }
        }
        # 2) layout por arquitectura runtimes/win-x64/lib/<tfm>/<id>.dll (implementacion real)
        if (-not $target) {
            foreach ($tfm in $tfms) {
                $want = "runtimes/win-x64/lib/$tfm/$idl.dll"
                $e = $zip.Entries | Where-Object { $_.FullName.ToLower() -eq $want } | Select-Object -First 1
                if ($e) { $target = $e; break }
            }
        }
        # 3) cualquier lib/<tfm>/<id>.dll, excluyendo ref/ y otras arquitecturas
        if (-not $target) {
            $target = $zip.Entries | Where-Object {
                $f = $_.FullName.ToLower()
                ($f -match "(^|/)lib/[^/]+/$idl\.dll$") -and ($f -notmatch '(^|/)ref/') -and ($f -notmatch 'win-x86|win-arm')
            } | Select-Object -First 1
        }
        if (-not $target) { throw "El paquete $Id $Version no contiene una DLL utilizable" }

        $outDll = Join-Path $DestDir "$Id.dll"
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($target, $outDll, $true)

        # Lee las dependencias no-framework del .nuspec
        $deps = @()
        $nuspec = $zip.Entries | Where-Object { $_.FullName.ToLower().EndsWith('.nuspec') } | Select-Object -First 1
        if ($nuspec) {
            $sr = New-Object System.IO.StreamReader($nuspec.Open())
            $xmlTxt = $sr.ReadToEnd(); $sr.Close()
            $xml = [xml]$xmlTxt
            $nodes = $xml.SelectNodes('//*[local-name()="dependency"]')
            foreach ($d in $nodes) {
                $did = $d.id
                if ([string]::IsNullOrEmpty($did)) { continue }
                $l = $did.ToLower()
                # Omite ensamblados del propio .NET Framework / facades / especificos de Linux:
                # en net472 sobre Windows solo hace falta HidSharp ademas de la Lib.
                if ($l.StartsWith('system.') -or $l.StartsWith('microsoft.') -or $l.StartsWith('netstandard') -or
                    $l.StartsWith('runtime.') -or $l.StartsWith('mono.')) { continue }
                $dv = ($d.version -replace '[\[\]\(\)]', '')
                $dv = ($dv -split ',')[0].Trim()
                $deps += (New-Object psobject -Property @{ Id = $did; Version = $dv })
            }
        }
        return ,($deps | Sort-Object Id -Unique)
    }
    finally { $zip.Dispose() }
}

$lhmDll = Join-Path $dist 'LibreHardwareMonitorLib.dll'
if ($NoAdvanced) {
    Write-Host "Sensores avanzados: omitidos (-NoAdvanced)." -ForegroundColor DarkGray
}
elseif ((Test-Path $lhmDll) -and (-not $RefreshAdvanced)) {
    Write-Host "Sensores avanzados: ya presentes (usa -RefreshAdvanced para actualizar)." -ForegroundColor DarkGray
}
else {
    Write-Host "Descargando sensores avanzados (LibreHardwareMonitor, MIT) desde NuGet..." -ForegroundColor Cyan
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("nautilus_lhm_" + [Guid]::NewGuid().ToString('N'))
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $script:ProgressPreference = 'SilentlyContinue'
        Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
        New-Item -ItemType Directory -Force -Path $tmp | Out-Null

        $queue = New-Object System.Collections.Queue
        $queue.Enqueue((New-Object psobject -Property @{ Id = 'LibreHardwareMonitorLib'; Version = $LhmVersion }))
        $seen = @{}

        while ($queue.Count -gt 0) {
            $pkg = $queue.Dequeue()
            $key = $pkg.Id.ToLower()
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true

            $deps = Get-NuGetDll -Id $pkg.Id -Version $pkg.Version -DestDir $dist -TmpDir $tmp
            $sz = [int]((Get-Item (Join-Path $dist ($pkg.Id + '.dll'))).Length / 1024)
            Write-Host ("   + " + $pkg.Id + ".dll (" + $sz + " KB)") -ForegroundColor Green
            foreach ($d in $deps) { if (-not $seen.ContainsKey($d.Id.ToLower())) { $queue.Enqueue($d) } }
        }
        Write-Host "Sensores avanzados listos: coloca la app y las DLL juntas para activarlos." -ForegroundColor Green
    }
    catch {
        Write-Host ("AVISO: no se pudieron descargar los sensores avanzados (" + $_.Exception.Message + ").") -ForegroundColor Yellow
        Write-Host "       El .exe funciona igual en modo ligero. Reintenta con red o baja la DLL a mano." -ForegroundColor Yellow
    }
    finally {
        try { if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp } } catch { }
    }
}
