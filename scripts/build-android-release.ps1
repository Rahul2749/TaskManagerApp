# Build a signed Release APK that uses the Render production API.
# Prerequisites: Platforms/Android/signing.local.props + taskmanager-release.keystore
# (created once; passwords live in SIGNING_SECRETS.txt - do not commit)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }
if (Test-Path (Join-Path $PSScriptRoot "..\src\TaskManager\TaskManager.Mobile\TaskManager.Mobile.csproj")) {
  $root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$project = Join-Path $root "src\TaskManager\TaskManager.Mobile\TaskManager.Mobile.csproj"
$signingProps = Join-Path $root "src\TaskManager\TaskManager.Mobile\Platforms\Android\signing.local.props"

if (-not (Test-Path $signingProps)) {
  Write-Error "Missing $signingProps - generate a keystore first."
}

$outDir = Join-Path $root "artifacts\android-release"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Publishing signed Release APK (API: https://taskmanager-app-plt1.onrender.com/)..."
dotnet publish $project `
  -f net10.0-android `
  -c Release `
  -p:AndroidPackageFormat=apk `
  -p:RunAOTCompilation=false `
  -p:PublishTrimmed=false `
  -o $outDir

if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Done. APK files:"
Get-ChildItem -Path $outDir -Recurse -Filter *.apk | ForEach-Object {
  Write-Host ("  " + $_.FullName)
}
