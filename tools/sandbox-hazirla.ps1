# Windows Korumali Alan'in ICINDE calisir; tools\sandbox-ac.ps1 tarafindan
# otomatik baslatilir. Elle calistirman gerekmez.
#
# Korumali Alan her acilista bos bir Windows'tur: .NET burada kurulur,
# proje kopyalanir ve testler kosulur.

$ErrorActionPreference = 'Stop'
$hedef = 'C:\tahta_kilit'

Write-Host "=== Tahta Kilit — Korumali Alan hazirligi ===" -ForegroundColor Cyan
Write-Host ""

# Salt okunur baglanan kaynagi yazilabilir bir yere kopyala.
Write-Host "[1/3] Proje kopyalaniyor..."
robocopy C:\kaynak $hedef /E /XD bin obj node_modules yayin .git /NFL /NDL /NJH /NJS /NC /NS | Out-Null
$global:LASTEXITCODE = 0  # robocopy basarida da sifir disi kod dondurur

Write-Host "[2/3] .NET 8 SDK kuruluyor (birkac dakika surebilir)..."
$betik = Join-Path $env:TEMP 'dotnet-install.ps1'
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $betik -UseBasicParsing
& $betik -Channel 8.0 -InstallDir 'C:\dotnet' -NoPath | Out-Null

$env:PATH = "C:\dotnet;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Write-Host "[3/3] Testler kosuluyor..."
Set-Location $hedef
dotnet test --nologo

Write-Host ""
Write-Host "=== Hazir ===" -ForegroundColor Green
Write-Host ""
Write-Host "Sirasiyla:" -ForegroundColor Cyan
Write-Host "  1) dotnet run --project src\TahtaKilit.Admin"
Write-Host "     Tahta adi ve kurulum PIN'i gir, karekod cikinca telefonla okut."
Write-Host ""
Write-Host "  2) powershell -ExecutionPolicy Bypass -File tools\dene.ps1"
Write-Host "     Kilit ekranini acar."
Write-Host ""
Write-Host "Cikmak icin Korumali Alan penceresini kapat; her sey silinir." -ForegroundColor Yellow
Write-Host ""
