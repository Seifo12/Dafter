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
## Phase 9 - Hardening & docs (done, 2026-10-07) - FINAL

- `README.md`: prerequisites, build, deploy, target PCs, verify a deployment,
  project layout, test commands and categories.
- `docs/اقرأني.txt` (copied to `dist\kit` by `Make-Dist.ps1`): the Arabic
  quick guide generated from the same wording as the in-app help.
- `QA-CHECKLIST.md`: 11 sections of manual acceptance mapped to AC ids
  (launch, records, decisions, search/grid, sidebar, printing, backups,
  kill tests, accessibility, logging, deliverable).
- `DECISIONS.md`: final phase decisions appended (addendum precedence, 3-tier
  layout, grid chrome, print layering, atomic schema stamp, restore lock order,
  native placement, digit handling).
- PNG review of the current build (artifacts/screens, artifacts/audit,
  artifacts/roster). Two defects found and fixed in review: the decided card
  showed the raw decision code instead of its Arabic label, and the decided
  card height clipped its second line (56 -> 76).
- No dead code found: every production type is referenced by the composition
  root, a view or a test.
- Gates: full suite 123/123 green; `build.cmd` exit 0; `--selftest` exit 0 in
  467 ms from an empty folder; `dist\CourtDesk.exe` = 2,262,528 bytes,
  sha256 e397729ac04b0158434b3ce741eabed83ce67c765a8522bc5d4e5482b2f15a39.

## Project status: all phases 0-9 complete

Phases: 0 scaffold, 1 domain, 2 persistence, 3 application, 4 UI shell,
4R UI addendum rework, 5 forms and wizard, 6 printing, 7 resilience and
platform, 8 packaging, 9 hardening and docs. All phase gates recorded above
pass; the product is a single 2.2 MB x86 exe that runs offline.
## Hotfix - field-to-property binding failures (done, 2026-10-07)

Found by real use, reproduced in `DialogInteractionTests` (click every offset
button, open the card after a decision, assert dropdown text):
- `OffsetButton.Days` was a public field: `Tag="{Binding Days}"` never
  resolved, so wizard offset clicks did nothing. Now a property; the chosen
  offset is highlighted (bold + full opacity) and `ChosenLine` updates.
- `TimelineEntry.Title/Detail` were fields: the card history bindings never
  resolved, so the timeline rendered blank. Now properties.
- Same latent failure converted everywhere it could bite: `CalendarCell`,
  `HistoryRow`, `DayCountRow`, `FieldProblem`, `BackupRow`, `LookupItem`,
  `WizardStage` (its `Number`/`Value` bindings were dead too).
- `LookupItem.ToString()` returns the name: all lookup dropdowns showed the
  class name before; the raw name had even been saved into `CaseType`.
- Card bottom buttons overflowed the dialog (860 px in 788 px): MinWidths cut
  to fit. Schedule grid columns are star-sized now. Edit dialog has a fixed
  height with MaxHeight instead of unbounded SizeToContent.
- `ValSummary` ended with a dangling "because:"; it now carries the first
  error. Fixed 8 corrupted Arabic strings in `Strings.cs`.
- Gates: 126/126 green; `build.cmd` exit 0; real-app screenshot verified.

## Phase 4R - layout addendum D1-D10 + sidebar D7/D8 (done, 2026-10-08)

- D1-D3 header/nav final: `NavTab` padding TemplateBinding, MinWidth 110,
  radius 24, bold; `NavTabCompact` 18px; `HeaderUtilityButton` 48 high dark
  Border; TopBar single row, brand never trimmed, station line 20px and hidden
  when narrow.
- D4-D6 register: search box 60 high with inner clear button (40 high, only
  when text, hint hides); chips horizontal scroll, `FilterChip` 40 high bold
  no-trim; action bar borderless 5-button Grid with trailing spacer + void;
  root margin 0; WideToggle moved to the status bar (Quiet, 36 high).
- D9-D10 chrome: status bar 36 high (records + exit + USB; unit appended in
  `SetStatus`); body margin 12; dead `SecondaryToggle` style deleted.
- D7 session cards vertical: text on top, full-width record button below
  (56 pending / 48 overdue, MinWidth 120); name click opens the case card
  (`CardRequested` wired in `MainWindow`); `SidebarTab` MinWidth 120.
