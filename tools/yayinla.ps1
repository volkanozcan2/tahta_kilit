# Tahta Kilit'i tek klasore yayinlar.
#
#   powershell -ExecutionPolicy Bypass -File tools\yayinla.ps1
#
# Cikti: yayin\  - servis, kilit ekrani ve kurulum sihirbazi bir arada.
# Kendi kendine yeterli (self-contained) yayinlanir; tahtaya ayrica .NET
# kurmak gerekmez.

$ErrorActionPreference = 'Stop'

$kok = Split-Path -Parent $PSScriptRoot
$cikti = Join-Path $kok 'yayin'

if (Test-Path $cikti) { Remove-Item $cikti -Recurse -Force }

$projeler = @(
    'src\TahtaKilit.Service\TahtaKilit.Service.csproj',
    'src\TahtaKilit.Lock\TahtaKilit.Lock.csproj',
    'src\TahtaKilit.Admin\TahtaKilit.Admin.csproj'
)

foreach ($proje in $projeler) {
    Write-Host "Yayinlaniyor: $proje"
    dotnet publish (Join-Path $kok $proje) `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $cikti `
        -p:DebugType=none
    if ($LASTEXITCODE -ne 0) { throw "Yayinlama basarisiz: $proje" }
}

Write-Host ""
Write-Host "Hazir: $cikti"
Write-Host "Kurmak icin yonetici PowerShell'de:  tools\kur.ps1"
