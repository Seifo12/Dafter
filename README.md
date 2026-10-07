# CourtDesk — سجل المحبوسين والجلسات

Single-user, fully offline Windows desktop app (WPF, .NET Framework 4.6.2, x86):
detainee records, today's-sessions sidebar, one-click A4 convoy roster,
atomic court-decision workflow with immutable audit trail.

Master spec: `SPEC.md` (v1.0, authoritative). Progress: `PROGRESS.md`.
Decisions: `DECISIONS.md`.

## Prerequisites (build host)

- Windows 10/11 x64, ≥ 3 GB free disk
- .NET SDK 8.0.x (`dotnet --list-sdks` must list `8.0.xxx`)
- Internet **once** for `tools\Fetch-Vendor.ps1`; every later build is offline

## Build

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Fetch-Vendor.ps1
build.cmd
```

`build.cmd` (authoritative, fail-fast, offline): restore from `vendor\packages`
→ `dotnet test` → `dotnet publish` to `dist\CourtDesk.exe` → `--selftest`
in an empty folder → `tools\Make-Dist.ps1` (hash + kit).

Product alone: `dotnet publish src\CourtDesk\CourtDesk.csproj -c Release -o dist`
⇒ `dist\CourtDesk.exe` (single file; user data stays in `%LOCALAPPDATA%\CourtDesk\`).

## Target PCs

Win7 SP1 → Win11, 32/64-bit. Win10 ≥ 1607 / Win11 run as-is; older need the
.NET 4.8 offline installer (see `dist\kit\PREREQUISITES.txt` after Make-Dist).
