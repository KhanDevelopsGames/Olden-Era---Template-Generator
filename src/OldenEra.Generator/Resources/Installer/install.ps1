$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$appId          = '{STEAM_APP_ID}'
$steamFolder    = '{STEAM_FOLDER_NAME}'
$templatesTail  = '{TEMPLATES_SUBPATH}'
$templateName   = '{TEMPLATE_NAME}'

function Find-TemplatesDir {
    foreach ($root in @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App $appId",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App $appId"
    )) {
        try {
            $install = (Get-ItemProperty -Path $root -ErrorAction Stop).InstallLocation
            if ($install -and (Test-Path $install)) {
                $candidate = Join-Path $install $templatesTail
                if (Test-Path $candidate) { return $candidate }
            }
        } catch { }
    }

    foreach ($base in @(
        (Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\$steamFolder"),
        (Join-Path ${env:ProgramFiles}      "Steam\steamapps\common\$steamFolder")
    )) {
        $candidate = Join-Path $base $templatesTail
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

$dest = Find-TemplatesDir
if (-not $dest) {
    Write-Host ""
    Write-Host "Could not locate your Olden Era installation." -ForegroundColor Red
    Write-Host "See README.txt in this folder for manual install steps." -ForegroundColor Red
    exit 1
}

$jsonSrc = Join-Path $scriptDir "$templateName.rmg.json"
$pngSrc  = Join-Path $scriptDir "$templateName.png"

Copy-Item -Path $jsonSrc -Destination $dest -Force
Copy-Item -Path $pngSrc  -Destination $dest -Force

Write-Host ""
Write-Host "Installed to: $dest" -ForegroundColor Green
