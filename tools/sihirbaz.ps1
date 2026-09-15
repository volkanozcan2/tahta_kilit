# Kurulum sihirbazini acar.
#
#   powershell -ExecutionPolicy Bypass -File tools\sihirbaz.ps1
#
# `dotnet run` kullanilamaz: sihirbaz yonetici yetkisi ister, dotnet run ise
# yetki yukseltemez ve "Istenen islem icin yukseltme gerekiyor" (hata 740)
# verir. Bu betik programi derleyip yukseltme istegiyle baslatir.

$ErrorActionPreference = 'Stop'

$kok = Split-Path -Parent $PSScriptRoot
$proje = Join-Path $kok 'src\TahtaKilit.Admin\TahtaKilit.Admin.csproj'
$exe = Join-Path $kok 'src\TahtaKilit.Admin\bin\Debug\net8.0-windows\TahtaKilit.Admin.exe'

Write-Host "Derleniyor..."
dotnet build $proje --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Derleme basarisiz." }

if (-not (Test-Path $exe)) { throw "Program bulunamadi: $exe" }

Write-Host "Sihirbaz aciliyor (yonetici izni istenecek)..."
Start-Process $exe -Verb RunAs
