[CmdletBinding()]
param([string]$ContentRoot = (Join-Path $PSScriptRoot '..\src\SourcingTenderManagementSystem.Web'))
$ErrorActionPreference = 'Stop'
$sourceCommit = '87416418657359cb625c412a48b6e1d6d41c29bd'
$resolvedRoot = [System.IO.Path]::GetFullPath($ContentRoot)
$destination = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot 'App_Data\Ocr\tessdata'))
$privateRoot = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot 'App_Data')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $destination.StartsWith($privateRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected OCR asset directory.' }
foreach ($directory in @($resolvedRoot, (Join-Path $resolvedRoot 'App_Data'), (Join-Path $resolvedRoot 'App_Data\Ocr'), $destination)) {
    if ((Test-Path -LiteralPath $directory) -and ((Get-Item -LiteralPath $directory).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) { throw 'Refusing a linked OCR asset directory.' }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$models = @{
    eng = '7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2'
    ara = 'E3206D3DC87FD50C24A0FB9F01838615911D25168F4E64415244B67D2BB3E729'
}
foreach ($language in @('eng', 'ara')) {
    $target = Join-Path $destination ($language + '.traineddata')
    if (Test-Path -LiteralPath $target) {
        if ((Get-Item -LiteralPath $target).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'Refusing a linked language asset.' }
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $models[$language]) { throw "Existing $language data differs from the approved version. No overwrite performed." }
        Write-Output "Verified existing $language language data."
        continue
    }
    $temporary = Join-Path $destination ($language + '.' + [guid]::NewGuid().ToString('N') + '.download')
    try {
        Invoke-WebRequest -Uri "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/$sourceCommit/$language.traineddata" -OutFile $temporary -TimeoutSec 120
        if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $models[$language]) { throw 'Downloaded language asset checksum mismatch.' }
        Move-Item -LiteralPath $temporary -Destination $target
        Write-Output "Installed verified $language language data."
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}
Write-Output 'Language setup complete. No database commands were executed.'
