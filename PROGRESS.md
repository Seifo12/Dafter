# PROGRESS.md

## Phase 0 — Preflight & scaffold (in progress, 2026-10-07)

- Host: Windows 11 x64 build 26200 ✔
- `.NET SDK 8.0.425` installed via winget (was missing; BLOCKED.md written then resolved) ✔
- Disk C: ~3.4 GB free ✔ · git 2.55.0 ✔ · nuget.org:443 reachable ✔
- `git init` done; SPEC.md copied to repo root ✔

Gates:
- [x] `tools\Fetch-Vendor.ps1` runs; `vendor\MANIFEST.sha256` verifies (manifest: OK)
- [x] `dotnet build` ok fully offline (restore from vendor\packages, 0 warnings/errors)
- [x] native import allow-list test passes (genuine 1.0.118.0, CRT-free; mscoree+wintrust pinned, see DECISIONS.md)
- [x] Arch tests (skeleton) pass: 6/6 green
- [x] single `dist\CourtDesk.exe` runs from an empty dir (exit 3 = Phase-8 selftest hook reached)
- [x] app.manifest launch-bisected: compat GUIDs byte-verified, exe activates (was SxS 14001)
- [ ] commit `phase-0: preflight and scaffold`
