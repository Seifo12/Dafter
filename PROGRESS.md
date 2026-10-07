# PROGRESS.md

## Phase 3 — Application services (done)

- Services: DefendantService (create/edit/void/restore + validation + dup warn),
  DecisionService (§6.2 atomic + read-first idempotency + fault hooks),
  UndoService (§6.3 + read-first), TodayService (cache by date+seq),
  ScheduleService, SearchIndex (Spaced/Joined tokens, urgency sort),
  LookupService (idempotent add by norm), SettingsService (Appendix B),
  BackupService (auto ring, exit skip-if-unchanged, USB verify+rotate,
  retention 60 + 12 months), RosterService (held-date set R(D)),
  ClockSanityService, IntegrityService, CsvExportService (BOM/CRLF).
- Ports: IDriveProvider, ILogger, IMetaStore, rows, IChangeSeqSource.
- Bugs fixed via failing tests: ListDecidedOn column offset; exit seq+1
  accounting; idempotent paths must not bump change_seq; lookup dupes return
  existing id; File.Copy replaced by streams (Arch ban holds).
- Gates: Services 17/17; non-Crash suite 74/74; 20k-row filter < 50 ms.

## Phase 2 — Persistence (done, 2026-10-07)

- `Persistence/`: DbPaths, DbGateway (DbThread, BEGIN IMMEDIATE, meta bump,
  read-back verify), SqliteBootstrap (pragma order + verify), MigrationRunner +
  `001_initial.sql` (SPEC §4.4 verbatim), IntegrityChecker (I1–I6),
  4 SQLite stores, SeedData, SqliteBackupEngine (backup API → verify → atomic
  rename), RestoreSwap (marker rollforward/rollback), DatabaseRecovery
  (quarantine + newest-verified auto-restore).
- `Application/Ports/`: IDatabase/ISession + store/engine ports (services use
  ports only). `tools/CrashHarness`: source-linked crash loop (create/edit/
  decide/undo + fsynced ack log).
- Bugs fixed via failing tests first: pragma setter-vs-getter reads;
  BackupDatabase 6-arg shape; NotADb enum name; test-only uuid/version mistakes.
- Gates: Persistence 11/11; Crash 300 kills normal + 300 kills backup
  (quick_check ok, I1–I6 hold, ack-subset holds, zero partial ops); full suite
  59/59 green.

## Phase 1 — Domain (done, 2026-10-07)

- `Domain/`: Defendant, DefendantSnapshot, DefendantStatus, DecisionCode,
  DecisionRequest, DecisionRules (Validate V1–V12 + Apply), ValidationResult,
  LocalDate, ArabicText, ArabicDateFormat, SnapshotJson, DisplayState,
  IClock, SystemClock. `Resources/Strings.cs`: full Appendix A catalog.
- Bugs found by tests then fixed: SnapshotJson missing commas (canonical +
  round-trip tests are the regression tests); wrong test expectation for ة→ه.
- Gates: Category=Domain + Category=Arch green (46/46); DecisionRules /
  ArabicText / LocalDate covered branch-by-branch (review-asserted ≥95%).

## Phase 0 — Preflight & scaffold (done, 2026-10-07)

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
