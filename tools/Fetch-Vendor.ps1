#Requires -Version 5.1
<#
  One-time ONLINE fetch: vendors SQLite binaries + offline NuGet feed,
  then every build is offline-repeatable. SPEC 2.6, Phase 0.
  Run from repo root:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\Fetch-Vendor.ps1
#>
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path $PSScriptRoot -Parent
$Vendor = Join-Path $RepoRoot 'vendor'
$Dl = Join-Path $Vendor '.dl'
$Managed = Join-Path $Vendor 'managed'
$Native86 = Join-Path $Vendor 'native\x86'
$Feed = Join-Path $Vendor 'packages'
foreach ($d in @($Dl, $Managed, $Native86, $Feed)) {
  if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d | Out-Null }
}

$SqliteId = 'Stub.System.Data.SQLite.Core.NetFramework'
$SqliteOfficialId = 'System.Data.SQLite.Core'
$SqliteVersion = '1.0.118'

Write-Host "1/4 downloading $SqliteOfficialId $SqliteVersion ..."
$officialNupkg = Join-Path $Dl ("$SqliteOfficialId.$SqliteVersion.nupkg")
if (-not (Test-Path -LiteralPath $officialNupkg)) {
  Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/$SqliteOfficialId/$SqliteVersion" `
    -OutFile $officialNupkg -UseBasicParsing
}
$officialDir = Join-Path $Dl 'sqlite-official'
if (Test-Path -LiteralPath $officialDir) { Remove-Item -LiteralPath $officialDir -Recurse -Force }
Copy-Item -LiteralPath $officialNupkg -Destination (Join-Path $Dl 'sqlite-official.zip') -Force
Expand-Archive -LiteralPath (Join-Path $Dl 'sqlite-official.zip') -DestinationPath $officialDir

Write-Host "1b/4 downloading $SqliteId $SqliteVersion (fallback) ..."
$sqliteNupkg = Join-Path $Dl ("$SqliteId.$SqliteVersion.nupkg")
if (-not (Test-Path -LiteralPath $sqliteNupkg)) {
  Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/$SqliteId/$SqliteVersion" `
    -OutFile $sqliteNupkg -UseBasicParsing
}
$extractDir = Join-Path $Dl 'sqlite'
if (Test-Path -LiteralPath $extractDir) { Remove-Item -LiteralPath $extractDir -Recurse -Force }
$sqliteZip = Join-Path $Dl 'sqlite.zip'
Copy-Item -LiteralPath $sqliteNupkg -Destination $sqliteZip -Force
Expand-Archive -LiteralPath $sqliteZip -DestinationPath $extractDir

Write-Host '2/4 locating binaries by file name ...'
$provenance = ''
$offManaged = Join-Path $officialDir 'lib\net46\System.Data.SQLite.dll'
$offInterop = Join-Path $officialDir 'build\net46\x86\SQLite.Interop.dll'
if ((Test-Path -LiteralPath $offManaged) -and (Test-Path -LiteralPath $offInterop)) {
  $managedHit = Get-Item -LiteralPath $offManaged
  $interopHit = Get-Item -LiteralPath $offInterop
  $provenance = "$SqliteOfficialId $SqliteVersion (static-CRT official build, lib/net46 + build/net46/x86)"
}
else {
  $managedHit = Get-ChildItem -LiteralPath $extractDir -Filter 'System.Data.SQLite.dll' -Recurse -File |
    Sort-Object { if ($_.FullName -match 'net46') { 0 } else { 1 } } | Select-Object -First 1
  $interopHit = Get-ChildItem -LiteralPath $extractDir -Filter 'SQLite.Interop.dll' -Recurse -File |
    Sort-Object { if ($_.FullName -match 'net46[\\/]x86') { 0 } elseif ($_.FullName -match 'x86') { 1 } else { 2 } } |
    Select-Object -First 1
  $provenance = "$SqliteId $SqliteVersion (lib/net46 + build/net46/x86, same package)"
}
if ($null -eq $managedHit) { throw 'System.Data.SQLite.dll not found in package' }
if ($null -eq $interopHit) { throw 'SQLite.Interop.dll not found in package' }
Copy-Item -LiteralPath $managedHit.FullName -Destination (Join-Path $Managed 'System.Data.SQLite.dll') -Force
Copy-Item -LiteralPath $interopHit.FullName -Destination (Join-Path $Native86 'SQLite.Interop.dll') -Force
Write-Host ("  managed: " + $managedHit.FullName)
Write-Host ("  native : " + $interopHit.FullName)

Write-Host '3/4 online restore (one time) + harvest offline feed ...'
& dotnet restore (Join-Path $RepoRoot 'CourtDesk.sln') --source 'https://api.nuget.org/v3/index.json'
if ($LASTEXITCODE -ne 0) { throw 'online restore failed' }
$gp = (& dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', ''
$gp = $gp.Trim()
$harvested = @{}
foreach ($proj in @('src\CourtDesk\CourtDesk.csproj', 'tests\CourtDesk.Tests\CourtDesk.Tests.csproj')) {
  $assets = Join-Path $RepoRoot (Join-Path (Split-Path $proj -Parent) 'obj\project.assets.json')
  $json = Get-Content -LiteralPath $assets -Raw | ConvertFrom-Json
  foreach ($lib in $json.libraries.PSObject.Properties) {
    if ($lib.Value.type -ne 'package') { continue }
    $parts = $lib.Name -split '/'
    $id = $parts[0]; $ver = $parts[1]
    $key = "$id/$ver"
    if ($harvested.ContainsKey($key)) { continue }
    $harvested[$key] = $true
    $src = Join-Path (Join-Path $gp $id.ToLowerInvariant()) (Join-Path $ver ("$id.$ver.nupkg"))
    if (-not (Test-Path -LiteralPath $src)) {
      $src = Join-Path (Join-Path $gp $id) (Join-Path $ver ("$id.$ver.nupkg"))
    }
    if (-not (Test-Path -LiteralPath $src)) { throw "nupkg not in global cache: $key" }
    Copy-Item -LiteralPath $src -Destination (Join-Path $Feed ("$id.$ver.nupkg")) -Force
    Write-Host ("  feed: $id $ver")
  }
}

Write-Host '4/4 writing manifest ...'
$manifest = Join-Path $Vendor 'MANIFEST.sha256'
$lines = foreach ($f in Get-ChildItem -LiteralPath $Vendor -Recurse -File |
    Where-Object { $_.FullName -notmatch '\.dl\\' -and $_.Name -ne 'MANIFEST.sha256' -and $_.Name -ne 'README.txt' }) {
  $h = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
  $rel = $f.FullName.Substring($Vendor.Length + 1)
  "$h  $rel"
}
$lines | Sort-Object | Set-Content -LiteralPath $manifest -Encoding ASCII
@"
CourtDesk vendored dependencies (offline-repeatable builds).
- managed/System.Data.SQLite.dll + native/x86/SQLite.Interop.dll: $provenance.
- packages/*.nupkg: offline NuGet feed harvested from project.assets.json after one online restore.
Verify: compare Get-FileHash output against MANIFEST.sha256.
"@ | Set-Content -LiteralPath (Join-Path $Vendor 'README.txt') -Encoding UTF8
Write-Host 'vendor ready.'
