# Tahta Kilit'i Windows Korumali Alan'da (Windows Sandbox) acar.
#
#   powershell -ExecutionPolicy Bypass -File tools\sandbox-ac.ps1
#
# Neden: Windows 11'in Akilli Uygulama Denetimi imzasiz programlari engelliyor.
# Korumali Alan'da bu kisitlama yok ve pencereyi kapatinca hicbir iz kalmiyor -
# kilit ekranini denemek icin en guvenli yer. Takilirsan Korumali Alan
# penceresini kapatman yeterli.
#
# Proje klasoru salt okunur baglanir; derleme Korumali Alan'in icinde yapilir,
# senin dosyalarina hicbir sey yazilmaz.

$ErrorActionPreference = 'Stop'

$kok = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command 'WindowsSandbox.exe' -ErrorAction SilentlyContinue)) {
    Write-Host "Windows Korumali Alan kurulu degil." -ForegroundColor Yellow
    Write-Host "Yonetici PowerShell'de sunu calistirip bilgisayari yeniden baslat:"
    Write-Host "  Enable-WindowsOptionalFeature -Online -FeatureName 'Containers-DisposableClientVM' -All"
    Write-Host ""
    Write-Host "Not: Windows 11 Home surumunde bu ozellik yoktur."
    exit 1
}

$wsb = Join-Path $env:TEMP 'tahta-kilit.wsb'

# vGPU kapali: yazilim ile cizim daha yavas ama uyumluluk sorunu cikarmiyor.
@"
<Configuration>
  <VGpu>Disable</VGpu>
  <Networking>Default</Networking>
  <MemoryInMB>8192</MemoryInMB>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$kok</HostFolder>
      <SandboxFolder>C:\kaynak</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoExit -ExecutionPolicy Bypass -File C:\kaynak\tools\sandbox-hazirla.ps1</Command>
  </LogonCommand>
</Configuration>
"@ | ForEach-Object { [IO.File]::WriteAllText($wsb, $_) }  # BOM'suz yaz

Write-Host "Korumali Alan aciliyor. Icinde .NET kurulup testler kosulacak;"
Write-Host "ilk acilis birkac dakika surer."

Start-Process $wsb
