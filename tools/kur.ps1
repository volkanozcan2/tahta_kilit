# Tahta Kilit'i bu bilgisayara kurar. YONETICI olarak calistirilmalidir.
#
#   powershell -ExecutionPolicy Bypass -File tools\kur.ps1
#
# 1. Dosyalari Program Files altina kopyalar
# 2. Servisi olusturup baslatir
# 3. Kurulum sihirbazini acar

#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$kok = Split-Path -Parent $PSScriptRoot
$kaynak = Join-Path $kok 'yayin'
$hedef = Join-Path $env:ProgramFiles 'TahtaKilit'
$servis = 'TahtaKilit'

if (-not (Test-Path $kaynak)) {
    throw "Once yayinla: tools\yayinla.ps1"
}

# Servis calisiyorsa dosyalar kilitli olur.
if (Get-Service -Name $servis -ErrorAction SilentlyContinue) {
    Write-Host "Mevcut servis durduruluyor..."
    Stop-Service -Name $servis -Force -ErrorAction SilentlyContinue
    sc.exe delete $servis | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Dosyalar kopyalaniyor: $hedef"
New-Item -ItemType Directory -Path $hedef -Force | Out-Null
Copy-Item -Path (Join-Path $kaynak '*') -Destination $hedef -Recurse -Force

Write-Host "Servis olusturuluyor..."
$exe = Join-Path $hedef 'TahtaKilit.Service.exe'
sc.exe create $servis binPath= "`"$exe`"" start= auto DisplayName= "Tahta Kilit" | Out-Null
sc.exe description $servis "Akilli tahtayi kilitler; yalnizca ogretmenin telefonundaki sifreyle acilir." | Out-Null

# Servis cokerse Windows yeniden baslatsin: kilit ekraninin ayakta kalmasi buna bagli.
sc.exe failure $servis reset= 0 actions= restart/5000/restart/5000/restart/5000 | Out-Null

Start-Service -Name $servis
Write-Host "Servis calisiyor."

Write-Host ""
Write-Host "Kurulum sihirbazi aciliyor..."
Start-Process (Join-Path $hedef 'TahtaKilit.Admin.exe')
