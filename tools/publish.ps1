param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\PixelMe\PixelMe.csproj"
$publish = Join-Path $root "publish"

[xml]$xml = Get-Content $project
$version = ($xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

if ($SkipInstaller) {
    Write-Output "Published PixelMe $version to $publish"
    return
}

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) { throw "Inno Setup 6 is not installed. Install it with: winget install JRSoftware.InnoSetup" }

& $iscc "/DAppVersion=$version" (Join-Path $root "installer\PixelMe.iss")
if ($LASTEXITCODE -ne 0) { throw "Building the installer failed" }

Write-Output "Installer: $(Join-Path $root "artifacts\PixelMe-Setup-$version.exe")"
