# SPEC.md — CourtDesk · «سجل المحبوسين والجلسات»

**Master Specification & Architectural Blueprint — v1.0**
**Audience:** an autonomous coding agent that will implement, verify and package the whole application with **no human available for questions**.
**Language policy:** this document is English. Every user-facing label, message and printed string is **Egyptian Arabic** (catalog in Appendix A).

---

## 0. How the agent must use this document

### 0.1 Authority and conventions
- **MUST / SHOULD / MAY** are used as in RFC 2119.
- Anything tagged **[FIXED]** is a final decision. Do not reopen it, do not "improve" it, do not substitute alternatives.
- Where this document is silent, choose the **simplest** option that satisfies Section 1, then append one line to `DECISIONS.md` (`date · decision · reason`). Never ask the human. Never ship `TODO`/`FIXME`.
- Code, identifiers and comments are English. Arabic literals exist **only** in `src/CourtDesk/Resources/Strings.cs` (enforced by a test, §9).

### 0.2 Operating rules
1. Execute the phases of §10 **in order**. A phase is complete only when **all its gates pass**. Do not start phase N+1 before that.
2. `git init` at Phase 0; commit at the end of every phase (`phase-N: <title>`); keep `PROGRESS.md` (phase, date, gate results, open issues).
3. Dependencies are exactly those in §10.2. **No network code of any kind** in the product (enforced by a test).
4. If a hard prerequisite is missing (host is not Windows, .NET SDK missing, vendor download impossible), **stop** and write `BLOCKED.md` stating precisely what is missing. Do not switch stacks, do not mock the missing piece.
5. Prefer boring, explicit code: no reflection-driven frameworks, no DI container, no code generation beyond what WPF/SDK already does.
6. Every bug found during verification gets a regression test before it is fixed.

### 0.3 Glossary
| English | Arabic | Meaning |
|---|---|---|
| Defendant / detainee | متهم / محبوس | One person **in one case** = one record |
| Session | جلسة | A court hearing date for that record |
| Court / Circuit | محكمة / دائرة | Circuit belongs to exactly one court |
| Roster | كشف الترحيل | A4 list handed to the escort convoy |
| Escort | قوة الترحيل | Police force that takes detainees to court |
| Decision | قرار المحكمة | Outcome recorded when the escort returns |
| Adjournment | تأجيل / تجديد حبس | Decision that sets a new session date |
| Custody location | مكان الحجز | Where the detainee is held |

---

## 1. Product definition

### 1.1 Purpose
A single-user, fully offline Windows desktop application used by one elderly police warrant officer (ضابط/أمين شرطة) in a local police station (قسم شرطة) to: keep detainee/defendant records, always see **who must go to court today**, print the convoy roster in one click, and record each court decision in the afternoon (renew detention, postpone, bail, release, judgment) while keeping a tamper-evident history — with **zero data loss** even if the power is cut at any instant.

### 1.2 Persona → design rules
| Trait | Consequence (binding) |
|---|---|
| Virtually zero technical literacy | No menus, no settings needed to start, no jargon, no English, no icon-only controls, no hidden gestures (no right-click-only, hover-only, drag-and-drop, double-click-only). Every action is a big labeled button. |
| Elderly (vision, motor precision) | Large type, high contrast, large targets, forgiving flows, confirmation by full sentences, undo. (§7.2) |
| Frequent power cuts | ACID storage, rolling snapshots, startup self-healing. (§4, §6.6) |
| Older hardware / Windows 7–11 | .NET Framework 4.6.2 + WPF forced to software rendering, tiny memory footprint, no GPU dependence. |
| Cannot call support quickly | The app explains every problem in plain Egyptian Arabic and fixes what it can by itself. |

### 1.3 Non-negotiable requirements
| ID | Requirement | Verified by |
|---|---|---|
| R-01 | 100% offline; zero network code, zero online assets/CDNs/fonts | AC-01 |
| R-02 | Runs on Windows 7 SP1 → Windows 11, 32/64-bit, ≥2 GB RAM, ≥1024×768 | AC-02 |
| R-03 | Distributed as **one** `CourtDesk.exe`; user data lives outside the exe | AC-03 |
| R-04 | Pulling the power cord at any instant never corrupts data and never loses a **committed** operation | AC-04, AC-05 |
| R-05 | Native Arabic RTL layout; all labels Egyptian Arabic | AC-06 |
| R-06 | Home view = Excel-like grid + instant search (<50 ms per keystroke at 20,000 records) | AC-07 |
| R-07 | Persistent "Today's Sessions" panel computed **strictly from the local machine date** | AC-08, AC-09 |
| R-08 | One-click A4 roster print with header, table, signature blocks | AC-10 |
| R-09 | Adjournment/decision workflow is atomic and appends to an immutable audit trail | AC-11, AC-12 |
| R-10 | Nothing destructive: soft delete, undo of the last change, append-only history | AC-13 |
| R-11 | Silent backup on exit, rolling safety snapshots, one-click USB export, one-click restore | AC-14, AC-15 |
| R-12 | Accessibility sizing standards of §7.2 are enforced by automated tests | AC-16 |
| R-13 | Cold start to usable window ≤ 3 s on baseline hardware (dual-core 2 GHz, 2 GB, 5400 rpm HDD, Win 7) with a visible splash ≤ 400 ms | AC-17 |
| R-14 | The user is never asked to make a technical choice; defaults are always safe | AC-18 |

### 1.4 Out of scope (do **not** build)
Network/multi-user/sync, login/roles, database encryption (rationale: key loss = total data loss; use Windows account password / BitLocker, see §11), photos/fingerprints/scans, SMS/e-mail, Hijri calendar, auto-update, telemetry, reports other than the roster and CSV export, Excel (.xlsx) import (CSV only, optional Phase 9), installer/MSI.

### 1.5 Scale assumptions
≤ 20,000 defendant records, ≤ 200,000 audit events, DB ≤ 100 MB. Design for these; test with 20,000 / 200,000.

---

## 2. Technology stack **[FIXED]**

### 2.1 Decision
| Layer | Choice |
|---|---|
| Language | **C# 10** (`LangVersion 10.0`, nullable annotations off) |
| Runtime | **.NET Framework 4.6.2** (`net462` target; runs unchanged on 4.6.2 → 4.8.x) |
| GUI | **WPF**, hand-rolled MVVM (`ObservableObject`, `RelayCommand`), manual composition root. Render mode forced to **software** |
| Database | **SQLite 3.41.x** via **System.Data.SQLite 1.0.118** (x86 build). **WAL + `synchronous=FULL` + `locking_mode=EXCLUSIVE`**, single connection |
| Packaging | **One `CourtDesk.exe` (x86)**; managed SQLite assembly and native `SQLite.Interop.dll` are embedded resources, loaded by a hand-written bootstrap (§3.5) |
| Printing | WPF `FixedDocument` → WPF/XPS print path (`System.Printing`, `ReachFramework`); custom paginator; no report library |
| JSON | In-box `System.Web.Script.Serialization.JavaScriptSerializer` for reading; hand-written canonical writer for snapshots |
| Tests | xUnit 2.x on `net462` + a console crash harness |
| Build | .NET SDK 8.x on a Windows host, dependencies vendored for offline repeatability |

### 2.2 Why this stack meets each constraint
- **Runtime stability on low-end Windows:** .NET Framework is part of the OS on Windows 10/11 and the most widely deployed managed runtime on Windows 7; WPF is mature; forcing software rendering removes GPU-driver variance on old machines. x86 process runs on both 32-bit and 64-bit Windows.
- **Native RTL/Arabic:** WPF uses DirectWrite-class text shaping; `FlowDirection="RightToLeft"` mirrors layout, `DataGrid`, scrollbars and popups natively; Arabic contextual shaping and bidi are correct out of the box. Printing through `FixedDocument` keeps identical shaping on paper.
- **Zero online dependencies:** all fonts are OS fonts (Tahoma → Segoe UI → Arial), icons are vector `StreamGeometry`, no CDN, no web view.
- **Single binary:** only one third-party binary pair exists (SQLite). Both are embedded; no other DLL is shipped.
- **Corruption resistance:** SQLite WAL with full sync is the most field-proven embedded ACID store; the architecture adds rolling snapshots and self-healing on top (§4, §6.6).
- **Agent verifiability:** unit/integration/crash tests and screenshot rendering (`RenderTargetBitmap`) are scriptable on the build host, so the agent can prove behaviour without a human.

### 2.3 Rejected alternatives (do not revisit)
| Option | Reason rejected |
|---|---|
| Delphi / Lazarus + SQLite | Fastest start, but Arabic bidi in grids/printing is least predictable, scarce tooling for an autonomous agent to verify visually, higher defect risk. |
| Python 3.8 + PySide2/Tk + PyInstaller | Win7 forces Python 3.8/Qt5; one-file unpack takes 3–10 s on old HDDs, 60–120 MB, frequent antivirus false positives. |
| Electron | 150 MB+, 300 MB+ RAM, slow start, Win7 dead-ends at Electron 22. |
| Tauri / WebView2 | WebView2 runtime unsupported on Win7 and must be installed offline. |
| .NET 6/8 self-contained single-file | .NET 8 does not support Win7; .NET 6 is end-of-life; 70–150 MB exe. |
| Go / Rust GUIs (Fyne, egui, Slint) | Arabic shaping/RTL immature or missing. |
| MS Access / Excel VBA | Corrupts on power loss; not a single exe; licensing. |
| ESENT or LiteDB instead of SQLite | ESENT needs a hand-built ISAM layer (high defect risk for an agent); LiteDB has far less power-loss field history. |

### 2.4 Honest trade-offs and mitigations
| Trade-off | Mitigation |
|---|---|
| Cold start is 1–3 s, not "native instant" | Splash within 400 ms; `ProfileOptimization` (multicore JIT); no reflection/IoC/serializer warm-up; lazy-load printing and backup code; budgets enforced in AC-17. |
| .NET 4.6.2+ must exist on the PC | Inbox on Win10 ≥ 1607 and Win11. Win7 SP1 / Win8.1 / Win10 < 1607 need the **.NET Framework 4.8 offline installer** (bundled in the delivery kit, §10.5). On a bare Win7 SP1, KB4474419 and KB4490628 are prerequisites of that installer. |
| Windows-only build host | Documented in §2.6; Phase 0 verifies it. |
| Native DLL must be extracted at first run | Extracted to a fixed per-user folder, SHA-256-verified on every launch, never to random temp names (reduces AV heuristics). |

### 2.5 Compatibility matrix
| OS | .NET 4.6.2+ | Supported |
|---|---|---|
| Windows 11 | inbox (4.8.1) | ✔ |
| Windows 10 ≥ 1607 | inbox | ✔ |
| Windows 10 1507/1511, Windows 8.1 | install 4.8 | ✔ with kit |
| Windows 7 SP1 | install 4.8 | ✔ with kit |
| Windows 7 RTM, Vista, XP | — | ✘ (README tells user to install SP1) |

