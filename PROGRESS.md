# PROGRESS.md

## Phase 4 — UI shell, read-only (done)

- Styles: Colors (tokens per §7.3), Typography (font stack + sizes), Icons
  (StreamGeometry), Controls (button/input/chip heights, double focus ring,
  wide scrollbars). RTL enforced by lint on every window/user-control root.
- Shell: MainWindow (top bar / persistent sidebar / content host / status bar,
  whole-page vertical scroll for 125–150 % zoom), TopBar, SplashWindow.
- Views: RegisterView (search, chips with counts, action bar, wide toggle,
  sortable virtualized DataGrid, TODAY/OVERDUE row tints, status chips),
  TodaySidebar (header, progress, OVERDUE block, pending/decided cards, print
  + preview buttons). Controls: StatusChip (colour + icon + word), ToastHost.
- App wiring: App.xaml merges dictionaries, SoftwareOnly rendering, ar-EG with
  Western digits, CompositionRoot builds all services, splash → main.
- Bugs found by rendering/screens then fixed: ProgressBar TwoWay on read-only
  property, AppFont lookups outside app resources, ShutdownMode killing the
  render loop, LayoutTransform binding not inheriting DataContext.
- Gates: UI 13/13, Screens 1/1 (PNG review), lint (no text <18, RTL roots,
  button/input heights, contrast pairs), full suite 92/92 incl. 300+300 kills.
- PNGs reviewed: reg-0/3/200 rows at 1366, 1024 and 150 %.

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

## Phase 6 - Printing (done, 2026-10-07)

- `Printing/`: `RosterPaginator` (pure geometry: pageH 703, table header 36,
  page-1 header 132, running title 30, footer 30, summary 26, signature 118),
  `RosterDocumentBuilder` (A4 landscape 1122.5x793.7 DIP, Tahoma, black on
  white, 1pt borders, code columns م/الاسم/رقم القضية/التهمة/المحكمة-الدائرة/
  مكان الحجز/ملاحظات = 6/22/13/21/17/10/11 %), `PrintService` (A4 landscape
  `PrintTicket`, direct print, `roster_prints` logging, `PrintOutcome` result).
- `Application/Ports/IPrintService.cs` + `IRosterPrintLog.cs`: UI reaches
  printing only through the port (SPEC §4.5 layering), preview window lives in
  UI, `PrintService` never references UI.
- `SqliteRosterPrintLog`: append-only roster_prints rows (date, count, ids,
  operator). `PrintPreviewWindow` with custom big buttons (طباعة / اختيار طابعة
  تانية / إغلاق); sidebar print + preview wired through `UiCoordinator`.
- Framework notes: this profile has no `PageContent.Page` (use `.Child`), no
  `FixedDocument.PageWidth/PageHeight`, no `DocumentPaginator.WriteTo`, and
  `XpsDocumentWriter` lives in `System.Windows.Xps` and is not IDisposable;
  `FixedPage.FlowDirection` getter always returns LTR, so RTL is asserted on
  the page children instead.
- Gates: `Category=Printing` 9/9 (paginator invariants for N=0..200 with random
  row heights: no overflow, header row repeated, signature exactly once on the
  last page, row order and count preserved; roster set R(D) incl. decided and
  excl. undone/REOPEN; per-court counts sum to the roster; print log append);
  roster PNGs for N = 1, 12, 40 rendered and reviewed (artifacts/roster);
  full suite 114/114 green.
## Phase 7 - Resilience & platform (done, 2026-10-07)

- `Platform/`: `SingleInstanceGuard` (named mutex, second copy exits 5),
  `FileLogger` (daily `logs/app-YYYYMMDD.log`, keeps 30), `CrashHandler`
  (AppDomain + dispatcher unhandled -> log, Arabic notice, exit 9),
  `PowerEvents` (session ending / power / display, exit snapshot on logout),
  `DesktopShortcut` (WScript.Shell late-bound .lnk), `InputLanguageAssist`
  (Arabic-Indic digit normalization on input).
- `Application/BackupScheduler`: Tier-B rolling timer (30 min default),
  Tier-C exit snapshot, `SessionEnding` hook; exit snapshot skipped when
  `change_seq` unchanged.
- UI: `UsbBackupDialog`, `RestoreDialog`, `ClockWarningDialog`,
  `BackupSettingsView` (USB + restore + direct-print + postpone offsets),
  `HelpWindow`; wired from `App.xaml.cs` (guard, logger, crash handler,
  scheduler, power events, recovery notice, clock sanity check).
- Bug found by the scripted restore scenario: `RestoreSwap.Install` opens a
  second connection on the live DB, which blocks forever while the gateway
  holds `locking_mode=EXCLUSIVE`. Restore now closes the DB first and the app
  exits afterwards; the test asserts a roster restored from a snapshot.
- Framework notes: `SystemEvents` lives in `Microsoft.Win32`; `InputScope` and
  `XmlLanguage` are not usable in this profile, so the digit mapping is done in
  `PreviewTextInput` instead.
- Gates: `Category=Resilience` 9/9 (auto snapshot verified, ring <= 10, exit
  snapshot once per change_seq, session ending, restore round-trip keeps the
  roster and invariants, file logger, single instance, digit normalization,
  clock regression flagged); full suite 123/123 green.
## Phase 8 - Packaging (done, 2026-10-07)

- `Bootstrap/NativeExtractor.cs`: hash-verified extraction of the embedded
  `vendor/x86/SQLite.Interop.dll` (SHA-256 pinned in code) to
  `<home>/native/x86`, `SetDllDirectory`, plus a copy next to the exe.
  The exe-side copy is required: `System.Data.SQLite` builds the native path
  from the managed assembly location, which is empty when the assembly is
  loaded from an embedded resource (fixed the selftest crash with
  `ArgumentNullException path1`).
- `Bootstrap/SelfTest.cs`: `--selftest` extracts natives, opens a home, runs
  quick_check + invariants I1-I6, seeds lookups, create/decide/undo round trip,
  verified snapshot, re-checks invariants, prints timings, exits 0.
- `Program.cs`: real `--selftest` / `--home` argument handling.
- `build.cmd` green end-to-end: restore (offline feed) -> 123/123 tests ->
  publish -> selftest from an empty temp folder -> `Make-Dist.ps1`.
- Gates: `build.cmd` exit 0; `--selftest` exit 0 from an empty folder in
  474-499 ms; `dist\CourtDesk.exe` = 2,262,528 bytes (2.2 MB) with
  `.sha256` = 8efd00a1fb28fbbcc473707bb0b895c9ba89d4dda96482f67c090e8f328c8026;
  kit with PREREQUISITES.txt + README produced.