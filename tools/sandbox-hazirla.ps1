# Windows Korumali Alan'in ICINDE calisir; tools\sandbox-ac.ps1 tarafindan
# otomatik baslatilir. Elle calistirman gerekmez.
#
# Korumali Alan her acilista bos bir Windows'tur: .NET burada kurulur,
# proje kopyalanir ve testler kosulur.

$ErrorActionPreference = 'Stop'
$hedef = 'C:\tahta_kilit'

Write-Host "=== Tahta Kilit - Korumali Alan hazirligi ===" -ForegroundColor Cyan
Write-Host ""

# Salt okunur baglanan kaynagi yazilabilir bir yere kopyala.
Write-Host "[1/3] Proje kopyalaniyor..."
robocopy C:\kaynak $hedef /E /XD bin obj node_modules yayin .git /NFL /NDL /NJH /NJS /NC /NS | Out-Null
$global:LASTEXITCODE = 0  # robocopy basarida da sifir disi kod dondurur

# .NET varsayilan konuma kurulur. Standart disi bir klasore kurulursa
# uygulamalar (ayri surec olarak baslayan WPF programlari gibi) calisma
# zamanini bulamiyor: "You must install .NET Desktop Runtime" hatasi.
$dotnetDir = Join-Path $env:ProgramFiles 'dotnet'
$dotnetExe = Join-Path $dotnetDir 'dotnet.exe'

# Betik iki kez calisabiliyor (Korumali Alan acilista kendisi baslatiyor).
# Ikinci kurulum, calisan dotnet.exe'yi ustune yazmaya calisip patliyordu.
$kurulu = $false
if (Test-Path $dotnetExe) {
    try {
        $null = & $dotnetExe --version 2>$null
        $kurulu = ($LASTEXITCODE -eq 0)
    } catch {
        $kurulu = $false
    }
}

if ($kurulu) {
    Write-Host "[2/3] .NET zaten kurulu, atlaniyor."
} else {
    Write-Host "[2/3] .NET 8 SDK kuruluyor (birkac dakika surebilir)..."
    $betik = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $betik -UseBasicParsing
    & $betik -Channel 8.0 -InstallDir $dotnetDir -NoPath | Out-Null
}

$env:PATH = $dotnetDir + ';' + $env:PATH
$env:DOTNET_ROOT = $dotnetDir
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

# Makine genelinde de ayarla: yoksa hem yeni pencerelerde dotnet bulunamaz,
# hem de yonetici olarak baslatilan uygulamalar calisma zamanini goremez.
try {
    $makine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    if ($makine -notlike "*$dotnetDir*") {
        [Environment]::SetEnvironmentVariable('Path', $dotnetDir + ';' + $makine, 'Machine')
    }
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $dotnetDir, 'Machine')
} catch {
    Write-Warning 'Makine ortam degiskenleri ayarlanamadi; dotnet yalnizca BU pencerede kullanilabilir.'
}

Write-Host "[3/3] Testler kosuluyor..."
Set-Location $hedef
dotnet test --nologo

Write-Host ""
Write-Host "=== Hazir ===" -ForegroundColor Green
Write-Host ""
Write-Host "Sirasiyla:" -ForegroundColor Cyan
Write-Host "  1) powershell -ExecutionPolicy Bypass -File tools\sihirbaz.ps1"
Write-Host "     Tahta adi ve kurulum PIN'i gir, karekod cikinca telefonla okut."
Write-Host ""
Write-Host "  2) powershell -ExecutionPolicy Bypass -File tools\dene.ps1"
Write-Host "     Kilit ekranini acar."
Write-Host ""
Write-Host "Cikmak icin Korumali Alan penceresini kapat; her sey silinir." -ForegroundColor Yellow
Write-Host ""
