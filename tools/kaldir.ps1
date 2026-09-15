# Tahta Kilit'i bu bilgisayardan kaldirir. YONETICI olarak calistirilmalidir.
#
# DIKKAT: Gizli anahtar silinir; ogretmenlerin telefonundaki kayit gecersiz olur.

#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$servis = 'TahtaKilit'
$hedef = Join-Path $env:ProgramFiles 'TahtaKilit'
$veri = Join-Path $env:ProgramData 'TahtaKilit'

if (Get-Service -Name $servis -ErrorAction SilentlyContinue) {
    Stop-Service -Name $servis -Force -ErrorAction SilentlyContinue
    sc.exe delete $servis | Out-Null
    Start-Sleep -Seconds 2
}

# Kilit ekrani ajani kullanici oturumunda calisiyor olabilir.
Get-Process -Name 'TahtaKilit.Lock' -ErrorAction SilentlyContinue | Stop-Process -Force

foreach ($klasor in @($hedef, $veri)) {
    if (Test-Path $klasor) {
        Remove-Item $klasor -Recurse -Force
        Write-Host "Silindi: $klasor"
    }
}

Write-Host "Tahta Kilit kaldirildi."
