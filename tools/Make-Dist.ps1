#Requires -Version 5.1
<#
  Assembles dist\ kit: exe hash + delivery kit. SPEC 10.5.
  (اقرأني.txt quick guide is added in Phase 9; installers are placed by the human integrator.)
#>
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path $PSScriptRoot -Parent
$Dist = Join-Path $RepoRoot 'dist'
$Exe = Join-Path $Dist 'CourtDesk.exe'
if (-not (Test-Path -LiteralPath $Exe)) { throw "missing: $Exe (run dotnet publish first)" }

$hash = (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  CourtDesk.exe" | Set-Content -LiteralPath ($Exe + '.sha256') -Encoding ASCII -NoNewline
Write-Host "sha256: $hash"

$Kit = Join-Path $Dist 'kit'
if (-not (Test-Path -LiteralPath $Kit)) { New-Item -ItemType Directory -Path $Kit | Out-Null }
Copy-Item -LiteralPath $Exe -Destination (Join-Path $Kit 'CourtDesk.exe') -Force
@'
CourtDesk prerequisites (placed by the human integrator, NOT downloaded by the agent):
1. .NET Framework 4.8 offline installer (ndp48-x86-x64-allos-enu.exe) for
   Win7 SP1 / Win8.1 / Win10 < 1607. Inbox on Win10 >= 1607 and Win11.
2. On bare Win7 SP1 only: KB4474419 and KB4490628 (prerequisites of the 4.8 installer).
3. Win7 RTM / Vista / XP are NOT supported (README tells the user to install SP1).
Data is per-user (%LOCALAPPDATA%\CourtDesk\); the officer must always use the same
Windows account (or set COURTDESK_HOME to a shared folder).
'@ | Set-Content -LiteralPath (Join-Path $Kit 'PREREQUISITES.txt') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $RepoRoot 'README.md') -Destination (Join-Path $Kit 'README.md') -Force
$Guide = Join-Path $RepoRoot ([string][char]0x0627 + [char]0x0642 + [char]0x0631 + [char]0x0623 + [char]0x0646 + [char]0x064A + '.txt')
if (Test-Path -LiteralPath $Guide) {
  Copy-Item -LiteralPath $Guide -Destination (Join-Path $Kit ([System.IO.Path]::GetFileName($Guide))) -Force
  Write-Host 'Arabic quick guide added.'
}
Write-Host 'kit ready.'
