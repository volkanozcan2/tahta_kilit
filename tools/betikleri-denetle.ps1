# Tum PowerShell betiklerini ayristirma hatasina karsi denetler.
#
#   pwsh tools/betikleri-denetle.ps1
#
# Bu betikler cogu zaman ancak gercek bir Windows'ta calistirilarak
# denenebiliyor; en azindan soz dizimi hatalari buradan yakalanir.

$hatali = $false

foreach ($dosya in Get-ChildItem -Path (Join-Path $PSScriptRoot '*.ps1') | Sort-Object Name) {
    $hatalar = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile(
        $dosya.FullName, [ref]$null, [ref]$hatalar)

    if ($hatalar -and $hatalar.Count -gt 0) {
        $hatali = $true
        Write-Host "HATA: $($dosya.Name)" -ForegroundColor Red
        foreach ($h in $hatalar) {
            Write-Host "   satir $($h.Extent.StartLineNumber): $($h.Message)"
        }
        continue
    }

    # PowerShell 5.1 BOM'suz dosyayi sistem kod sayfasiyla okur; ASCII disi
    # karakter varsa ayristirma bozulur. Bu yuzden BOM sart.
    $ilk = [byte[]](Get-Content -Path $dosya.FullName -AsByteStream -TotalCount 3)
    if ($ilk.Length -lt 3 -or $ilk[0] -ne 0xEF -or $ilk[1] -ne 0xBB -or $ilk[2] -ne 0xBF) {
        $hatali = $true
        Write-Host "HATA: $($dosya.Name) - UTF-8 BOM eksik" -ForegroundColor Red
        continue
    }

    Write-Host "tamam: $($dosya.Name)"
}

if ($hatali) { exit 1 }
