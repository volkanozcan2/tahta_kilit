# Tahta Kilit'i KURMADAN dener.
#
#   powershell -ExecutionPolicy Bypass -File tools\dene.ps1
#
# Yayinlanmis .exe dosyalari yerine `dotnet run` kullanir. Windows 11'in
# Akilli Uygulama Denetimi (Smart App Control) imzasiz .exe dosyalarini
# engelledigi icin, Microsoft'un imzaladigi dotnet.exe uzerinden calistirmak
# genelde bu engele takilmaz.
#
# Servis KURULMAZ: cikmak istersen iki pencereyi kapatman yeter, kilit ekrani
# geri gelmez. Takilirsan Ctrl+Alt+Del ile oturumu kapat.

#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$kok = Split-Path -Parent $PSScriptRoot
$servis = Join-Path $kok 'src\TahtaKilit.Service\TahtaKilit.Service.csproj'
$kilit = Join-Path $kok 'src\TahtaKilit.Lock\TahtaKilit.Lock.csproj'

if (-not (Test-Path (Join-Path $env:ProgramData 'TahtaKilit\tahta.dat'))) {
    Write-Warning "Once kurulum sihirbazini calistir: dotnet run --project src\TahtaKilit.Admin"
    Write-Warning "Yapilandirma olmadan servis kilit ekranini baslatamaz."
    Write-Host ""
}

Write-Host "Once derleniyor (ilk seferde biraz surer)..."
dotnet build $servis --nologo --verbosity quiet
dotnet build $kilit --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Derleme basarisiz." }

Write-Host "Dogrulama servisi ayri pencerede baslatiliyor..."
Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "Write-Host 'Tahta Kilit - dogrulama servisi (kapatmak icin Ctrl+C)'; dotnet run --project '$servis'"
)

Start-Sleep -Seconds 3

Write-Host "Kilit ekrani aciliyor..."
Write-Host "Cikmak icin: Ctrl+Alt+Del -> Oturumu kapat" -ForegroundColor Yellow
dotnet run --project $kilit
