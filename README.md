# CourtDesk — سجلّ المحكومين وإدارة الترحيل

Single-user, fully offline Windows desktop app (WPF, .NET Framework 4.6.2, x86):
detainee records, today's-sessions sidebar, one-click A4 convoy roster,
atomic court-decision workflow with immutable audit trail.

Master spec: `SPEC.md` (v1.0, authoritative). Progress: `PROGRESS.md`.
Decisions: `DECISIONS.md`. QA: `QA-CHECKLIST.md`.

## Prerequisites (build host)

- Windows 10/11 x64, >= 3 GB free disk
- .NET SDK 8.0.x (`dotnet --list-sdks` must list `8.0.xxx`)
- Internet **once** for `tools\Fetch-Vendor.ps1`; every later build is offline

## Build

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Fetch-Vendor.ps1
build.cmd
```

`build.cmd` (authoritative, fail-fast, offline): restore from `vendor\packages`
-> `dotnet test` -> `dotnet publish` to `dist\CourtDesk.exe` -> `--selftest`
in an empty folder -> `tools\Make-Dist.ps1` (hash + kit).

Product alone: `dotnet publish src\CourtDesk\CourtDesk.csproj -c Release -o dist`
-> `dist\CourtDesk.exe` (single file; user data stays in `%LOCALAPPDATA%\CourtDesk\`).

## Target PCs

Win7 SP1 -> Win11, 32/64-bit. Win10 >= 1607 / Win11 run as-is; older need the
.NET 4.8 offline installer (see `dist\kit\PREREQUISITES.txt` after Make-Dist).

## Deploy

1. Copy `dist\CourtDesk.exe` (and `CourtDesk.exe.config`) to any writable folder,
   e.g. `C:\CourtDesk\`.
2. First run extracts the verified `SQLite.Interop.dll` next to the exe and into
   `%LOCALAPPDATA%\CourtDesk\native\x86`, then creates the database.
3. The desktop shortcut is created by the app on first run.
4. `dist\kit\` holds the exe, `PREREQUISITES.txt`, this README and the exe hash.

Optional: set `COURTDESK_HOME` to a shared folder so several Windows accounts
read the same data; otherwise each user has their own database and the officer
must always use one Windows account.

## Verify a deployment

```
dist\CourtDesk.exe --selftest --home <temp folder>
```

Prints `native ok`, `schema ok`, `invariants ok`, `ops ok`, `backup ok` and
`selftest OK in ... ms`, exit code 0.

## First steps for the officer (Arabic)

`dist\kit\اقرأني.txt` is generated from the in-app help text and lists the six
steps: add a detainee, record a decision, undo, print the convoy roster, back up
to USB, and what to do when the computer warns about the clock.

## Layout

| Path | Contents |
|---|---|
| `src\CourtDesk\Domain` | records, decision rules, dates, Arabic text |
| `src\CourtDesk\Application` | use-cases and ports (create/decide/undo/today/search/backups) |
| `src\CourtDesk\Persistence` | SQLite, pragmas, migrations, stores, backup/restore, recovery |
| `src\CourtDesk\Printing` | roster pagination and the A4 FixedDocument |
| `src\CourtDesk\Platform` | single instance, logging, crash handler, power events |
| `src\CourtDesk\UI` | shell, views, dialogs, styles, icons |
| `tests\CourtDesk.Tests` | xUnit: Domain, Persistence, Crash, Printing, Resilience, UI, LayoutAudit, Screens, Arch |
| `tools` | `Fetch-Vendor.ps1`, `Make-Dist.ps1`, `CrashHarness`, `RenderScreens` |
| `artifacts` | review PNGs (`screens`, `audit`, `roster`) |

## Test commands

```
dotnet test tests\CourtDesk.Tests\CourtDesk.Tests.csproj -c Release
dotnet test tests\CourtDesk.Tests\CourtDesk.Tests.csproj -c Release --filter "Category=LayoutAudit"
dotnet test tests\CourtDesk.Tests\CourtDesk.Tests.csproj -c Release --filter "Category=Screens"
```

## Test categories

`Domain`, `Persistence`, `Crash`, `Printing`, `Resilience`, `UI`, `LayoutAudit`,
`Screens`, `Arch`.