- D8 decided strip: vertical list replaced by a horizontal `DecidedStrip`
  (exempted from the audit's horizontal-overflow rule by design); per-card
  undo button removed; "+N" (`SideMoreDecidedPlus`) + show-all/show-less
  toggle, collapsed to 5 by default.
- D14-D16 verified already in place: central `DataChanged` ->
  `RefreshAfterDataChanged` (`SearchIndex.RefreshIds`/`ReplaceRow` +
  `RefreshAll`); tab texts set after cards are filled; `AssertViewsMatchDb`
  + 7 `RefreshPathTests` + 4 `DialogInteractionTests`.
- Verification: `LayoutAudit` 12/12 scenarios, full suite 136/136 green;
  screens re-rendered into `artifacts/screens`.
## FIX-ROUND-2 - defect table (done, 2026-10-08)

Before images: the user's own screenshots (old `dist` build) + `artifacts/screens/*`.
After images: `artifacts/round2/*.png`, produced by the **published exe**
(`CourtDesk.exe --screenshots artifacts/round2`, new CLI mode).

| ID | Before | After | Verified by |
|---|---|---|---|
| D1 title trimmed | «سجل المحبوسين وا…» | full title, no MaxWidth/trimming | `r2-main-1366.png`, `LayoutAudit` |
| D2 nav labels touching | labels on pill edge | `NavTab` binds `Padding`, MinWidth 110, radius 24 | `r2-main-1366.png` |
| D3 utility buttons bare | bare bold text | `HeaderUtilityButton` on-dark button | `r2-main-1366.png` |
| D4 search «مسح» outside | huge secondary button | inline 40-high clear inside the box | `r2-main-1366.png` |
| D5 chips truncated / lost wide toggle | «بدون ميعاد جل…» + blank pill | auto-width chips on one row; toggle moved to status bar | `r2-main-1366.png`, `TrimAudit` |
| D6 «إلغاء السجل» floating | plain bold text | `DangerQuietButton` in the trailing spacer column | `r2-main-1366.png` |
| D7 grid chrome | stray scrollbar fragments | app-wide 20px `ScrollBar` template, state header aligned to chip | `r2-main-1366.png`, `TemplateAudit` |
| D8 sidebar names/cases trimmed | «متهم رقم 102…» | name wraps (no trim), case + 120x44 record button, min 92 | `r2-main-1366.png` (3 full cards), `SessionCardButtonsShowFullLabel` |
| D9 status bar jumbled | mixed text on white | 36-high tonal strip, three blocks + wide toggle | `r2-main-1024.png` |
| D10 misaligned bottoms | sidebar/content ended at different heights | body `*` row, 12px margin, both columns stretch | `r2-main-1366.png`, `LayoutAudit` |
| D11 wizard footer/clipping | «التالي» mid-row, cut tick-box | pinned footer (Next → Back → spacer → Quiet cancel), content in `ScrollViewer`, selected offset = Primary | `r2-wiz-1/2/3.png`, `OffsetButtonsChangeTheChosenDate`, `FridayOffsetShowsWeekendTickBoxFullyVisible` |
| D12 edit dialog | two ragged columns, cut «+ إضافة» | single 640px column, same-row add buttons, custom `ComboBox`/`TextBox` | `r2-edit.png`, `DialogClipTests` |
| D13 default chrome | English `DocumentViewer` chrome, default combos | custom ComboBox/TextBox/CheckBox/ToolTip/ScrollBar | `r2-preview.png`, `TemplateAudit`, `PreviewWindowUsesOwnPageImagesNotDocumentViewer` |

### Runtime defects found from the user's screenshots
| Symptom | Root cause | Fix | Test |
|---|---|---|---|
| "حدث انقطاع في الكهرباء" on every start | `clean_shutdown=1` was never written, so every open counted as unclean | `DbGateway.Close` stamps it; notice only for real recovery/invariant warnings | `CleanExitDoesNotReportUncleanOnReopen`, `UncleanExitIsNoticedOnce` |
| register/sidebar stale until restart | `Main.Index` was never wired, so `RefreshAfterDataChanged` refreshed an empty index | `CompositionRoot` sets `Main.Index` | `CompositionRootDatabaseChangeRefreshesUiWithoutCrash` |
| crash after edits (`CollectionView ... thread different`) | `DataChanged` invoked UI collections inline on the DB thread when no marshal context existed | gateway queues instead of inlining; `CompositionRoot` always supplies a dispatcher context; `MainViewModel.RefreshAll` marshals | same test |
| hang when the notification re-entered the gateway | re-entrant `Read` inside `Write` blocked the single DB thread forever | re-entrant reads run inline on the DB thread, re-entrant writes fail fast | `ReadInsideDataChangedDoesNotDeadlock` |
| «معاينة» unexpected error / blank preview | `DocumentViewer` default chrome + unreliable headless render | own page-image host; preview renders the real roster | `PreviewWindowShowsUserLikeRoster`, `PreviewWindowUsesOwnPageImagesNotDocumentViewer` |
| no visual separation between regions | grid area had no container, status bar blended into the canvas | card-framed grid + tonal status bar | `r2-main-1366.png` |

Gates: full suite **147/147 green**; `LayoutAudit` 12/12; new
`TrimAudit`, `DialogClipTests`, `TemplateAudit`; `--screenshots` mode renders
9 real-app screens into `artifacts/round2/` with no errors.