### 2.6 Build-host prerequisites (checked in Phase 0)
Windows 10/11 x64; **.NET SDK 8.0.x**; ≥ 3 GB free disk; internet **only once** to run `tools\Fetch-Vendor.ps1`, after which every build is offline-repeatable (vendored binaries + local NuGet feed under `vendor\`).

---

## 3. Architecture

### 3.1 Repository layout
One shipped project (so only SQLite needs embedding) with strict internal layering, plus tests and tools.

```
CourtDesk/
├─ SPEC.md  PROGRESS.md  DECISIONS.md  README.md  BLOCKED.md(only if blocked)
├─ build.cmd  build.ps1                 # single entry point (§10.3)
├─ CourtDesk.sln
├─ nuget.config                         # local feed ./vendor/packages first, no nuget.org during build
├─ vendor/
│  ├─ managed/System.Data.SQLite.dll    # 1.0.118 net46 build
│  ├─ native/x86/SQLite.Interop.dll     # matching native build
│  ├─ packages/                         # offline NuGet feed (reference assemblies, xunit, test sdk)
│  ├─ MANIFEST.sha256
│  └─ README.txt
├─ tools/
│  ├─ Fetch-Vendor.ps1                  # one-time online fetch + hash manifest
│  ├─ Make-Dist.ps1                     # hash, kit assembly
│  ├─ CrashHarness/                     # console exe linking Persistence+Domain sources
│  └─ RenderScreens/                    # renders views/roster to PNG for visual QA
├─ src/CourtDesk/
│  ├─ CourtDesk.csproj  app.manifest  app.config  App.xaml  App.xaml.cs
│  ├─ Program.cs                        # real entry point (no SQLite types referenced here)
│  ├─ Bootstrap/       EmbeddedAssemblyLoader.cs NativeSqliteLoader.cs SingleInstanceGuard.cs
│  │                   CrashHandler.cs CompositionRoot.cs StartupSequence.cs SelfTest.cs
│  ├─ Domain/          # pure logic, no I/O, no WPF, no SQLite
│  │   Defendant.cs DefendantSnapshot.cs DefendantStatus.cs DecisionCode.cs DecisionRequest.cs
│  │   DecisionRules.cs ValidationResult.cs LocalDate.cs ArabicText.cs ArabicDateFormat.cs
│  │   SnapshotJson.cs DisplayState.cs IClock.cs SystemClock.cs
│  ├─ Application/     # use-cases; depends on Domain + interfaces it owns
│  │   DefendantService.cs DecisionService.cs UndoService.cs TodayService.cs TodaySnapshot.cs
│  │   ScheduleService.cs SearchIndex.cs LookupService.cs SettingsService.cs BackupService.cs
│  │   RosterService.cs ClockSanityService.cs IntegrityService.cs CsvExportService.cs
│  │   ImportService.cs(optional)  Ports/*.cs (IDefendantStore, IEventStore, ILookupStore,
│  │   ISettingsStore, IBackupEngine, IDriveProvider, IPrintService, ILogger)
│  ├─ Persistence/     # only place that touches System.Data.SQLite
│  │   DbGateway.cs DbPaths.cs SqliteBootstrap.cs MigrationRunner.cs IntegrityChecker.cs
│  │   SqliteDefendantStore.cs SqliteEventStore.cs SqliteLookupStore.cs SqliteSettingsStore.cs
│  │   SqliteBackupEngine.cs RestoreSwap.cs SeedData.cs Migrations/001_initial.sql
│  ├─ Printing/        RosterModel.cs RosterPaginator.cs RosterDocumentBuilder.cs
│  │                   PrintService.cs PrintPreviewWindow.xaml(.cs)
│  ├─ Platform/        UsbDriveProvider.cs DesktopShortcut.cs Win32.cs FileLogger.cs PowerEvents.cs
│  │                   InputLanguageAssist.cs
│  ├─ UI/
│  │   Shell/          MainWindow.xaml TopBar.xaml SplashWindow.xaml
│  │   Views/          RegisterView TodaySidebar ScheduleView HistoryView BackupSettingsView HelpView
│  │   Dialogs/        DefendantEditDialog DecisionWizardDialog DefendantCardDialog ConfirmDialog
│  │                   MessageDialog UsbBackupDialog RestoreDialog FirstRunDialog ClockWarningDialog
│  │                   RecoveryNoticeDialog LookupAdminDialog
│  │   Controls/       BigButton BigCalendar StatusChip SearchBox ChoiceTile LookupCombo ToastHost
│  │   ViewModels/     (one per view/dialog, no WPF types except ICommand)
│  │   Styles/         Colors.xaml Typography.xaml Controls.xaml Icons.xaml
│  │   Converters/
│  └─ Resources/       Strings.cs        # ALL Arabic text lives here
└─ tests/CourtDesk.Tests/                # xUnit; categories: Domain, Persistence, Crash, Printing, UI, Arch, Selftest
```

### 3.2 Layering rules (enforced by `ArchitectureTests`, a text scan of `using` directives and project references)
| From ↓ may use → | Domain | Application | Persistence | Printing | Platform | UI |
|---|---|---|---|---|---|---|
| Domain | ✔ | ✘ | ✘ | ✘ | ✘ | ✘ |
| Application | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ |
| Persistence | ✔ | ports only | ✔ | ✘ | ✘ | ✘ |
| Printing | ✔ | models only | ✘ | ✔ | ✘ | ✘ |
| Platform | ✔ | ports only | ✘ | ✘ | ✔ | ✘ |
| UI | ✔ | ✔ | **✘** | ✔ (via port) | ✔ (via port) | ✔ |
| Bootstrap | ✔ all (composition root only) | | | | | |

Additional bans: `System.Data.SQLite` outside `Persistence/` and `Bootstrap/`; `System.Net*`, `System.Web` (except `System.Web.Script.Serialization`), `WebClient`, `HttpClient`, `Socket` anywhere; Arabic literals outside `Strings.cs`; `FontSize` < 18 in XAML.

### 3.3 Module responsibilities (single sentence each)
- **Domain** — entities, the decision state machine, validation rules, Arabic text normalization, local-date type, canonical JSON snapshots. Zero I/O.
- **Application** — orchestrates use-cases (create/edit/decide/undo/void, today snapshot, search index, backups, roster model) against ports; owns transactions' *shape*, not their SQL.
- **Persistence** — SQLite connection lifecycle, pragmas, migrations, SQL, integrity checks, snapshot/backup engine, restore swap.
- **Printing** — turns a `RosterModel` into paginated A4 pages; sends to a printer or preview.
- **Platform** — Win32 and OS integrations (removable drives, shortcut, single instance, logging, power/time events, input language).
- **UI** — views, view-models, dialogs, styles; calls Application only.
- **Bootstrap** — assembly/native loading, single instance, crash handler, composition, startup sequence, `--selftest`.

### 3.4 Threading model
- **UI thread (STA):** rendering and input only. Never blocks on disk for more than a frame; shows a "جارٍ الحفظ…" overlay only if an operation exceeds 300 ms.
- **DbThread:** one dedicated thread owns the **only** `SQLiteConnection`. `DbGateway.Run<T>(Func<DbContext,T>)` enqueues work and returns `Task<T>`. No other class touches the connection.
- **Timers** (UI dispatcher, 15 s): day-rollover check, snapshot debounce check, USB-age reminder check. No background threads beyond DbThread.
- After every commit `DbGateway` raises `DataChanged(changeSeq)` on the UI dispatcher.

### 3.5 Startup sequence (`StartupSequence.cs`)
1. `Program.Main` (contains **no** SQLite type references; marked `[STAThread]`): parse args (`--home <dir>`, `--selftest`); register `EmbeddedAssemblyLoader` (`AppDomain.AssemblyResolve` → read embedded `vendor/System.Data.SQLite.dll` → `Assembly.Load(byte[])`); start `ProfileOptimization`; then call `App.Run()` in a separate `[MethodImpl(NoInlining)]` method so the JIT does not touch SQLite types earlier.
2. `SingleInstanceGuard` (named mutex `Local\CourtDesk.SingleInstance`): if another instance runs, signal it to restore/focus its window and exit silently.
3. Resolve data home (`%LOCALAPPDATA%\CourtDesk\`, or `--home` / env `COURTDESK_HOME`); create sub-folders; delete stale `*.tmp`/`*.partial`.
4. `NativeSqliteLoader`: extract `SQLite.Interop.dll` to `<home>\native\x86\` if missing or SHA-256 differs; set env `PreLoadSQLite_BaseDirectory=<home>\native` and `PreLoadSQLite_NoSearchForDirectory=1`; `LoadLibrary` the full path.
5. Show `SplashWindow` (static Arabic text + "برجاء الانتظار…") **before** step 6.
6. `RestoreSwap.ResolvePending()`, then open DB via `SqliteBootstrap` (pragmas, migrations, `quick_check`) with the recovery path of §4.9.
7. Build services (composition root), seed lookups if empty, load `SearchIndex` and `TodaySnapshot`.
8. First-run dialog if `first_run_done = 0`; auto-create desktop shortcut once.
9. `ClockSanityService` check (§6.5); show warning dialog if suspicious.
10. Close splash, show `MainWindow` maximized.
11. If previous run did not shut down cleanly, show `RecoveryNoticeDialog` (after integrity passed).

### 3.6 Cross-cutting rules
- **Clock:** only `IClock` (`Today: LocalDate`, `UtcNow: DateTime`); never `DateTime.Now/Today` elsewhere. Date logic uses the Gregorian **local** date; storage is ISO `YYYY-MM-DD`; timestamps UTC `YYYY-MM-DDTHH:MM:SSZ`.
- **Culture:** set `Thread.CurrentCulture`, `DefaultThreadCurrentCulture` and WPF `Language` to `ar-EG` **but** force Western digits (`NumberSubstitution.Substitution="European"` in the root style) and the Gregorian calendar. Formatting uses `CultureInfo.InvariantCulture` + explicit Arabic month/weekday tables (Appendix A). Never use `ar-SA`/UmAlQura.
- **Logging:** `FileLogger` → `<home>\logs\courtdesk-YYYYMMDD.log`, UTF-8, flushed per line, 30-day retention, **ids only — never names or national IDs**.
- **Settings:** key/value in DB (Appendix B), cached in memory, written through `SettingsService`.
- **Operator name:** `operator_name` setting is stamped on every audit event and printed in the roster signature block.

---

## 4. Persistence, schema and data integrity

### 4.1 Files and folders
Data home `H = %LOCALAPPDATA%\CourtDesk\` (override: `--home` or env `COURTDESK_HOME`; used by tests and `--selftest`).

| Path | Purpose |
|---|---|
| `H\db\courtdesk.db` (+ `-wal`) | The database. Must be on a local fixed NTFS volume. Never copy these files while the app is running. |
| `H\backups\auto\auto-0.db … auto-9.db` | Tier-B rolling safety snapshots (ring of 10) |
| `H\backups\exit\exit-YYYYMMDD-HHMMSS.db` | Tier-C exit backups |
| `H\backups\pre-op\` | `pre-migrate-vN-*.db`, `pre-restore-*.db` |
| `H\quarantine\` | Corrupt files moved aside, never deleted automatically |
| `H\native\x86\SQLite.Interop.dll` | Extracted native engine (hash-verified) |
| `H\tmp\` | Temp files (`*.tmp`, `*.partial`), cleaned at startup |
| `H\logs\` | Logs |
| `H\restore.pending` | Marker used by the crash-safe restore swap (§4.9) |

Startup warns (non-blocking Arabic notice) if the volume is not NTFS (FAT32 is less crash-safe) or free space < 500 MB.

### 4.2 Connection lifecycle and pragmas **[FIXED]**
One connection, opened once, owned by `DbThread`, kept for the app lifetime. Connection string: `Data Source=<path>;Version=3;Pooling=False;Default Timeout=5`. Immediately after `Open()`, execute **in this exact order** and verify every returned value (mismatch ⇒ treat as startup failure):

| # | Statement | Why |
|---|---|---|
| 1 | `PRAGMA locking_mode=EXCLUSIVE;` | Set **before first WAL access**: no shared-memory `-shm` file, a second process gets `SQLITE_BUSY` (friendly message), nobody else can touch the file. |
| 2 | `PRAGMA journal_mode=WAL;` → must return `wal` | One sync per commit; readers never block; crash recovery by WAL replay. |
| 3 | `PRAGMA synchronous=FULL;` | WAL is fsynced on **every** commit ⇒ a committed operation survives power loss. (`NORMAL` can lose recent commits; forbidden.) |
| 4 | `PRAGMA foreign_keys=ON;` → read back `1` | Referential integrity. |
| 5 | `PRAGMA busy_timeout=5000;` | Defensive. |
| 6 | `PRAGMA trusted_schema=OFF;` `PRAGMA cell_size_check=ON;` | Fail fast on malformed pages. |
| 7 | `PRAGMA secure_delete=FAST;` `PRAGMA temp_store=MEMORY;` | Privacy of freed pages; no temp files on disk. |
| 8 | `PRAGMA wal_autocheckpoint=400;` `PRAGMA journal_size_limit=4194304;` | Keep WAL small (≈1.6 MB / 4 MB). |
| 9 | `PRAGMA application_id` must be `0x43444B31` (set on creation) else "not our file" | Prevent opening foreign files. |
| 10 | `PRAGMA user_version` must be ≤ supported version, else message "قاعدة البيانات أحدث من البرنامج" | Forward-only migrations. |
| 11 | `PRAGMA quick_check;` → must return exactly `ok` | Detect corruption at launch. |
| 12 | Run invariant queries I1–I6 (§4.6) | Log violations (see policy there). |

On clean exit: `PRAGMA wal_checkpoint(TRUNCATE)` then close, leaving a single self-contained `.db` file.

### 4.3 Gateway and transaction strategy **[FIXED]**
- All DB access goes through `DbGateway.Run(Func<DbContext,T>)` on `DbThread`. SQL is **parameterized only** (no string concatenation).
- **Reads:** plain `SELECT` in autocommit.
- **Writes:** `DbContext.Write(Func<WriteTx,T>)` executes `BEGIN IMMEDIATE` → work → `COMMIT`. Any exception ⇒ `ROLLBACK`; if the rollback itself fails, close the connection and re-run the open sequence (§4.2) before the next request.
- **Rules inside a write transaction:** no UI calls, no user interaction, no file/network I/O, no `await` on foreign tasks, total time < 50 ms. Read-after-write verification is performed **before** `COMMIT` (§6.2 step 9).
- Every write transaction also executes: `UPDATE app_meta SET value=value+1 WHERE key='change_seq'` and `UPDATE app_meta SET value=<now utc> WHERE key='max_seen_utc'` (cast as needed). This makes "did anything change?" and clock-regression checks trivial.
- Optimistic concurrency: every mutable defendant row carries `row_version`; updates use `WHERE id=? AND row_version=?` and assert `changes()==1`.
- After commit: update the in-memory caches **from the committed result** (not from the request), then raise `DataChanged`.
- Idempotency: audit events carry `request_uuid` (unique). Re-submitting the same request returns the original event and changes nothing (guards double-click and retry).
- Checkpointing: `PASSIVE` when WAL > 4 MB or after 5 idle minutes; `TRUNCATE` on exit.

### 4.4 Schema (migration `001_initial.sql`, executed inside one `BEGIN IMMEDIATE … COMMIT`)
Dates are `TEXT 'YYYY-MM-DD'` (local calendar dates, no time zone). Timestamps are UTC `TEXT 'YYYY-MM-DDTHH:MM:SSZ'`. Names are stored as typed; `*_norm` columns hold `ArabicText.Normalize()` output (§5.5) for duplicate checks and uniqueness.

```sql
PRAGMA application_id = 0x43444B31;   -- 'CDK1'

CREATE TABLE app_meta (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL
) WITHOUT ROWID;
-- keys: schema_created_utc, change_seq (int), max_seen_utc, last_exit_backup_seq,
--       restore_id, last_run_app_version, clean_shutdown ('1' on clean exit, '0' at startup)

CREATE TABLE settings (
  key            TEXT PRIMARY KEY,
  value          TEXT NOT NULL,
  updated_at_utc TEXT NOT NULL
) WITHOUT ROWID;

CREATE TABLE courts (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  name           TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 120),
  name_norm      TEXT NOT NULL,
  sort_order     INTEGER NOT NULL DEFAULT 100,
  is_active      INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1)),
  created_at_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX ux_courts_name ON courts(name_norm);

CREATE TABLE circuits (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  court_id       INTEGER NOT NULL REFERENCES courts(id) ON DELETE RESTRICT,
  name           TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 120),
  name_norm      TEXT NOT NULL,
  is_active      INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1)),
  created_at_utc TEXT NOT NULL,
  UNIQUE (id, court_id),
  UNIQUE (court_id, name_norm)
);

CREATE TABLE custody_locations (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  name           TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 120),
  name_norm      TEXT NOT NULL,
  sort_order     INTEGER NOT NULL DEFAULT 100,
  is_active      INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1)),
  created_at_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX ux_custody_name ON custody_locations(name_norm);

CREATE TABLE postponement_reasons (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  label      TEXT NOT NULL CHECK (length(trim(label)) BETWEEN 1 AND 120),
  label_norm TEXT NOT NULL UNIQUE,
  sort_order INTEGER NOT NULL DEFAULT 100,
  is_active  INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1))
);

CREATE TABLE charge_suggestions (          -- learns typed charges for autocomplete
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  text             TEXT NOT NULL,
  text_norm        TEXT NOT NULL UNIQUE,
  use_count        INTEGER NOT NULL DEFAULT 1,
  last_used_at_utc TEXT NOT NULL
);

CREATE TABLE defendants (
  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
  full_name           TEXT NOT NULL CHECK (length(trim(full_name)) BETWEEN 2 AND 200),
  name_norm           TEXT NOT NULL,
  national_id         TEXT CHECK (national_id IS NULL OR (length(national_id) = 14 AND national_id NOT GLOB '*[^0-9]*')),
  case_number         TEXT NOT NULL CHECK (length(case_number) BETWEEN 1 AND 12 AND case_number NOT GLOB '*[^0-9]*'),
  case_year           INTEGER NOT NULL CHECK (case_year BETWEEN 1950 AND 2100),
  case_type           TEXT NOT NULL DEFAULT '' CHECK (length(case_type) <= 40),
  charges             TEXT NOT NULL DEFAULT '' CHECK (length(charges) <= 1000),
  court_id            INTEGER REFERENCES courts(id) ON DELETE RESTRICT,
  circuit_id          INTEGER,
  custody_location_id INTEGER REFERENCES custody_locations(id) ON DELETE RESTRICT,
  session_date        TEXT CHECK (session_date IS NULL OR session_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
  status              TEXT NOT NULL DEFAULT 'DETAINED' CHECK (status IN ('DETAINED','RELEASED','JUDGED')),
  detained_since      TEXT CHECK (detained_since IS NULL OR detained_since GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
  notes               TEXT NOT NULL DEFAULT '' CHECK (length(notes) <= 2000),
  is_deleted          INTEGER NOT NULL DEFAULT 0 CHECK (is_deleted IN (0,1)),
  row_version         INTEGER NOT NULL DEFAULT 1,
  created_at_utc      TEXT NOT NULL,
  updated_at_utc      TEXT NOT NULL,
  FOREIGN KEY (circuit_id, court_id) REFERENCES circuits(id, court_id) ON DELETE RESTRICT,
  CHECK (status = 'DETAINED' OR session_date IS NULL),     -- released/judged never appear in session lists
  CHECK (circuit_id IS NULL OR court_id IS NOT NULL)
);
CREATE INDEX ix_def_session ON defendants(session_date) WHERE is_deleted = 0 AND status = 'DETAINED';
CREATE INDEX ix_def_status  ON defendants(status, is_deleted);
CREATE INDEX ix_def_case    ON defendants(case_year, case_number);
CREATE INDEX ix_def_name    ON defendants(name_norm);
CREATE TRIGGER trg_def_no_hard_delete BEFORE DELETE ON defendants
BEGIN SELECT RAISE(ABORT, 'defendants are soft-deleted only'); END;

CREATE TABLE session_events (            -- append-only audit trail + undo source
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,   -- AUTOINCREMENT: ids never reused, strictly increasing
  request_uuid          TEXT,
  defendant_id          INTEGER NOT NULL REFERENCES defendants(id) ON DELETE RESTRICT,
  event_type            TEXT NOT NULL CHECK (event_type IN ('CREATED','EDITED','DECISION','UNDO','DELETED','RESTORED','IMPORTED')),
  decision_code         TEXT CHECK (decision_code IS NULL OR decision_code IN
                          ('RENEW_DETENTION','POSTPONE','RELEASE','RELEASE_BAIL','JUDGMENT','REOPEN')),
  session_date_held     TEXT,            -- the session this decision settled
  next_session_date     TEXT,
  reason_text           TEXT CHECK (reason_text IS NULL OR length(reason_text) <= 200),
  bail_amount           INTEGER CHECK (bail_amount IS NULL OR bail_amount >= 0),
  note                  TEXT CHECK (note IS NULL OR length(note) <= 500),
  court_name_at_event   TEXT,            -- names frozen at event time (courts may be renamed later)
  circuit_name_at_event TEXT,
  before_json           TEXT,            -- NULL only for CREATED / IMPORTED
  after_json            TEXT NOT NULL,
  undoes_event_id       INTEGER REFERENCES session_events(id),   -- set on UNDO rows
  undone_by_event_id    INTEGER REFERENCES session_events(id),   -- set once on the original
  operator_name         TEXT NOT NULL,
  occurred_at_utc       TEXT NOT NULL,
  occurred_local_date   TEXT NOT NULL,
  CHECK ((event_type = 'DECISION') = (decision_code IS NOT NULL)),
  CHECK (event_type <> 'DECISION' OR session_date_held IS NOT NULL)
);
CREATE UNIQUE INDEX ux_ev_request ON session_events(request_uuid) WHERE request_uuid IS NOT NULL;
CREATE INDEX ix_ev_def  ON session_events(defendant_id, id);
CREATE INDEX ix_ev_held ON session_events(session_date_held)
  WHERE event_type = 'DECISION' AND undone_by_event_id IS NULL;
CREATE INDEX ix_ev_time ON session_events(occurred_at_utc);

CREATE TRIGGER trg_ev_no_delete BEFORE DELETE ON session_events
BEGIN SELECT RAISE(ABORT, 'audit rows are immutable'); END;

-- The ONLY permitted UPDATE of an audit row: set undone_by_event_id from NULL to a value.
CREATE TRIGGER trg_ev_immutable BEFORE UPDATE ON session_events
WHEN NOT (
      OLD.undone_by_event_id IS NULL AND NEW.undone_by_event_id IS NOT NULL
  AND NEW.id IS OLD.id AND NEW.request_uuid IS OLD.request_uuid AND NEW.defendant_id IS OLD.defendant_id
  AND NEW.event_type IS OLD.event_type AND NEW.decision_code IS OLD.decision_code
  AND NEW.session_date_held IS OLD.session_date_held AND NEW.next_session_date IS OLD.next_session_date
  AND NEW.reason_text IS OLD.reason_text AND NEW.bail_amount IS OLD.bail_amount AND NEW.note IS OLD.note
  AND NEW.court_name_at_event IS OLD.court_name_at_event AND NEW.circuit_name_at_event IS OLD.circuit_name_at_event
  AND NEW.before_json IS OLD.before_json AND NEW.after_json IS OLD.after_json
  AND NEW.undoes_event_id IS OLD.undoes_event_id AND NEW.operator_name IS OLD.operator_name
  AND NEW.occurred_at_utc IS OLD.occurred_at_utc AND NEW.occurred_local_date IS OLD.occurred_local_date)
BEGIN SELECT RAISE(ABORT, 'audit rows are immutable'); END;

CREATE TABLE roster_prints (             -- append-only: proof of what was handed to the convoy
  id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  roster_date        TEXT NOT NULL,
  printed_at_utc     TEXT NOT NULL,
  operator_name      TEXT NOT NULL,
  defendant_count    INTEGER NOT NULL,
  defendant_ids_json TEXT NOT NULL
);
CREATE TRIGGER trg_rp_no_update BEFORE UPDATE ON roster_prints BEGIN SELECT RAISE(ABORT,'immutable'); END;
CREATE TRIGGER trg_rp_no_delete BEFORE DELETE ON roster_prints BEGIN SELECT RAISE(ABORT,'immutable'); END;
```

**Snapshot JSON** (`before_json` / `after_json`): canonical, fixed key order, no whitespace, UTF-8:
`id, full_name, national_id, case_number, case_year, case_type, charges, court_id, circuit_id, custody_location_id, session_date, status, detained_since, notes, is_deleted`. Excludes `row_version`, timestamps and `name_norm`. Written by `SnapshotJson.Write()` (hand-written), read with `JavaScriptSerializer`.

### 4.5 Seed data (inserted by `SeedData.cs` after migration, only if the tables are empty; editable by the user)
Needs `ArabicText.Normalize` for `*_norm`, therefore seeded from C#, not SQL. Values are conveniences, not authoritative.
- Courts: `محكمة الجنح`, `محكمة الجنايات`, `النيابة العامة`.
- Custody locations: `حجز القسم`.
- Postponement reasons: `للاطلاع`, `للمرافعة`, `لسماع الشهود`, `لحضور المتهم`, `لتقديم مستندات`, `للنطق بالحكم`.
- Case types (UI combo list, not a table): `جنحة`, `جناية`, `مخالفة`, `إدارية`, `أخرى`.
- Settings defaults: Appendix B.

### 4.6 Invariants (checked by tests always; at startup logged)
| ID | Invariant (SQL-checkable) |
|---|---|
| I1 | Every defendant has exactly one `CREATED` or `IMPORTED` event, and it has the lowest id among its events. |
| I2 | `status <> 'DETAINED' ⇒ session_date IS NULL` (also a CHECK). |
| I3 | **Row/log consistency:** for every defendant, the editable fields in the `after_json` of its highest-id event equal the current row (full_name, national_id, case_number, case_year, case_type, charges, court_id, circuit_id, custody_location_id, session_date, status, detained_since, notes, is_deleted). |
| I4 | Every event with `undone_by_event_id` set is referenced by exactly one `UNDO` event whose `undoes_event_id` points back. |
| I5 | `circuit.court_id = defendant.court_id` whenever `circuit_id` is set (composite FK). |
| I6 | `PRAGMA foreign_key_check` returns no rows. |

Policy: in tests any violation fails the build. At startup a violation is **logged and shown as a non-blocking banner** ("تنبيه: فيه تعارض في بيانات متهم رقم {id}. كمّل شغلك وبلّغ المسؤول") — never an automatic restore (invariants hold by construction; a violation indicates a bug, and a false-positive rollback would lose real data).

### 4.7 Migrations
`PRAGMA user_version` = schema version (001 ⇒ 1). `MigrationRunner` applies embedded `NNN_*.sql` in order, **each in its own transaction**, after writing `pre-migrate-vN-<ts>.db` via the backup engine. Forward-only; a DB newer than the app is refused.

### 4.8 What happens when the power cord is pulled (design proof)
| Moment of power loss | Result at next launch |
|---|---|
| Before `COMMIT` of any write | WAL frames lack a commit record and are ignored ⇒ state is exactly as before the operation. |
| During the `COMMIT` fsync | Either fully committed (recovered from WAL) or fully absent. Never half. |
| During checkpoint | WAL stays authoritative; replay on open. |
| During a Tier-B/C snapshot or a USB copy | Only a `*.tmp` / `*.partial` file is affected; main DB untouched; stale temp files deleted at startup; only files that pass verification and were atomically renamed count as backups. |
| During a migration | Migration is one transaction ⇒ all-or-nothing; a pre-migration backup exists. |
| During a restore swap | Marker-driven rollforward/rollback (§4.9) ⇒ either old or new DB is whole. |
| Disk lies about fsync / hardware fault | `quick_check` fails at startup ⇒ automatic quarantine + restore of the newest verified snapshot (§4.9); loss window ≤ about 1–5 minutes of work. |

### 4.9 Startup self-healing and crash-safe restore
```
OpenDatabaseWithRecovery():
  ResolvePendingRestore()
  try:  Open(); ApplyPragmas(); Migrate(); quick_check()==ok            → proceed
  catch Busy        → blocking message "البرنامج مفتوح في مكان تاني" ; exit
  catch Corrupt|NotADatabase|quick_check!=ok:
      move courtdesk.db(+wal) → quarantine\corrupt-<utc>.db
      candidates = auto ring ∪ exit backups ∪ pre-op backups, sorted by file time desc
      for c in candidates:
          copy c → tmp\verify.db ; open read-only ; quick_check==ok AND tables present AND user_version<=supported
          if good: InstallViaRestoreSwap(c) ; show RecoveryNoticeDialog(c.time, estimatedLossMinutes) ; proceed
      if none good: blocking dialog "مفيش نسخة سليمة" with the file left in quarantine and
                    two big buttons: «ابدأ قاعدة جديدة فاضية» (confirmed twice) / «إغلاق»
```
**Restore swap (also used by the user-initiated restore)** — all renames are same-volume (atomic):
1. Verify the source (as above). Take `pre-restore-<ts>.db` of the current DB (if it opens).
2. `PRAGMA wal_checkpoint(TRUNCATE)`; close the connection.
3. Copy source → `db\courtdesk.db.restoring` (write-through, `Flush(true)`); verify again.
4. Write `restore.pending` containing `restore_id` (GUID), flushed.
5. Rename `courtdesk.db` → `courtdesk.db.replaced`; delete stale `courtdesk.db-wal`.
6. Rename `courtdesk.db.restoring` → `courtdesk.db`.
7. Open; set `app_meta.restore_id = <id>`; delete `courtdesk.db.replaced` and `restore.pending`.

`ResolvePendingRestore()` at startup, if the marker exists: if `courtdesk.db` opens and its `restore_id` equals the marker's → finish step 7; else if `courtdesk.db.replaced` exists → delete any `courtdesk.db`, rename `.replaced` back (roll back); else if only `.restoring` exists → roll back to nothing changed. Then delete the marker.

---

## 5. Domain model and business rules

### 5.1 Statuses and derived display states
Stored `status` (3 values): `DETAINED` محبوس · `RELEASED` مُفرج عنه · `JUDGED` صدر حكم.

Derived `DisplayState` (never stored; `DisplayState.cs`, pure function of row + `today`):
```
is_deleted                                   → hidden everywhere (except trash screen)
status = RELEASED                            → RELEASED
status = JUDGED                              → JUDGED
status = DETAINED and session_date IS NULL  → NO_DATE
status = DETAINED and session_date <  today  → OVERDUE      (a session passed with no decision recorded)
status = DETAINED and session_date == today  → TODAY
status = DETAINED and session_date >  today  → UPCOMING
```
(`DECIDED_TODAY` exists only inside the sidebar: a defendant whose session was today and whose decision is recorded.)

### 5.2 Decisions and the state machine (`DecisionRules.cs`, pure and exhaustively unit-tested)
| Code | Arabic label | Allowed from | Next date | New status | New `session_date` | Extra optional input |
|---|---|---|---|---|---|---|
| `RENEW_DETENTION` | تجديد الحبس | DETAINED | **required** | DETAINED | next date | reason, note, court/circuit change |
| `POSTPONE` | تأجيل الجلسة | DETAINED | **required** | DETAINED | next date | reason, note, court/circuit change |
| `RELEASE` | إخلاء سبيل | DETAINED | forbidden | RELEASED | NULL | note |
| `RELEASE_BAIL` | إخلاء سبيل بكفالة | DETAINED | forbidden | RELEASED | NULL | bail amount, note |
| `JUDGMENT` | صدر حكم | DETAINED | forbidden | JUDGED | NULL | judgment note |
| `REOPEN` | إعادة للحبس | RELEASED, JUDGED | **required** | DETAINED | next date | note |

`session_date_held` (stored on the event) = the defendant's `session_date` at decision time; for `REOPEN` it is the recording date (today).
Soft-delete/restore, create and edit are *events* but not decisions.

### 5.3 Validation rules (authoritative set; each has a test and an Arabic message key)
| ID | Rule | Kind |
|---|---|---|
| V1 | Target exists, is not deleted, and `row_version` equals the version the UI loaded | hard (`err.stale`) |
| V2 | Decision allowed from current status per §5.2 | hard |
| V3 | For decisions from DETAINED: `session_date` is not NULL (`err.no_session`) **and** `session_date ≤ today` (`err.future_session`) | hard |
| V4 | Next date: present iff required; for RENEW/POSTPONE it must be **> held date** (`err.next_after`); for REOPEN it must be **≥ today** | hard |
| V5 | Next date **< today** (catch-up of missed sessions) is allowed only with `confirm_past = true` | confirm |
| V6 | Next date on **Friday or Saturday** (Egyptian weekend; courts normally closed) is allowed only with `confirm_weekend = true` | confirm |
| V7 | Next date more than **120 days** after held date needs `confirm_far = true`; more than **1,100 days** is a hard error (typo guard such as year 2062) | confirm / hard |
| V8 | `bail_amount` only with `RELEASE_BAIL`; integer 0…99,999,999 | hard |
| V9 | `reason_text` ≤ 200 and `note` ≤ 500 chars; `reason_text` only for RENEW/POSTPONE | hard |
| V10 | If court/circuit are overridden: circuit must belong to the court | hard |
| V11 | `operator_name` empty ⇒ stored as `غير مسجل` | auto |
| V12 | `request_uuid` present (32 hex chars) | hard |

`DecisionRules.Validate(snapshot, request, today) → ValidationResult { Errors[], RequiredConfirmations[] }`. The wizard uses `RequiredConfirmations` to show the tick-box; the service refuses to save if a required confirmation flag is false.

### 5.4 Other mutation rules
- **Create:** required full name (2–200), case number (digits only, 1–12), case year (1950…current year + 1). Optional: national ID (exactly 14 digits), court, circuit, custody location, charges, case type, session date, `detained_since` (default today), notes. A new record is `DETAINED`. Writes `CREATED` event.
- **Duplicate warning (soft):** same `name_norm` + same case number + year ⇒ warn with two buttons (open the existing record / add anyway). Not a hard block.
- **Edit:** cannot change `status`. `session_date` is editable only while DETAINED. Any actual change writes an `EDITED` event with before/after. No change ⇒ no event.
- **Soft delete (إلغاء السجل):** sets `is_deleted=1`; needs a reason chip (`إدخال خاطئ` / `مكرر`) and a confirmation; writes `DELETED`. Restore from the trash screen writes `RESTORED`. Hard delete never exists.
- **Undo (§6.3):** only for the **latest** event of that defendant, only types `DECISION` or `EDITED`, only if not already undone. `UNDO`, `CREATED`, `IMPORTED`, `DELETED`, `RESTORED` are not undoable (deletion is reverted by restore; a wrong undo is corrected by recording the decision again).
- **Lookups (courts, circuits, custody locations, reasons):** can be added inline from any combo ("+ إضافة جديد"), renamed or disabled in `LookupAdminDialog`; never hard-deleted; disabled items stay valid for existing records.

### 5.5 Arabic text normalization and search semantics (`ArabicText.cs`)
`Normalize(s)` for **search and duplicate detection only** (display text is stored as typed after trim + whitespace collapse + control-character strip):
1. Unicode NFKC (folds Arabic presentation forms pasted from other sources).
2. Remove tashkeel U+064B–U+065F, U+0670; remove tatweel U+0640.
3. Remove zero-width and bidi control characters (U+200B–U+200F, U+202A–U+202E, U+2066–U+2069, U+FEFF).
4. Map `أ إ آ ٱ → ا`; `ى → ي`; `ؤ → و`; `ئ → ي`; `ة → ه`.
5. Map Arabic-Indic (U+0660–0669) and Persian (U+06F0–06F9) digits to `0-9`.
6. Lower-case Latin; collapse whitespace; trim.

`Joined(s)` = `Normalize(s)` with all spaces removed (so "عبدالله" finds "عبد الله" and vice-versa).
Input fields for numbers (case number, year, national ID, amounts) accept either digit set and convert to Western digits on commit.

### 5.6 Dates
`LocalDate` is a `struct (Year, Month, Day)` with `ToIso()`, `Parse`, `DayOfWeek`, comparison, `AddDays`. Never `DateTime` in Domain signatures. Arabic display: weekday table `الأحد الاثنين الثلاثاء الأربعاء الخميس الجمعة السبت`, month table `يناير فبراير مارس أبريل مايو يونيو يوليو أغسطس سبتمبر أكتوبر نوفمبر ديسمبر`; long form `الثلاثاء 6 أكتوبر 2026`, short form `06/10/2026`, Western digits. Weekend = Friday and Saturday (fixed constant). Wrap numeric runs that sit next to Arabic text (case numbers, dates) with U+200E (LRM) where needed so bidi never reorders them; case numbers are displayed `{number} لسنة {year}`, never as `n/y`.

---

## 6. Core algorithms

### 6.1 "Today's Sessions": detection, caching, refresh
Definitions: `today = IClock.Today` (local machine date, Gregorian). No UTC, no time zones, no network time.

```
BuildTodaySnapshot(today):
  pending   = SELECT d.*, court/circuit/custody names
              FROM defendants d WHERE d.is_deleted=0 AND d.status='DETAINED' AND d.session_date = @today
  decided   = SELECT d.*, e.id, e.decision_code, e.next_session_date
              FROM session_events e JOIN defendants d ON d.id=e.defendant_id
              WHERE e.event_type='DECISION' AND e.decision_code<>'REOPEN'
                AND e.session_date_held=@today AND e.undone_by_event_id IS NULL AND d.is_deleted=0
  overdue   = SELECT ... WHERE is_deleted=0 AND status='DETAINED' AND session_date < @today
              ORDER BY session_date ASC, name_norm
  tomorrow  = SELECT COUNT(*) ... WHERE is_deleted=0 AND status='DETAINED' AND session_date = @today+1
  noDate    = SELECT COUNT(*) ... WHERE is_deleted=0 AND status='DETAINED' AND session_date IS NULL
  order pending/decided by: court.sort_order, court.name_norm, circuit.name_norm, defendant.name_norm
  return TodaySnapshot{ Date=today, Pending, Decided, Overdue, TomorrowCount, NoDateCount,
                        Stamp=(today, changeSeq) }
```
**Cache:** one `TodaySnapshot` in memory keyed by `(LocalDate, changeSeq)`. It is rebuilt (always from the DB, never patched incrementally — no coherence bugs) when any of these occur: app start; `DataChanged` after any commit; day rollover detected by the 15 s timer (`clock.Today != snapshot.Date`); `SystemEvents.TimeChanged`; `PowerModeChanged(Resume)`; main window `Activated`. On rollover while running, show a toast "اليوم اتغيّر — اتحدّثت قايمة جلسات اليوم". An open wizard keeps its target but re-validates with a fresh `today` at save time.

**Progress counter:** `Decided.Count` of `Decided.Count + Pending.Count`.

### 6.2 Atomic adjournment / decision transaction (`DecisionService.Decide`)
```
Decide(req):                                   // req carries request_uuid, defendant_id, expected_version,
                                               // code, next_date?, reason?, note?, bail?, court_id?, circuit_id?,
                                               // confirm_past/weekend/far flags
 1  today := clock.Today ; nowUtc := clock.UtcNow ; operator := settings.operator_name or "غير مسجل"
 2  gateway.Write(tx =>
 3     if events.ExistsByRequestUuid(req.uuid): return AlreadyApplied(events.GetByRequestUuid(req.uuid))
 4     row := defendants.Get(tx, req.defendant_id)                                  -- inside the transaction
 5     vr  := DecisionRules.Validate(row, req, today) ; if vr.HasErrors or vr.UnconfirmedRequired: throw ValidationException(vr)
 6     after := DecisionRules.Apply(row, req, today)                                -- pure; new status/session_date/court/circuit
 7     eventId := events.Insert(tx, { request_uuid, defendant_id, DECISION, code, held, next, reason, bail, note,
                                     court_name_at_event/circuit_name_at_event (names BEFORE any change),
                                     before_json=Write(row), after_json=Write(after),
                                     operator, nowUtc, local_date=today })
 8     n := defendants.UpdateState(tx, after, expectedVersion=req.expected_version, nowUtc)   -- WHERE id AND row_version
       assert n == 1  (else throw ConcurrencyException)
 9     meta.Bump(tx, changeSeq, max_seen_utc=nowUtc)
 10    VERIFY (read back inside the same transaction):
          Write(defendants.Get(tx,id)) == Write(after)   and   events.LatestId(tx,id) == eventId
          else throw InvariantException                                        -- rolls back everything
 11 )                                                                          -- gateway COMMITs (fsync) here
 12 post-commit: refresh SearchIndex row, rebuild TodaySnapshot, raise DataChanged, mark snapshot dirty
```
"All calendars and logs update atomically" is guaranteed structurally: **calendars are queries over `defendants.session_date`** (no second copy of the date exists), and the audit row is written in the same transaction. If power is cut anywhere in steps 2–11 nothing is visible after restart.

### 6.3 Undo (`UndoService.Undo(eventId)`)
```
gateway.Write(tx =>
   ev  := events.Get(tx,eventId)
   require ev.event_type in (DECISION, EDITED) and ev.undone_by_event_id IS NULL
   require ev.id == events.LatestId(tx, ev.defendant_id)                 -- only the latest change can be undone
   row := defendants.Get(tx, ev.defendant_id)
   require Write(row) == ev.after_json                                   -- sanity: nothing drifted
   restored := FromSnapshot(ev.before_json)
   undoId := events.Insert(tx, { UNDO, undoes_event_id=ev.id, before_json=ev.after_json, after_json=ev.before_json, ... })
   defendants.ApplySnapshot(tx, restored, expectedVersion=row.row_version)   -- row_version still increments
   events.MarkUndone(tx, ev.id, undoId)                                  -- the only permitted audit UPDATE (trigger-checked)
   meta.Bump(tx) ; VERIFY Write(defendants.Get(tx,id)) == ev.before_json )
```
UI: offered on the success screen of the wizard and in the case card/history; confirmation sentence shows the decision and the person.

### 6.4 Instant search (`SearchIndex`)
- In memory: one row object per non-deleted defendant with precomputed `Spaced` and `Joined` strings built from `Normalize` of: full name, national ID, case number, year, `"{n}/{y}"`, `"{n} {y}"`, case type, charges, court, circuit, custody location, Arabic status label, notes.
- Query: `Normalize`, split on whitespace into tokens; a row matches iff **every** token is a substring of `Spaced` **or** of `Joined` (ordinal compare). Empty query = no filter.
- Complexity: linear scan with `IndexOf(..., StringComparison.Ordinal)`; target < 10 ms at 20,000 rows; no debounce, evaluated on every keystroke on the UI thread.
- Maintenance: single-row replace after each commit; full rebuild only when lookup names change or on startup.
- Empty result: if the active filter chip is not "الكل" and ≥1 hit exists in "الكل", show `search.none_in_filter` with a button to switch chip.
- Default sort ("urgency"): OVERDUE (oldest first) → TODAY → UPCOMING (soonest first) → NO_DATE → rest by name; clicking a header sorts by that column (toggle direction, arrow shown).

### 6.5 Clock sanity (`ClockSanityService`)
Old PCs reset to a factory date when the CMOS battery dies; since "today" drives everything this is guarded.
```
suspect(now) =
     now.Utc < max_seen_utc − 5 minutes          → reason REGRESSION
  OR now.LocalDate < BuildDate − 1 day           → reason BEFORE_BUILD   (BuildDate = constant stamped at compile time)
```
Checked at startup, at each day-rollover tick and before recording any decision. If suspect: `ClockWarningDialog` with two big buttons: «فتح إعدادات التاريخ» (`Process.Start("control.exe","timedate.cpl")`, re-check when the window is re-activated) and «التاريخ صحيح — كمّل» (allowed for this run only, not persisted). While unresolved, a red banner stays in the top bar.

### 6.6 Backup, snapshot and restore
**Tier A — continuous:** WAL + `synchronous=FULL` (§4).

**Tier B — rolling safety snapshots (silent):** `dirty` is set by `DataChanged`. The 15 s timer takes a snapshot when `dirty && idle ≥ 60 s`, or `dirty && now − lastSnapshot ≥ 5 min` during continuous work. Slots `auto-0…auto-9` used round-robin; each written as temp then atomically swapped in (`File.Replace`).

**Common snapshot procedure** `BackupEngine.Snapshot(destFinalPath)` (runs as one work item on `DbThread`):
```
1  tmp := H\tmp\snap-<guid>.tmp  (same volume as destination when possible)
2  open destination connection (Pooling=False); SQLite online Backup API: source main → dest main (all pages)
3  on dest: PRAGMA journal_mode=DELETE   -- the copy must be a single self-contained file, not WAL-dependent
          PRAGMA synchronous=FULL ; PRAGMA integrity_check == "ok" ; read counts (defendants, events)
4  close dest ; reopen tmp as FileStream and Flush(true)    -- force data to the device
5  File.Move / File.Replace tmp → destFinalPath             -- atomic rename
6  return { path, sizeBytes, sha256, defendantCount, eventCount }
Failure at any step: delete tmp, report error class; main DB is never affected.
```
Never use `File.Copy` on the live database.

**Tier C — silent backup on exit:** at normal close and on `SessionEnding`: skip if `app_meta.change_seq == last_exit_backup_seq`; otherwise snapshot to `exit-<yyyyMMdd-HHmmss>.db`, set `last_exit_backup_seq`, apply retention (**keep newest 60, plus the first backup of each of the last 12 months**), then `wal_checkpoint(TRUNCATE)` and close. Time budget 8 s (2 s on `SessionEnding`); if exceeded, abandon the backup (Tier B already covers). If `settings.secondary_backup_dir` is set and reachable, also copy the new file there (best effort).

**Tier D — one-click USB export** («حفظ نسخة على الفلاشة»):
```
1  drives := UsbDriveProvider.List()    -- DriveType.Removable && IsReady; plus (collapsed "أقراص أخرى") non-system Fixed drives
2  none      → UsbBackupDialog waits, polling every 1.5 s, shows strings bk.usb.insert + «حاول تاني»; continues by itself
   exactly 1 → continue without asking
   several   → big buttons, one per drive: letter, label, size, free space
3  require free space ≥ 2×dbSize + 5 MB else message bk.usb.fail.full
4  snap := BackupEngine.Snapshot(H\tmp\usb-<ts>.db)                                  -- verified local snapshot
5  dest := <drive>\CourtDesk_Backups\CourtDesk_<yyyyMMdd_HHmm>.db.partial
   copy snap → dest through FileStream(write-through) ; Flush(true)
6  verify dest: open read-only; integrity_check=="ok"; counts equal snap counts; SHA-256(dest)==snap.sha256
7  rename .partial → .db ; keep newest 30 CourtDesk_*.db on the drive (delete older matches only)
8  settings.last_usb_backup_utc := now ; success screen with big message bk.usb.ok
   any failure: delete .partial, show reason (removed during write / full / read-only / unknown) in plain Arabic
```
**Reminder:** if `last_usb_backup_utc` is older than `usb_backup_reminder_days` (default 7) or never set, the status bar and backup screen show a calm amber line (`bk.usb_old` / `bk.never_usb`). No modal nagging.

**User-initiated restore** («استرجاع نسخة قديمة»): lists local backups (auto/exit/pre-op) and `CourtDesk_Backups` on any attached removable drive, newest first, labelled with Arabic date/time. Selecting one shows a comparison card (selected copy: N defendants, date; current: M defendants), the sentence "هنحفظ نسخة من بياناتك الحالية الأول", and two buttons «استرجاع» / «رجوع». Execution = restore swap of §4.9 (which makes the `pre-restore` copy automatically), then the app reloads all caches.

**CSV export** («تصدير كل المتهمين لملف إكسل»): UTF-8 **with BOM**, CRLF, Arabic headers, one row per non-deleted defendant (all statuses), dates `dd/MM/yyyy`, saved to the Desktop as `CourtDesk_Export_<ts>.csv`, then the folder opens. Purpose: a human-readable last-resort copy.

### 6.7 Offline A4 roster generation
**Roster set** for a date `D` (today by default; the schedule screen can print another date):
```
R(D) = { d | d.is_deleted=0 AND ( (d.status='DETAINED' AND d.session_date = D)
                                  OR EXISTS decision event e: e.defendant_id=d.id AND e.event_type='DECISION'
                                         AND e.decision_code<>'REOPEN' AND e.session_date_held = D AND e.undone_by_event_id IS NULL ) }
```
So reprinting in the afternoon still lists everyone who was sent in the morning, even after their decision moved them. Court/circuit shown = `court_name_at_event` / `circuit_name_at_event` of the decision (the court that held the session); for pending rows, current names. Order: court `sort_order`, court name, circuit name, defendant name. Numbering `م` restarts at 1.

**Page and typography** (A4 **landscape**, 297×210 mm = 1122.5×793.7 DIP at 96 dpi; margins 12 mm ≈ 45.4 DIP ⇒ content box ≈ 1032×703). Font `Tahoma` (fallback Segoe UI, Arial). Body 13 pt (never < 12 pt), table header 13 pt bold on light grey `#E6E6E6`, title 20 pt bold, header lines 16 pt bold. **Black on white only**; nothing relies on color (works on B/W laser printers). `FlowDirection=RightToLeft` on every page root. All pages built in code as `FixedPage`s inside a `FixedDocument`.

**Page content** (`RosterModel` → `RosterDocumentBuilder`):
1. Page-1 header (top right, stacked): `وزارة الداخلية` / `مديرية أمن {directorate}` / `قسم شرطة {station}` (empty lines omitted). Centered title `كشف ترحيل المحبوسين إلى المحاكم`; below it `ليوم {weekday} الموافق {dd/MM/yyyy}`.
2. Table, columns right→left: `م` (6%) · `اسم المتهم` (22%) · `رقم القضية` (13%) · `التهمة` (21%) · `المحكمة / الدائرة` (17%) · `مكان الحجز` (10%) · `ملاحظات` (11%, blank for hand notes). 1 pt borders; header row repeats on every page.
3. Summary line after the table: `إجمالي عدد المحبوسين: {n}` (with per-court counts if more than one court).
4. Signature block (last page only, **kept together**, four equal boxes): `القائم بالتحضير` (prefilled with operator name) · `ضابط الحجز` · `رئيس قوة الترحيل (استلام)` · `مأمور / رئيس القسم`, each with a signature line and the caption `الاسم والتوقيع`; plus the line `وقت التسليم: ______ تاريخ: ______`.
5. Footer on every page: `صفحة {i} من {n}` (center) and `طُبع بتاريخ {dd/MM/yyyy} الساعة {HH:mm}` (side).

**Pagination algorithm** (`RosterPaginator`, pure geometry so it is unit-testable without a printer):
```
Inputs: model, measure(rowIndex) → height in DIP at table width (via off-screen layout), constants:
        pageH=703, footerH=28, tableHeaderH=34, page1HeaderH=H1 (measured), sigBlockH=S (measured), summaryH=26
page = new; avail = pageH − footerH − page1HeaderH − tableHeaderH
for each row r in order:
    h = measure(r)
    if h > avail and page has ≥1 row: close page; page = new; avail = pageH − footerH − tableHeaderH(+ slim 28 DIP running title)
    place r; avail −= h
after last row: need = summaryH + sigBlockH
    if need > avail:
         if page has ≥ 2 rows: move the LAST row to a new page (avoid an orphan signature page); recompute
         else: put summary+signature on a new page
total pages known only now → second pass fills "صفحة i من n"
Invariants (tested for N=0…200 random heights): no content exceeds the page box; header row on every page;
signature block exactly once, on the last page; sum of placed rows == N; row order preserved.
```
**Printing**: `PrintService.PrintRoster(model, direct)`: build document → `PrintTicket` with `PageMediaSize ISOA4`, `PageOrientation Landscape` → if `direct`, `PrintDialog { PrintQueue = LocalPrintServer.GetDefaultPrintQueue(), PrintTicket = ticket }.PrintDocument(paginator, title)` with no dialog; on success append a `roster_prints` row (date, count, ids, operator). If there is no default printer or the spool fails: show message `print.fail` and open `PrintPreviewWindow` automatically. Preview = read-only `DocumentViewer` with big custom buttons «طباعة» (direct), «اختيار طابعة تانية» (standard dialog), «إغلاق». The sidebar's one-click button uses `direct` when `settings.print_direct = 1` (default); «معاينة قبل الطباعة» always opens the preview. If the roster is empty the buttons show `print.empty` instead of printing.

---

## 7. UI/UX architecture for a low-tech elderly user

### 7.1 Principles (binding)
- **P1 One screen, one job.** A single main window; modal dialogs for tasks; no MDI, no tabs inside dialogs, no menu bar, no hamburger, no tooltips-only information.
- **P2 Words, not symbols.** Every button has an Arabic text label; icons (vector `StreamGeometry`, no icon fonts, no image files) only *accompany* text.
- **P3 Never rely on color alone.** Every state has color **+ icon shape + word**.
- **P4 Buttons are never silently disabled.** Primary actions stay enabled, validate on click and explain in a sentence what is missing (focus moves to the problem field). Toolbar actions that need a selected row show `hint.select_row` instead of being greyed out.
- **P5 Forgiving.** Confirmations are full sentences naming the person; every dangerous or irreversible-looking action has a clear safe default (initial focus on the safe button, `Esc` = safe). Decisions can be undone.
- **P6 No timing pressure.** No auto-dismissing dialogs; toasts persist ≥ 10 s or until clicked; no timeouts.
- **P7 Predictable.** Same button, same place, same color, always. Confirm buttons on the leading edge (right in RTL) in primary color; cancel/back on the opposite side in neutral.
- **P8 Self-protecting.** Impossible choices are not offered (wizard shows only valid decisions; calendar blocks invalid days; invalid combinations cannot be saved).
- **P9 Quiet.** No animations, no sounds, no flashing. Only a static highlight when attention is needed.
- **P10 Language assist.** On focus of an Arabic text field, switch the keyboard layout to Arabic (`InputLanguageManager`, best-effort); on numeric fields accept both digit sets.

### 7.2 Sizing and contrast standards **[FIXED]** (units = WPF DIP at 100% zoom)
| Element | Standard |
|---|---|
| Base text | **20**; **absolute minimum anywhere: 18** (secondary hints only) |
| Grid cell text / header | 20 / 20 bold (22 when window width ≥ 1600) |
| Titles: dialog / section / window | 28 / 26 / 30 bold |
| Buttons | height ≥ **56** (primary action **64–72**), min width 140, text 22 bold |
| Text inputs, combos | height **56**, text 22 |
| Any other interactive element | min **48 × 48**; gap between adjacent targets ≥ **12** |
| Grid row | min height **56**, 2-line wrap then ellipsis; header 52 |
| Chips (status) | height 40, text 18 bold, with icon |
| Check/radio | 32 × 32 |
| Scrollbars | width **28**, min thumb 56, large arrow buttons |
| Calendar day cell | ≥ 76 × 60, number 24 |
| Focus indicator | double ring: 3 DIP black outer + 3 DIP yellow `#FFD400` inner, on every focusable control |
| Contrast | body text on its background **≥ 7:1**; large/bold text and UI boundaries/icons **≥ 4.5:1**; accent stripes **≥ 3:1** |
| Zoom | top-bar buttons «حرف أصغر / حرف أكبر» cycle 100 % → 125 % → 150 % (`LayoutTransform` on the root), persisted (`font_scale`) |
| Minimum screen | layout valid at 1024×768 (100 %); at 125/150 % vertical scrolling inside regions is allowed, horizontal scrolling never on the page itself |

An automated test (§9) parses `Colors.xaml`/styles to assert the contrast pairs, no `FontSize < 18`, and button/input style heights.

### 7.3 Color tokens and state semantics (`Styles/Colors.xaml`)
| Token | Hex | Notes |
|---|---|---|
| Surface / SurfaceAlt | `#FFFFFF` / `#F5F3EE` | page / zebra rows |
| Ink / InkMuted | `#151515` / `#4A4A4A` | ≈18:1 / ≈8.9:1 on white |
| Border | `#6B6B6B` | grid lines, inputs (≥ 5:1) |
| Primary (actions, selection) | `#084A8F` with white text | ≈8.8:1 |
| Danger | `#B3261E` with white text | destructive actions only |
| Focus | `#000000` + `#FFD400` | see §7.2 |

| State | Background / accent (stripe, border, icon) | Icon | Word | Where |
|---|---|---|---|---|
| **TODAY** (جلسة اليوم) | `#FFF1B8` / `#8A5A00` | clock | جلسة اليوم | row tint + chip + sidebar cards |
| **OVERDUE** (فاتت) | `#FFD9D4` / `#B3261E` | triangle-exclamation | فاتت الجلسة | row tint + chip + sidebar top block |
| **RESCHEDULED / UPCOMING** | `#D6E8FA` / `#0B5CAD` | circular-arrow | جلسة قادمة / «تأجّل إلى …» | chip (+ decided cards that moved forward) |
| **RELEASED** | `#D5EFDA` / `#1B6E2D` | check-in-circle | مُفرج عنه | chip, decided cards |
| **JUDGED** | `#E4DDF4` / `#5B3FA3` | scales (two-pan) | صدر حكم | chip, decided cards |
| **NO_DATE** | `#EDEDED` / `#4A4A4A` | dashed-circle | بدون ميعاد | chip |

Only TODAY and OVERDUE tint a whole grid row; all other states use only a 8-DIP leading stripe + chip. Selected row = Primary background, white text (selection always beats tint). Text on pastel backgrounds is always `Ink`.

### 7.4 Typography and RTL rules
- Font stack `Tahoma, "Segoe UI", Arial` set once in `Typography.xaml`; no embedded or downloaded fonts.
- `Window.FlowDirection="RightToLeft"`; every dialog/popup inherits it explicitly. `xml:lang="ar-EG"`; root style sets `NumberSubstitution.Substitution="European"`; `TextOptions.TextFormattingMode="Ideal"`, `UseLayoutRounding="True"`.
- In a RTL `Grid`, column 0 renders on the **right**; the sidebar is column 0.
- Directional icons mirror: "next/forward" arrow points **left**, "back" points **right**.
- Numbers/dates/case numbers keep LTR runs (LRM wrapping, §5.6).
- `DataGrid`: first declared column appears rightmost — declare columns in logical reading order.

### 7.5 Main window layout (≥1024×768; maximized at start; `MinWidth 1024, MinHeight 700`)
```
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│ TOP BAR (76)   App title + station │ NAV: [Register][Schedule][Decisions][Backup & Settings] │
│                │ big date "Tuesday 6 October 2026" │ [A−][A+] [Help] [Close program]         │
│                │ (red clock-warning banner appears here when suspect)                        │
├──────────────────────┬──────────────────────────────────────────────────────────────────────┤
│ TODAY SIDEBAR        │ CONTENT HOST (swaps: Register / Schedule / Decisions / Backup / Help) │
│ (RIGHT side,         │ ┌ SEARCH BOX (60)  [type name or case number…]            [Clear] ┐ │
│  width 380 @≥1280,   │ │ FILTER CHIPS: [Detained 112][All 340][Released 201][Judged 27]    │ │
│  340 @<1280)         │ │               [No date 3]                                        │ │
│ ─ header: "جلسات اليوم"│ │ ACTION BAR: [+ Add][Edit][Record decision][Card & history][Void]  │ │
│   date, progress     │ ├──────────────────────────────────────────────────────────────────┤ │
│ ─ OVERDUE block (red)│ │ EXCEL-LIKE GRID: # │ Name │ Case │ Court/Circuit │ Session │ State │ │
│ ─ PENDING cards      │ │ (sortable headers, gridlines, zebra, frozen first data column)     │ │
│ ─ DECIDED cards      │ │                                                                  │ │
│ ─ tomorrow count     │ ├──────────────────────────────────────────────────────────────────┤ │
│ ─ [PRINT ROSTER A4]  │ │ STATUS BAR: "340 records" · last exit backup · USB-backup age     │ │
│ ─ [Preview]          │ └──────────────────────────────────────────────────────────────────┘ │
└──────────────────────┴──────────────────────────────────────────────────────────────────────┘
```
The sidebar is **persistent in every view** (only dialogs cover it). Header and print buttons are fixed; the card list scrolls vertically inside the sidebar.

### 7.6 "Today's Sessions & Convoy Roster" sidebar
1. **Header:** `side.title`, long Arabic date (28 bold), progress `side.progress` ("تم تسجيل 5 من 14") with a thick progress bar (no color-only meaning; the numbers are the information).
2. **OVERDUE block** (only if any): red-accent header `side.overdue`, compact cards (name, case, "فاتت من {n} يوم", button «تسجيل القرار»). Always above pending cards.
3. **PENDING cards** (amber): name (24 bold), line 2 `{n} لسنة {y}` + circuit, line 3 court; full-width button «تسجيل القرار» (56). Clicking the name opens the case card. A button at the top of the list, `side.queue`, starts **queue mode** (§7.9).
4. **DECIDED cards** (stripe color by outcome: blue rescheduled, green released, purple judged): name + result sentence ("تجديد حبس ← جلسة 19/10/2026"); small button «تراجع» (48 high) for the latest event.
5. **Footer (fixed):** `side.tomorrow` (≥18 text), primary button «طباعة كشف الترحيل (A4)» (72 high, printer icon), secondary «معاينة قبل الطباعة» (56). If the roster is empty, the center shows `side.empty` with a check icon and the print buttons answer with `print.empty`.

### 7.7 Register view (central, default)
- **Search box:** 60 high, 22 text, placeholder `search.placeholder`, auto-focused on start and when returning to this view; `Esc` clears; button «مسح». Filters on every keystroke (§6.4).
- **Filter chips** (single-select, counts shown): `المحبوسون` (default), `الكل`, `المفرج عنهم`, `صدر فيهم حكم`, `بدون ميعاد جلسة`. Selection persisted (`last_filter`).
- **Action bar** (5 labeled buttons, 56 high): `+ إضافة متهم جديد`, `تعديل البيانات`, `تسجيل قرار الجلسة`, `بطاقة المتهم وسجله`, `إلغاء السجل (إدخال خاطئ)` (Danger style, last). Row-dependent buttons with no selection show `hint.select_row` in a yellow band under the bar for 10 s.
- **Grid** (`DataGrid`: read-only, full-row single selection, `AutoGenerateColumns=False`, row virtualization with recycling, gridlines on, zebra `SurfaceAlt`): 
  - Default columns (reading order): `م` · `الاسم` · `رقم القضية` (`{n} لسنة {y}`) · `المحكمة / الدائرة` (two lines) · `ميعاد الجلسة` (weekday line + short date) · `الحالة` (StatusChip).
  - Toggle «عرض كل الأعمدة» adds `التهمة` and `مكان الحجز` (horizontal scroll inside the grid only; persisted `grid_wide_mode`).
  - Sort by clicking a header (arrow shown), default urgency sort (§6.4). Keyboard: arrows move, `Enter` opens the case card, `Home/End`, `PageUp/PageDown`.
  - Double-click opens the case card (accelerator only; a visible button exists).
- **Empty states:** no records → big sentence + the «إضافة متهم جديد» button; no search hit → §6.4 messages.
- **Status bar:** record count, "آخر نسخة عند الخروج: …", USB-age line (§6.6).

### 7.8 Add / edit defendant (`DefendantEditDialog`, ≤ 900×700, single scrolling column; two columns when width allows)
Order and controls (all labels from Appendix A; required fields carry the word `(مطلوب)`, not just an asterisk):
1. اسم المتهم (رباعي) — text, required. 2. الرقم القومي — text, optional, 14 digits.
3. رقم القضية — digits, required; سنة القضية — digits, required, default current year; نوع القضية — editable combo.
4. التهمة — multi-line text with autocomplete from `charge_suggestions`.
5. المحكمة — combo (+ إضافة جديد); الدائرة — combo filtered by court (+ إضافة جديدة); مكان الحجز — combo (+ إضافة جديد).
6. ميعاد الجلسة — date control (`BigCalendar` popup + `اليوم`/`غدًا` quick buttons + "بدون ميعاد"); محبوس من تاريخ — same control.
7. ملاحظات — multi-line.
Buttons: «حفظ» (primary, leading edge), «إلغاء». Save never disabled: on click validate all fields, show a red banner listing problems in plain Arabic and focus the first bad field. On duplicate: `dup.warn` dialog. After save a toast `saved` and the new/edited row is selected and scrolled into view.

### 7.9 The Postponement / Decision wizard (`DecisionWizardDialog`)
**Entry points:** «تسجيل القرار» on any sidebar card; «تسجيل قرار الجلسة» in the action bar; `side.queue` (queue mode).
**Pre-checks (before Step 1, plain-language stop messages, never an exception):** V3 failures (`err.no_session`, `err.future_session`) with a button «تعديل البيانات»; clock-suspect warning (§6.5); defendant already non-DETAINED ⇒ only the `REOPEN` tile is offered.
**Frame (all steps):** modal, centered, ≤ 92 % of the window (max 960×720), dimmed backdrop, **locked identity bar** on every step (name 26 bold · `{n} لسنة {y}` · court/circuit · «جلسة يوم {date}» — read-only, so the officer always sees *whose* decision this is), step counter `wiz.step`, **no title-bar X** (a visible «إلغاء» button; `Esc` = cancel; cancel writes nothing).

| Step | Shown when | Content | Navigation / validation |
|---|---|---|---|
| **1 — Decision** | always | Q `wiz.q1`; large tiles (≥ 380×110, 2 columns): تجديد الحبس · تأجيل الجلسة · إخلاء سبيل · إخلاء سبيل بكفالة · صدر حكم (only valid tiles shown) | Tapping a tile selects it (check icon + thick border) **and advances**; «رجوع» always available from step 2 |
| **2 — Details** | per decision | **RENEW / POSTPONE / REOPEN:** Q `wiz.q2`; quick buttons `بعد {n} يوم` from `quick_offsets` (default 7·15·30·45, counted from the held session date, from today for REOPEN); `wiz.pick_date` opens `BigCalendar` (Fri/Sat shaded; invalid days before the minimum are non-selectable; per-day counts of already scheduled sessions shown small); live line `wiz.chosen` ("الجلسة الجاية: الاثنين 19 أكتوبر 2026 (بعد 13 يوم)"); reason chips from `postponement_reasons` (RENEW/POSTPONE, optional); collapsed «تغيير المحكمة أو الدائرة»; warning panels with a 32×32 tick-box for V5/V6/V7 (`warn.past/weekend/far` + `warn.confirm`). **RELEASE_BAIL:** numeric `wiz.bail_amount` (optional) + note. **RELEASE / JUDGMENT:** optional note (`wiz.note` / `wiz.judgment_note`). | «التالي» (primary, arrow pointing left) validates (V4–V10); failures appear as a red sentence under the offending control; button stays enabled |
| **3 — Review** | always | Title `wiz.q3`; **one full plain sentence** in 26-pt text built from the decision (Appendix A `wiz.sentence.*`), plus a small summary list; nothing editable | «حفظ القرار» (green-on-primary, 72 high) · «رجوع للتعديل». On click: button replaced by "جارٍ الحفظ…"; request carries a fresh `request_uuid` (double-click safe) |
| **Success** | after commit | Big check icon, `wiz.done`, line "الجلسة الجاية: …" or "اتنقل لقايمة …", buttons: «المتهم الجاي: {name}» (queue mode, primary), «إغلاق», small «تراجع عن القرار ده» | Undo → confirmation sentence (`undo.confirm`) → §6.3 |
**Error branches:** `err.stale` (record changed) → dialog with «افتح السجل من جديد»; any other failure → generic friendly message with an error code; **nothing is saved** and the wizard stays open with the user's choices intact.
**Queue mode (afternoon return):** after each successful save the wizard re-opens at Step 1 for the next pending defendant (same order as the roster). A progress line "{i} من {n}" and a «وقّف هنا» button leave the queue at any moment. Decisions already recorded are never lost by stopping.
**Foolproofing checklist (each item is a test or a review gate):** identity always visible · only valid options exist · invalid dates unselectable · risky dates need an explicit tick · full-sentence review · one primary action per step · idempotent save · atomic commit · success echo + undo · no ambiguous «OK/Cancel» labels · no free-text date entry.

### 7.10 Other screens
- **Schedule (`جدول الجلسات`):** left/center: month `BigCalendar` with per-day counts (number badge, ≥18 text), Fri/Sat shaded; right: list of that day's defendants (read-only, same columns as roster) and buttons «طباعة كشف اليوم ده» (uses §6.7 with the selected date) and «تعديل الميعاد» (opens the edit dialog). Default day = today. A strip "الأيام الجاية" lists the next 14 days with counts.
- **Decisions history (`سجل القرارات`):** newest-first grid of audit events (time, defendant, event/decision, next date, operator, "تم التراجع" flag), with the search box scoped to history and a date-range of two `BigCalendar` pickers («من» / «إلى»); row → case card.
- **Case card (`DefendantCardDialog`):** all fields read-only, big timeline of events (icon + sentence per event), buttons «تعديل البيانات», «تسجيل قرار الجلسة», «تراجع عن آخر تغيير», «إغلاق». Deleted records appear in a **trash** list inside the backup/settings screen with «استرجاع السجل».
- **Backup & Settings (`النسخ الاحتياطي والإعدادات`):** top: three big buttons — «حفظ نسخة على الفلاشة», «استرجاع نسخة قديمة», «تصدير كل المتهمين لملف إكسل»; below: last backup times/USB age (`bk.*`), note `bk.auto_note`, «فتح مكان البيانات»; **Settings** (plain form, all optional): اسم القسم, اسم المديرية, اسم القائم بالتسجيل, الأيام السريعة, الطباعة المباشرة (yes/no), «إدارة القوائم» (courts, circuits, custody locations, postponement reasons → `LookupAdminDialog`), trash, app version and DB path read-only.
- **Help (`مساعدة`, F1):** six short large-type steps (Appendix A `help.*`), no scrolling needed at 1366×768.
- **First run (`FirstRunDialog`):** one screen: station name, directorate name, officer name (all optional with sensible blanks) → «ابدأ». Nothing else is asked.
- **Recovery notice, clock warning, USB dialogs, restore dialog:** as specified in §4.9 / §6.5 / §6.6.

### 7.11 Dialog, toast, keyboard standards
- Dialog types: `MessageDialog` (one button «تمام»), `ConfirmDialog` (title, sentence, primary label, secondary label; the **safe** button has initial focus; labels are verbs, never «OK/Cancel»). Max width 760, padding 32, text 22.
- `ToastHost`: non-blocking strip at the top of the content area, persists ≥ 10 s or until clicked, has a visible «تمام» button.
- Keyboard (always optional, never required): `Tab` order follows reading order (right→left, top→bottom); `Enter` = default (primary) button; `Esc` = cancel/safe; `Ctrl+F` focus search; `F1` help; `Ctrl+N` add defendant; `F2` edit selected; `Ctrl+P` print roster (direct). Shortcuts are not shown on buttons (avoid clutter).
- Closing: the window X and «إغلاق البرنامج» both open `ConfirmDialog` («تقفل البرنامج؟» / «أيوه، اقفل» / «لأ، ارجع»); on confirm run Tier-C exit backup (cursor = wait, ≤ 8 s) then exit.
- Never show exception text; show the Arabic message and, for unexpected errors only, an error code (e.g. `E-DB-017`) that is also in the log.

### 7.12 Rendering and performance settings
`RenderOptions.ProcessRenderMode = SoftwareOnly` at startup; no animations/transitions/effects (no blur, no drop shadows — use borders); `ScrollViewer.CanContentScroll=True` with virtualization on grids; DataGrid bound to an `ICollectionView` over a pre-built list (no per-keystroke allocation of row objects); memory target < 150 MB at 20,000 records; `app.config`: `gcServer=false`, `gcConcurrent=false`; `ProfileOptimization` profile stored in `H\tmp`.

---

## 8. Error handling, logging and diagnostics

| Situation | Behaviour |
|---|---|
| Validation failure | Inline Arabic sentence under the control + summary banner; nothing saved. |
| `ConcurrencyException` / V1 | `err.stale` dialog → «افتح السجل من جديد». |
| Any DB exception on a write | Transaction rolled back; friendly `err.generic` with code `E-DB-nnn`; UI state unchanged; log the full exception (ids only). |
| `SQLITE_BUSY` at startup | `busy.other`, exit. |
| Disk full / I/O error on commit | Treated as "not saved": message says so explicitly; no cache update; suggest freeing space or USB backup. |
| Unhandled exception (`Dispatcher`, `AppDomain`) | Log; show `err.generic`; if the state is unsafe, restart the app through the normal startup path. `TaskScheduler.UnobservedTaskException` is logged only. |
| Abrupt previous exit (`clean_shutdown='0'`) | After a passing integrity check show `recover.unclean` once. |
| Printer missing/failing | `print.fail` + automatic preview. |
| Clock suspect | §6.5. |
| Invariant violation at startup | §4.6 banner; no auto-restore. |

Error codes are stable strings (`E-DB-001…`, `E-PRN-001…`, `E-BAK-001…`, `E-STA-001…`) listed in `ErrorCodes.cs` and in the log line. Logs contain ids and codes, **never** names, national IDs or case text.

---

## 9. Testing and verification

### 9.1 Automated test suites (`tests/CourtDesk.Tests`, xUnit, categories)
| Category | Must cover |
|---|---|
| **Domain** | `ArabicText.Normalize` (tashkeel, alef/ya/ta-marbuta, digit sets, bidi controls, "عبدالله"/"عبد الله"); `LocalDate`; **`DecisionRules` exhaustive table** (every status × decision × validity, V1–V12 each with positive and negative case, weekend/far/past confirmations); `DisplayState`; `SnapshotJson` canonical output stability; weekday/month formatting with Western digits. |
| **Persistence** | Pragmas read back correctly; migration 001 on empty DB; every CHECK/FK/trigger rejects what it should (hard delete of defendant, audit update/delete, circuit–court mismatch, non-DETAINED with date); `Decide` atomicity (inject a failure at each of steps 3–10, assert zero visible change); idempotent `request_uuid`; optimistic concurrency; undo (allowed/denied cases, chained decide→undo→decide); invariants I1–I6 after randomized operation sequences (property-style, 5,000 ops, fixed seed); backup engine produces self-contained DB (journal_mode `delete`, no `-wal`); restore swap rollforward/rollback at **every** step boundary (simulate marker states); startup recovery with a truncated/garbage DB and with a flipped-byte DB. |
| **Crash** | `CrashHarness` console process loops random operations (create/edit/decide/undo) against a temp DB; the test **kills it at random instants (≥ 300 iterations)**, then reopens and asserts: `quick_check == ok`; I1–I6 hold; the set of acknowledged operations (harness writes each acknowledged `request_uuid` to a separate fsynced file *after* commit returns) is a subset of what is in the DB (no acknowledged loss), and no partial operation exists. Run again with `--kill-during-backup`. |
| **Printing** | `RosterPaginator` properties for N = 0…200 with random row heights (see §6.7 invariants); roster set `R(D)` incl. decided-today, undone, REOPEN exclusion; builder output page count; `--selftest` prints to an XPS file and reads page count back; render page PNG for visual QA. |
| **UI** | STA test helper (`StaRunner`): instantiate views with a temp DB — sidebar shows pending/decided/overdue correctly for a `FakeClock`; wizard flows (each decision), validation messages, queue mode; search filter correctness and speed (20,000 synthetic Arabic records: filter < 50 ms); day rollover refresh; RTL (`FlowDirection`) on every window/dialog; **accessibility lint**: contrast pairs from `Colors.xaml`, no `FontSize < 18`, button/input style heights, every `Button` has non-empty Arabic `Content`/`AutomationProperties.Name`. |
| **Arch** | Layering matrix §3.2; no `System.Net*`/`HttpClient`/`Socket`; no Arabic literals outside `Strings.cs`; no `DateTime.Now/Today/UtcNow` outside `SystemClock`; no `File.Copy` of the live DB path; the vendored native DLL imports only allow-listed system DLLs (below). |
| **Selftest** | Runs the built `dist\CourtDesk.exe --selftest --home <empty temp dir>` (see 9.2). |

**Native import allow-list test:** parse the PE import table of `vendor\native\x86\SQLite.Interop.dll`; every imported DLL must be one of `kernel32, user32, advapi32, ole32, oleaut32, shell32, ntdll, msvcrt` (case-insensitive). If it imports `vcruntime*`, `msvcp*`, `api-ms-win-crt-*` or `ucrtbase`, replace the vendored pair with the **static-CRT** official build of the same version and re-run; record in `DECISIONS.md`.

### 9.2 `--selftest` (headless gate on the **final single exe**)
`CourtDesk.exe --selftest --home <dir>` must, within 30 s and without showing UI: extract and load the embedded SQLite; create the DB; run migrations; seed; create 3 defendants; run Decide for each decision type; Undo one; build the roster for today with a `FakeClock`; paginate and write an XPS to `<dir>\selftest.xps`; take a snapshot and verify it; run invariants; exit code **0** on success, non-zero with the failing step on stderr otherwise. The build gate runs it from an empty directory containing **only** the exe (proves the single-file claim and the embedded loader).

### 9.3 Visual QA (the agent must look at the images)
`tools/RenderScreens` renders to PNG at 1366×768 and 1024×768 (100 % and 150 %): register view (0, 3 and 200 rows), sidebar (empty / 3 pending / overdue + decided), each wizard step, edit dialog, backup screen, USB dialog, clock warning, and roster pages for N = 1, 12, 40. The agent opens each PNG and checks the review list: Arabic shaped and right-aligned, RTL order of columns, nothing clipped or overlapping, text ≥ 18, contrast, buttons ≥ 56 high, identity bar visible in every wizard step, signature block only on the last roster page.

### 9.4 Acceptance criteria
| ID | Criterion | Evidence |
|---|---|---|
| AC-01 | No networking APIs or online assets anywhere; app works with all adapters disabled | Arch test; manual |
| AC-02 | Builds `x86`; uses no API newer than .NET 4.6.2/Win7; no Win8+-only calls | Arch scan + checklist 9.5 |
| AC-03 | `dist\CourtDesk.exe` alone runs from an empty folder; `--selftest` exits 0 | Selftest |
| AC-04 | ≥ 300 random kills: DB opens, `quick_check` ok, invariants hold | Crash suite |
| AC-05 | No acknowledged operation is ever missing after a kill | Crash suite |
| AC-06 | All windows RTL; no Arabic literal outside `Strings.cs`; Western digits everywhere | UI + Arch tests, PNGs |
| AC-07 | 20,000 records: filter < 50 ms/keystroke; startup load < 1.5 s on dev host | UI perf test |
| AC-08 | Sidebar equals `SELECT … session_date = local today` for random datasets; rolls over at midnight and on clock change | UI + service tests |
| AC-09 | Overdue defendants always visible in the red block until a decision is recorded | UI test |
| AC-10 | One click prints/previews a correct A4-landscape roster with header, repeated table header, summary, signature block on last page only | Printing tests + PNGs |
| AC-11 | Decide commits defendant row + audit event + meta bump in one transaction; injected failure at any step leaves zero change | Persistence tests |
| AC-12 | Every wizard path ends in a state allowed by §5.2; invalid dates cannot be saved; double-click saves once | UI + Domain tests |
| AC-13 | Undo restores exact prior state; audit rows cannot be updated/deleted (except the `undone_by` marker); no hard delete | Persistence tests |
| AC-14 | Exit backup, Tier-B ring, USB export (verified, rotated), restore (with pre-restore copy) all work and survive kills at every step | Persistence + Crash tests |
| AC-15 | Corrupt DB at startup → quarantine + automatic restore of newest verified snapshot + Arabic notice | Persistence test |
| AC-16 | Sizing/contrast standards of §7.2 enforced by lint | UI lint test |
| AC-17 | Splash ≤ 400 ms; usable window ≤ 3 s on baseline (measure on dev host, record number; baseline measured by checklist 9.5) | Timing log + manual |
| AC-18 | A first-time user can reach "print roster" and "record a decision" with zero configuration | Walk-through in README + UI test of first-run |

### 9.5 Manual QA checklist (for the human integrator; the agent must generate it as `QA-CHECKLIST.md`)
Fresh Windows 7 SP1 x86 VM with .NET 4.8 installed, and Windows 10/11 x64; verify: launch from a USB stick and from Desktop; first-run screen; desktop shortcut; Arabic rendering and Western digits; printing to a real printer and to "Microsoft XPS Document Writer"; unplug power / kill VM during a decision save (5 repetitions); change system date backwards → warning; USB export with 0, 1, 2 drives; pull the USB mid-copy; 1024×768 resolution; 150 % zoom; start time with stopwatch on the slowest available PC.

---

## 10. Implementation roadmap for the autonomous agent

### 10.1 Phases and gates
Each phase lists **deliverables** and **gates** (all must pass; record results in `PROGRESS.md`). Test commands use `dotnet test tests\CourtDesk.Tests -c Release --filter "Category=<X>"`.

| Phase | Deliverables | Gates |
|---|---|---|
| **0 Preflight & scaffold** | Verify Windows + `dotnet --list-sdks` (8.0.x) + disk. Run `tools\Fetch-Vendor.ps1` (downloads `Stub.System.Data.SQLite.Core.NetFramework 1.0.118` from nuget.org, extracts `lib\net46\System.Data.SQLite.dll` and `build\net46\x86\SQLite.Interop.dll` (locate by file name if the folder layout differs; both must come from the same package), plus nupkgs for the offline feed; writes `vendor\MANIFEST.sha256`). Create solution, `CourtDesk.csproj` (§10.4), `tests`, `nuget.config`, `build.cmd`, `.gitignore`, `README.md`, `DECISIONS.md`, `PROGRESS.md`. Empty window builds. | `dotnet build` ok offline; manifest verifies; native import allow-list test passes; Arch tests (skeleton) pass |
| **1 Domain** | Everything in `Domain/` + unit tests; `Strings.cs` skeleton with Appendix A keys. | `Category=Domain` and `Arch` green; ≥ 95 % line coverage of `DecisionRules`, `ArabicText`, `LocalDate` (measure with a simple instrumented run or review) |
| **2 Persistence** | `DbPaths`, `SqliteBootstrap` (§4.2), `DbGateway`, `MigrationRunner` + `001_initial.sql`, stores, `SeedData`, `IntegrityChecker`, `SqliteBackupEngine`, `RestoreSwap`, startup recovery (§4.9), `CrashHarness`. | `Category=Persistence` and `Category=Crash` green (≥ 300 kills ×2 modes) |
| **3 Application services** | `DefendantService`, `DecisionService`, `UndoService`, `TodayService`, `ScheduleService`, `SearchIndex`, `LookupService`, `SettingsService`, `BackupService` (tiers B–D logic with fake drive provider), `RosterService`, `ClockSanityService`, `CsvExportService`; in-memory fakes for ports in tests. | Service tests green; `Decide` fault-injection tests green; 20,000-row search < 50 ms |
| **4 UI shell (read-only)** | Styles (colors, typography, controls, icons), `MainWindow`, top bar, `TodaySidebar`, `RegisterView` (search, chips, grid), font zoom, splash, `StatusChip`, `ToastHost`. | UI tests; **RenderScreens PNG review (§9.3)** for register + sidebar at both resolutions; lint test green |
| **5 Forms & wizard** | `DefendantEditDialog`, `BigCalendar`, `LookupCombo`, `DecisionWizardDialog` (all steps, queue mode, undo), `DefendantCardDialog`, void/trash, `ConfirmDialog`, `MessageDialog`, `ScheduleView`, `HistoryView`, `FirstRunDialog`. | UI wizard tests (every decision path, validation, double-click, stale); PNG review of every wizard step |
| **6 Printing** | `RosterModel`, `RosterPaginator`, `RosterDocumentBuilder`, `PrintService`, `PrintPreviewWindow`, sidebar print/preview wiring, `roster_prints` logging. | `Category=Printing` green; roster PNGs for N = 1, 12, 40 reviewed; XPS page-count assertion |
| **7 Resilience & platform** | Tier B timer, Tier C on exit/`SessionEnding`, USB dialog + `UsbDriveProvider`, restore dialog, `BackupSettingsView`, clock sanity UI, recovery dialogs, `SingleInstanceGuard`, `CrashHandler`, `FileLogger`, `DesktopShortcut`, `PowerEvents`, `InputLanguageAssist`, help screen, settings. | All suites green; manual-style scripted scenarios: kill during save/backup/restore, corrupt file at startup, clock regression |
| **8 Packaging** | Embedded-resource loader + native extraction (hash-verified), `app.manifest`, `app.config`, `build.cmd` end-to-end, `Make-Dist.ps1` (hash file, kit), `--selftest`. | `build.cmd` exit 0 from a clean clone with no network; `dist\CourtDesk.exe` size reported; `--selftest` from an empty folder exits 0; startup timing logged |
| **9 Hardening & docs** | `QA-CHECKLIST.md`, `README.md` (build, deploy, prerequisites, Arabic `اقرأني.txt` quick guide generated from `help.*`), review of all PNGs, remove dead code, final `DECISIONS.md`; *optional* CSV import (below) only if all prior gates are green. | Full suite + selftest + PNG review; `PROGRESS.md` final entry |

*Optional CSV import (`ImportService`):* guided dialog, UTF-8 or Windows-1256 auto-detect, header names auto-mapped from the Arabic export headers; dry-run preview (rows OK / rows with errors, duplicates by `(case_number, case_year, name_norm)`); apply in **one** transaction writing `IMPORTED` events; all-or-nothing.

### 10.2 Exact dependency list
**Runtime (shipped):** none besides the BCL and:
| Item | Version | Source |
|---|---|---|
| System.Data.SQLite (managed, `net46`) | **1.0.118.0** | vendored file `vendor\managed\System.Data.SQLite.dll` |
| SQLite.Interop.dll (native x86; SQLite 3.41.x) | **1.0.118.0** | vendored file `vendor\native\x86\SQLite.Interop.dll` |
| BCL references | in-box | `PresentationFramework`, `PresentationCore`, `WindowsBase`, `System.Xaml`, `System.Printing`, `ReachFramework`, `System.Web.Extensions`, `System.IO.Compression.FileSystem`, `System`, `System.Core`, `Microsoft.CSharp` |

**Build/test only (offline feed `vendor\packages`):**
| Package | Version |
|---|---|
| Microsoft.NETFramework.ReferenceAssemblies (+ `.net462`) | 1.0.3 (`PrivateAssets=all`) |
| Microsoft.NET.Test.Sdk | 17.8.0 |
| xunit | 2.6.2 |
| xunit.runner.visualstudio | 2.5.4 |

**Toolchain:** .NET SDK 8.0.x on Windows 10/11 x64. No Fody/Costura/ILMerge, no MVVM framework, no DI container, no JSON library, no UI toolkit.

### 10.3 The single build command
```
build.cmd
```
`build.cmd` (authoritative, fail-fast, offline) executes, in order:
```
dotnet restore CourtDesk.sln --source vendor\packages
dotnet test tests\CourtDesk.Tests\CourtDesk.Tests.csproj -c Release --no-restore
dotnet publish src\CourtDesk\CourtDesk.csproj -c Release --no-restore -o dist
dist\CourtDesk.exe --selftest --home %TEMP%\courtdesk-selftest      (run from a copy of the exe in an empty folder)
powershell -File tools\Make-Dist.ps1                                  (writes dist\CourtDesk.exe.sha256 and dist\kit\)
```
The product itself is produced by the third line alone: `dotnet publish src\CourtDesk\CourtDesk.csproj -c Release -o dist` ⇒ **`dist\CourtDesk.exe`**.

### 10.4 `CourtDesk.csproj` essentials (binding)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net462</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <UseWPF>true</UseWPF>
    <LangVersion>10.0</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>CourtDesk</AssemblyName>
    <RootNamespace>CourtDesk</RootNamespace>
    <StartupObject>CourtDesk.Program</StartupObject>   <!-- our Main, not the WPF-generated one -->
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Optimize>true</Optimize>
    <DebugType>none</DebugType>
    <Deterministic>true</Deterministic>
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System.Data.SQLite">
      <HintPath>..\..\vendor\managed\System.Data.SQLite.dll</HintPath>
      <Private>false</Private>                              <!-- never copied beside the exe -->
    </Reference>
    <Reference Include="System.Printing" /> <Reference Include="ReachFramework" />
    <Reference Include="System.Web.Extensions" /> <Reference Include="System.IO.Compression.FileSystem" />
    <EmbeddedResource Include="..\..\vendor\managed\System.Data.SQLite.dll" LogicalName="vendor/System.Data.SQLite.dll" />
    <EmbeddedResource Include="..\..\vendor\native\x86\SQLite.Interop.dll"  LogicalName="vendor/x86/SQLite.Interop.dll" />
    <EmbeddedResource Include="Persistence\Migrations\*.sql" />
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>
</Project>
```
`app.manifest`: `requestedExecutionLevel asInvoker`; compatibility GUIDs for Windows 7, 8, 8.1, 10/11; `dpiAware` true. `app.config`: `supportedRuntime v4.0 sku=".NETFramework,Version=v4.6.2"`, `gcServer=false`, `gcConcurrent=false`. A post-publish step asserts `dist\` contains **only** `CourtDesk.exe` (+ its `.config` merged/embedded or deleted if redundant) — any other DLL fails the build.

### 10.5 Deliverables
`dist\CourtDesk.exe` · `dist\CourtDesk.exe.sha256` · `dist\kit\` (the exe, `اقرأني.txt` quick guide, `PREREQUISITES.txt` listing the .NET 4.8 offline installer and Win7 KB4474419/KB4490628 — **the installers themselves are placed by the human integrator**, not downloaded by the agent) · source tree · `QA-CHECKLIST.md` · `README.md` · `DECISIONS.md` · `PROGRESS.md` · screenshots under `artifacts\screens\`.

---

## 11. Risks, decisions and assumptions

### 11.1 Risk register
| Risk | Mitigation |
|---|---|
| Wrong machine date (dead CMOS battery) | §6.5 regression/before-build detection, red banner, decision-time check |
| Forgotten decisions leave detainees past their date | OVERDUE red block pinned above pending cards; `OVERDUE` row tint; never hidden |
| Disk dies / only copy lost | Tier B/C backups + USB reminder; CSV export; `secondary_backup_dir` |
| Disk reports fsync but loses data | Startup `quick_check` + verified ring snapshots + auto-restore |
| Antivirus flags native extraction | Fixed per-user path, hash-verified, no random temp names, documented in README; unsigned-exe note (optional code signing outside scope) |
| GPU/driver instability on old PCs | Forced software rendering, no effects |
| .NET missing on Win7/early Win10 | Kit includes prerequisite list; `PREREQUISITES.txt`; Windows' own message names the missing framework |
| User closes via power button | WAL/FULL + snapshots; `recover.unclean` notice |
| USB pulled mid-copy | `.partial` + verify + rename; clear Arabic failure message |
| Data-entry mistakes | Duplicate warning, undo, soft delete, audit trail |
| Print driver quirks | Direct print falls back to preview; preview → standard printer dialog |
| Sensitive data on a shared PC | Windows account password and BitLocker recommended in README; no app-level encryption (key loss would destroy data); USB copies must be kept physically secure |
| Multiple Windows accounts on one PC | Data is per-user (`%LOCALAPPDATA%`); README states the officer must always use the same account (or set `COURTDESK_HOME` to a shared folder) |

### 11.2 Executive decisions recorded (also copy into `DECISIONS.md`)
Stack fixed (§2). No login, no encryption (§1.4, §11.1). Soft-delete + undo + immutable audit instead of hard delete. Western digits in UI/print; input accepts both. Roster is A4 **landscape**, black-and-white-safe. Court/circuit/custody are user-editable lookups seeded with neutral examples. Quick-offset buttons default 7/15/30/45 days and are configurable (no legal rule is hard-coded). Weekend = Friday + Saturday (warn, never block). Decisions can only be recorded for sessions dated ≤ today (otherwise the date must be corrected first). `OVERDUE` is a first-class state.

### 11.3 Assumptions
One officer, one PC, one Windows account; fewer than 20,000 records; a printer may or may not exist; USB flash drive appears as a removable drive; the officer can read Arabic and large type; court names/circuits are entered by the user (no official list is bundled).

---

---

## Appendix A — Arabic string catalog (`Resources/Strings.cs`)
Separator note: in rows listing several keys, `/` or `؛` separates keys and values in matching order; `؛` is used where a value itself contains `/`.

**Register rule:** *legal/status terms* are standard Egyptian police/court vocabulary and stay formal; *guidance and helper messages* are short, spoken-friendly Egyptian Arabic addressed to a male user. `{0}` placeholders are positional. Do not translate or "improve" wording without a reason recorded in `DECISIONS.md`.

| Key | Arabic |
|---|---|
| app.title | سجل المحبوسين والجلسات |
| nav.register / schedule / history / backup / help / exit | السجل الرئيسي / جدول الجلسات / سجل القرارات / النسخ الاحتياطي والإعدادات / مساعدة / إغلاق البرنامج |
| font.smaller / font.larger | حرف أصغر / حرف أكبر |
| search.placeholder | اكتب اسم المتهم أو رقم القضية للبحث… |
| search.clear | مسح |
| search.none | مفيش نتايج للبحث ده. |
| search.none_in_filter | مفيش نتايج بين «{0}». فيه {1} نتيجة في «الكل». |
| search.show_all | اعرض الكل |
| filter.detained / all / released / judged / nodate | المحبوسون / الكل / المفرج عنهم / صدر فيهم حكم / بدون ميعاد جلسة |
| action.add / edit / decide / card / void | إضافة متهم جديد / تعديل البيانات / تسجيل قرار الجلسة / بطاقة المتهم وسجله / إلغاء السجل (إدخال خاطئ) |
| hint.select_row | اختار متهم من الجدول الأول. |
| grid.index ؛ name ؛ case ؛ court ؛ session ؛ state ؛ charges ؛ custody | م ؛ الاسم ؛ رقم القضية ؛ المحكمة / الدائرة ؛ ميعاد الجلسة ؛ الحالة ؛ التهمة ؛ مكان الحجز |
| grid.wide_on / wide_off | عرض كل الأعمدة / عرض مختصر |
| case.format | {0} لسنة {1} |
| status.detained / released / judged | محبوس / مُفرج عنه / صدر حكم |
| state.today / overdue / upcoming / nodate | جلسة اليوم / فاتت الجلسة / جلسة قادمة / بدون ميعاد |
| overdue.days | فاتت من {0} يوم |
| side.title | جلسات اليوم |
| side.progress | تم تسجيل {0} من {1} |
| side.pending / decided | في انتظار القرار / تم تسجيل القرار |
| side.overdue | جلسات فاتت من غير قرار ({0}) |
| side.tomorrow | جلسات بكرة: {0} |
| side.empty | مفيش جلسات النهارده. |
| side.record | تسجيل القرار |
| side.queue | سجّل قرارات الجلسات واحد ورا التاني |
| side.undo | تراجع |
| side.print | طباعة كشف الترحيل (A4) |
| side.preview | معاينة قبل الطباعة |
| print.sent | تم إرسال الكشف للطابعة. |
| print.fail | مش قادرين نطبع (مفيش طابعة أو الطابعة مش شغالة). فتحنا لك المعاينة. |
| print.empty | مفيش متهمين في كشف النهارده. |
| print.choose | اختيار طابعة تانية |
| form.add.title / edit.title | إضافة متهم جديد / تعديل بيانات المتهم |
| field.name | اسم المتهم (رباعي) |
| field.national_id | الرقم القومي (اختياري) |
| field.case_number / case_year / case_type | رقم القضية / سنة القضية / نوع القضية |
| field.charges / court / circuit / custody | التهمة / المحكمة / الدائرة / مكان الحجز |
| field.session_date / detained_since / notes | ميعاد الجلسة / محبوس من تاريخ / ملاحظات |
| field.required | (مطلوب) |
| field.no_date | بدون ميعاد |
| date.today / tomorrow | اليوم / بكرة |
| add_new / add_new_circuit | + إضافة جديد / + إضافة دائرة جديدة |
| form.save / cancel | حفظ / إلغاء |
| val.name | اكتب اسم المتهم. |
| val.case_number | اكتب رقم القضية (أرقام فقط). |
| val.case_year | اكتب سنة القضية (4 أرقام). |
| val.national_id | الرقم القومي لازم يكون 14 رقم. |
| val.date_invalid | التاريخ مش صحيح. |
| val.circuit_court | الدائرة دي مش تابعة للمحكمة المختارة. |
| val.summary | الحفظ مش ممكن لأن: |
| dup.warn | فيه متهم مسجل بنفس الاسم ونفس رقم القضية. تحب تفتح السجل الموجود ولا تكمّل الإضافة؟ |
| dup.open / dup.add | فتح السجل الموجود / إضافة برضه |
| saved | اتحفظت البيانات. |
| void.title | إلغاء السجل |
| void.sentence | هتلغي سجل {0} (قضية {1}). السجل مش هيظهر في القوايم، وتقدر تسترجعه بعدين من سلة المحذوفات. |
| void.reason1 / reason2 | إدخال خاطئ / مكرر |
| void.yes / no | أيوه، إلغاء السجل / لأ، سيبه |
| trash.restore | استرجاع السجل |
| wiz.title | تسجيل قرار الجلسة |
| wiz.step | الخطوة {0} من {1} |
| wiz.q1 | إيه قرار المحكمة؟ |
| dec.renew / postpone / release / bail / judgment / reopen | تجديد الحبس / تأجيل الجلسة / إخلاء سبيل / إخلاء سبيل بكفالة / صدر حكم / إعادة للحبس |
| wiz.session_held | جلسة يوم {0} |
| wiz.q2 | ميعاد الجلسة الجاية امتى؟ |
| wiz.offset | بعد {0} يوم |
| wiz.pick_date | اختار يوم تاني من التقويم |
| wiz.chosen | الجلسة الجاية: {0} (بعد {1} يوم) |
| wiz.reason | السبب (اختياري) |
| wiz.change_court | تغيير المحكمة أو الدائرة |
| wiz.bail_amount | مبلغ الكفالة بالجنيه (اختياري) |
| wiz.note / judgment_note | ملاحظات (اختياري) / منطوق الحكم (اختياري) |
| warn.weekend | اليوم ده {0} والمحاكم غالبًا مقفولة. |
| warn.past | التاريخ ده في الماضي. |
| warn.far | التاريخ بعيد (بعد {0} يوم). |
| warn.confirm | أيوه، متأكد من التاريخ ده |
| err.next_required | اختار ميعاد الجلسة الجاية الأول. |
| err.next_after | ميعاد الجلسة الجاية لازم يكون بعد ميعاد الجلسة الحالية ({0}). |
| err.stale | السجل ده اتغيّر وانت بتسجل. افتحه من جديد وراجع بياناته. |
| err.future_session | ميعاد جلسة المتهم ده لسه ماجاش ({0}). لو الميعاد غلط، عدّله من «تعديل البيانات». |
| err.no_session | المتهم ده مالوش ميعاد جلسة متسجل. عدّل بياناته وحط الميعاد الأول. |
| err.generic | حصلت مشكلة بسيطة. بياناتك في أمان. لو اتكررت بلّغ المسؤول برقم الخطأ: {0} |
| err.reopen_record | افتح السجل من جديد |
| wiz.q3 | راجع قبل الحفظ |
| wiz.sentence.renew | المحكمة قررت تجديد حبس {0} (قضية {1})، والجلسة الجاية يوم {2}. |
| wiz.sentence.postpone | المحكمة قررت تأجيل قضية {0} ({1}) لجلسة يوم {2}. |
| wiz.sentence.release | المحكمة قررت إخلاء سبيل {0} (قضية {1}). هيتنقل لقايمة المفرج عنهم. |
| wiz.sentence.bail | المحكمة قررت إخلاء سبيل {0} (قضية {1}) بكفالة {2} جنيه. هيتنقل لقايمة المفرج عنهم. |
| wiz.sentence.bail_nomoney | المحكمة قررت إخلاء سبيل {0} (قضية {1}) بكفالة. هيتنقل لقايمة المفرج عنهم. |
| wiz.sentence.judgment | صدر حكم في قضية {0} ({1}). هيتنقل لقايمة «صدر فيهم حكم». |
| wiz.sentence.reopen | هيرجع {0} (قضية {1}) لقايمة المحبوسين، والجلسة الجاية يوم {2}. |
| wiz.save / back / next / cancel / close | حفظ القرار / رجوع / التالي / إلغاء / إغلاق |
| wiz.saving | جارٍ الحفظ… |
| wiz.done | اتحفظ القرار. |
| wiz.next_date_line | الجلسة الجاية: {0} |
| wiz.moved_to | اتنقل لقايمة «{0}». |
| wiz.next_person | المتهم الجاي: {0} |
| wiz.queue_progress / stop | {0} من {1} / وقّف هنا |
| wiz.undo | تراجع عن القرار ده |
| undo.confirm | هتتراجع عن قرار «{0}» للمتهم {1} وترجّع بياناته زي ما كانت. متأكد؟ |
| undo.yes / no | أيوه، تراجع / لأ، سيبه |
| exit.confirm / yes / no | تقفل البرنامج؟ / أيوه، اقفل / لأ، ارجع |
| ok | تمام |
| bk.title | النسخ الاحتياطي |
| bk.usb | حفظ نسخة على الفلاشة |
| bk.usb.insert | ادخل الفلاشة في الجهاز، وهنكمل لوحدنا أول ما نشوفها. |
| bk.usb.retry | حاول تاني |
| bk.usb.choose | اختار الفلاشة اللي عايز تحفظ عليها: |
| bk.usb.ok | اتحفظت النسخة بنجاح على الفلاشة ({0}). تقدر تسحبها بعد ما تضغط «إزالة الجهاز بأمان». |
| bk.usb.fail | ما اتحفظتش النسخة. {0} |
| bk.usb.fail.removed / full / ro / unknown | الفلاشة اتشالت أثناء الحفظ. / مفيش مساحة كفاية على الفلاشة. / الفلاشة مقفولة للكتابة. / حصلت مشكلة غير متوقعة. |
| bk.last_usb | آخر نسخة على الفلاشة: {0} |
| bk.usb_old | آخر نسخة على الفلاشة كانت من {0} يوم. الأفضل تحفظ نسخة جديدة دلوقتي. |
| bk.never_usb | لسه ماحفظتش نسخة على الفلاشة. |
| bk.last_exit | آخر نسخة عند الخروج: {0} |
| bk.restore | استرجاع نسخة قديمة |
| bk.restore.compare | النسخة المختارة: {0} متهم، بتاريخ {1}. النسخة الحالية: {2} متهم. |
| bk.restore.safe | هنحفظ نسخة من بياناتك الحالية الأول عشان ماتضيعش. |
| bk.restore.go / back | استرجاع / رجوع |
| bk.csv | تصدير كل المتهمين لملف إكسل |
| bk.auto_note | البرنامج بيحفظ نسخة لوحده كل ما تقفله وكل شوية أثناء الشغل. |
| bk.open_folder | فتح مكان البيانات |
| recover.notice | حصلت مشكلة بسبب انقطاع الكهربا. رجّعنا آخر نسخة سليمة بتاريخ {0}. ممكن تكون آخر عمليات اتسجلت من حوالي {1} دقيقة اتفقدت، راجع آخر القرارات. |
| recover.unclean | البرنامج اتقفل فجأة المرة اللي فاتت (غالبًا الكهربا قطعت). اتفحصت البيانات وهي سليمة. |
| recover.none | مفيش نسخة سليمة نرجّعها. الملف التالف اتحفظ على جنب ومامسّيناش. بلّغ المسؤول. |
| recover.new_db / close | ابدأ قاعدة جديدة فاضية / إغلاق |
| busy.other | البرنامج مفتوح في مكان تاني. |
| db.newer | قاعدة البيانات أحدث من البرنامج. |
| warn.fs | القرص ده مش NTFS، والأفضل تحفظ نسخة على الفلاشة كل يوم. |
| warn.space | المساحة الفاضية على الجهاز قليلة. |
| clock.warn | تاريخ الجهاز غلط على الأغلب. التاريخ الحالي {0} لكن آخر استخدام للبرنامج كان {1}. صحّح التاريخ في ويندوز قبل ما تسجل أي قرار. |
| clock.open / ok | فتح إعدادات التاريخ / التاريخ صحيح — كمّل |
| clock.banner | تاريخ الجهاز مشكوك فيه |
| rollover.toast | اليوم اتغيّر — اتحدّثت قايمة جلسات اليوم. |
| invariant.warn | تنبيه: فيه تعارض في بيانات متهم رقم {0}. كمّل شغلك وبلّغ المسؤول. |
| firstrun.title | أهلًا بيك |
| firstrun.station / directorate / operator | اسم القسم / اسم المديرية / اسم القائم بالتسجيل |
| firstrun.go | ابدأ |
| settings.offsets / direct_print / lookups / trash | الأيام السريعة / الطباعة المباشرة / إدارة القوائم / سلة المحذوفات |
| help.title | مساعدة |
| help.1 | 1- دوّر على المتهم: اكتب جزء من الاسم أو رقم القضية في خانة البحث. |
| help.2 | 2- جلسات النهارده: القايمة اللي على اليمين فيها كل اللي عندهم جلسة. اضغط «طباعة كشف الترحيل» وهتطلع الورقة. |
| help.3 | 3- لما قوة الترحيل ترجع: اضغط «تسجيل القرار» قدام كل اسم، واختار قرار المحكمة، وحدد ميعاد الجلسة الجاية، وبعدين «حفظ القرار». |
| help.4 | 4- لو غلطت: بعد الحفظ اضغط «تراجع عن القرار ده». |
| help.5 | 5- النسخ الاحتياطي: البرنامج بيحفظ نسخة لوحده. كل كام يوم ادخل فلاشة واضغط «حفظ نسخة على الفلاشة». |
| help.6 | 6- لو الكهربا قطعت: ماتقلقش، اللي اتحفظ ما بيضيعش. افتح البرنامج تاني وكمّل. |
| roster.ministry | وزارة الداخلية |
| roster.directorate | مديرية أمن {0} |
| roster.station | قسم شرطة {0} |
| roster.title | كشف ترحيل المحبوسين إلى المحاكم |
| roster.date | ليوم {0} الموافق {1} |
| roster.cols (7 headers, separated by ؛) | م ؛ اسم المتهم ؛ رقم القضية ؛ التهمة ؛ المحكمة / الدائرة ؛ مكان الحجز ؛ ملاحظات |
| roster.count | إجمالي عدد المحبوسين: {0} |
| roster.page | صفحة {0} من {1} |
| roster.printed | طُبع بتاريخ {0} الساعة {1} |
| roster.sig.prepared / custody / escort / chief | القائم بالتحضير / ضابط الحجز / رئيس قوة الترحيل (استلام) / مأمور / رئيس القسم |
| roster.sig.caption | الاسم والتوقيع |
| roster.receipt | وقت التسليم: ______ تاريخ: ______ |
| csv.headers | م, الاسم, الرقم القومي, رقم القضية, سنة القضية, نوع القضية, التهمة, المحكمة, الدائرة, مكان الحجز, ميعاد الجلسة, الحالة, محبوس من, ملاحظات |
| weekdays | الأحد، الاثنين، الثلاثاء، الأربعاء، الخميس، الجمعة، السبت |
| months | يناير، فبراير، مارس، أبريل، مايو، يونيو، يوليو، أغسطس، سبتمبر، أكتوبر، نوفمبر، ديسمبر |

---

## Appendix B — Settings keys (table `settings`, defaults written at first run)
| Key | Default | Meaning |
|---|---|---|
| `first_run_done` | `0` | first-run dialog completed |
| `station_name` / `directorate_name` | empty | roster header |
| `roster_header_line1` | `وزارة الداخلية` | top header line |
| `operator_name` | empty | stamped on audit events and roster |
| `font_scale` | `100` | 100 / 125 / 150 |
| `grid_wide_mode` | `0` | show charges and custody columns |
| `last_filter` | `DETAINED` | last filter chip |
| `quick_offsets` | `7,15,30,45` | next-date quick buttons (days) |
| `print_direct` | `1` | one-click direct print to default printer |
| `usb_backup_reminder_days` | `7` | amber reminder threshold |
| `last_usb_backup_utc` | empty | last verified USB export |
| `secondary_backup_dir` | empty | optional second copy of exit backups |
| `shortcut_created` | `0` | desktop shortcut auto-created once |

## Appendix C — Quick reference of invariants the implementation must never violate
1. A write is one `BEGIN IMMEDIATE … COMMIT` on the single `DbThread` connection; nothing else touches the DB.
2. `synchronous=FULL`, `journal_mode=WAL`, `locking_mode=EXCLUSIVE`, `foreign_keys=ON` — read back and verified at every open.
3. Calendars are queries; there is exactly one stored copy of a defendant's session date.
4. Every state change appends exactly one audit event in the same transaction; audit rows are never deleted or edited (except the `undone_by` marker).
5. The live database file is never copied with the file system; backups use the SQLite backup API → temp file → verify → atomic rename.
6. "Today" is `IClock.Today` (local date) and nothing else.
7. No network code; no Arabic text outside `Strings.cs`; no UI element below 18 DIP text or 48 DIP target.
