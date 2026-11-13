# Builds a signed MSIX of AVR Control Center for local testing.
#
#   pwsh packaging/msix/build-msix.ps1
#
# Output: installer/out/AVRControlCenter-<version>.msix plus the .cer to trust.
# The package is signed with a self-signed certificate kept in
# Cert:\CurrentUser\My (created on first run, reused afterwards). Before the
# first install, trust it once from an elevated PowerShell:
#
#   Import-Certificate -FilePath installer\out\AVRControlCenter-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople

[CmdletBinding()]
param(
    [string]$Publisher = 'CN=sreddev'
)

$ErrorActionPreference = 'Stop'
$root    = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $root 'src\AvrControlCenter\AvrControlCenter.csproj'
$stage   = Join-Path $root 'publish\msix-staging'
$outDir  = Join-Path $root 'installer\out'

# --- Windows SDK tools (newest SDK that has makeappx) ---
$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } |
    Sort-Object { [version]$_.Name } | Select-Object -Last 1
if (-not $sdkBin) { throw 'Windows SDK not found (makeappx.exe).' }
$makeappx = Join-Path $sdkBin.FullName 'x64\makeappx.exe'
$signtool = Join-Path $sdkBin.FullName 'x64\signtool.exe'

# --- Version: MSIX wants four parts ---
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$msixVersion = (@($version.Split('.')) + @('0', '0', '0'))[0..3] -join '.'
Write-Host "Packaging AVR Control Center $msixVersion"

# --- Publish the same single-file build the zip release uses ---
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -o $stage
if ($LASTEXITCODE) { throw 'dotnet publish failed.' }

# --- Manifest ---
(Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw).
    Replace('$VERSION$', $msixVersion).Replace('$PUBLISHER$', $Publisher) |
    Set-Content (Join-Path $stage 'AppxManifest.xml') -Encoding utf8

# --- Logos, rendered from the app icon ---
Add-Type -AssemblyName System.Drawing
$assets = New-Item (Join-Path $stage 'Assets') -ItemType Directory -Force
$ico = Join-Path $root 'src\AvrControlCenter\Assets\app.ico'
foreach ($logo in @(
        @{ Name = 'Square44x44Logo.png';   Size = 44 },
        @{ Name = 'Square150x150Logo.png'; Size = 150 },
        @{ Name = 'StoreLogo.png';         Size = 50 })) {
    $icon = New-Object System.Drawing.Icon($ico, 256, 256)   # largest frame available
    $src  = $icon.ToBitmap()
    $bmp  = New-Object System.Drawing.Bitmap($logo.Size, $logo.Size)
    $g    = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.SmoothingMode     = 'HighQuality'
    $g.PixelOffsetMode   = 'HighQuality'
    $g.DrawImage($src, 0, 0, $logo.Size, $logo.Size)
    $bmp.Save((Join-Path $assets $logo.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $src.Dispose(); $icon.Dispose()
}

# --- Pack ---
New-Item $outDir -ItemType Directory -Force | Out-Null
$msix = Join-Path $outDir "AVRControlCenter-$version.msix"
& $makeappx pack /d $stage /p $msix /o
if ($LASTEXITCODE) { throw 'makeappx failed.' }

# --- Sign with a self-signed test certificate ---
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Publisher -and $_.FriendlyName -eq 'AVR Control Center MSIX test' -and $_.NotAfter -gt (Get-Date) } |
    Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating self-signed certificate $Publisher"
    $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher `
        -KeyUsage DigitalSignature -FriendlyName 'AVR Control Center MSIX test' `
        -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix
if ($LASTEXITCODE) { throw 'signtool failed.' }

$cer = Join-Path $outDir 'AVRControlCenter-test.cer'
Export-Certificate -Cert $cert -FilePath $cer -Force | Out-Null

Write-Host ''
Write-Host "MSIX:        $msix"
Write-Host "Certificate: $cer"
