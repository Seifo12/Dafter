# CourtDesk remaining features plan (2026-10-09)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the eight open items the user reported: Excel import/export in the backup screen, a tomorrow tab, small-screen header fixes, roster decision columns, a medical-notes field, optional defendant photos, and the freeze.

**Architecture:** Every task is TDD with a regression test that fails before the change. Persistence changes go through one migration that bumps `user_version`; the app already stamps it atomically. Excel uses the hand-written `Xlsx` engine (no external dependency). Images are files on disk referenced by a new table, never blobs, so backups stay small.

**Tech Stack:** .NET Framework 4.6.2, WPF, SQLite (System.Data.SQLite), xUnit, hand-rolled OOXML writer.

**Spec:** `SPEC.md`, `FIX-ROUND-2.md`, plus this conversation's decisions.

## Global Constraints
- No new NuGet packages. Only what is in `vendor/packages` (SQLite only).
- Arabic copy lives only in `src/CourtDesk/Resources/Strings.cs`.
- No custom `ControlTemplate` may break input: any control that receives typing keeps the WPF default template and is styled through setters only.
- Every task ends with `dotnet test tests\CourtDesk.Tests\CourtDesk.Tests.csproj -c Release` green and a commit.

---

### Task 1: Excel import/export inside the backup screen

**Files:**
- Create: `src/CourtDesk/UI/Dialogs/ExcelTransferView.xaml` + `.xaml.cs`
- Modify: `src/CourtDesk/UI/Dialogs/BackupSettingsView.xaml`
- Modify: `src/CourtDesk/Bootstrap/CompositionRoot.cs` (build `ExcelTransferService`, add `public ExcelTransferService Excel;` to `Composition`)
- Modify: `src/CourtDesk/UI/UiCoordinator.cs` (add `OpenExcelTransfer()`; the new service goes into `CompositionLike`)
- Modify: `src/CourtDesk/Resources/Strings.cs` (`ExcelTitle`, `ExcelImport`, `ExcelExport`, `ExcelTemplate`, `ExcelDone`, `ExcelCancelled`, `ExcelPick`)
- Test: `tests/CourtDesk.Tests/ExcelTransferTests.cs` (extend)

**Interfaces:**
- Consumes: `ExcelTransferService.WriteTemplate(path)`, `.Export(path, rows)`, `.Import(path, dryRun) -> ImportReport { int Added; int Skipped; List<string> Problems; }`
- Produces: `void UiCoordinator.OpenExcelTransfer()`, `string UiCoordinator.ExcelTemplatePath`

- [ ] **Step 1: Write the failing test** — assert the backup view exposes three buttons whose commands are non-null and that `OpenExcelTransfer` does not throw when no window is active.
- [ ] **Step 2: Run it** — `dotnet test tests\CourtDesk.Tests\CourtDesk.Tests.csproj -c Release --filter FullyQualifiedName~ExcelTransferTests` → FAIL.
- [ ] **Step 3: Implement** — a `BackupSettingsView` row of three Quiet buttons bound to `ImportCommand` / `ExportCommand` / `TemplateCommand`; each command asks for a path through `UiCoordinator` (write path default `%USERPROFILE%\Desktop\courtdesk-<kind>-<yyyyMMdd>.xlsx`), then shows a `MessageDialog` with the report (`ExcelDone` formatted with added/skipped counts and the first three problems).
- [ ] **Step 4: Run** — the filter above → PASS.
- [ ] **Step 5: Commit** — `git commit -m "excel: import/export buttons inside the backup and settings screen"`.

### Task 2: Tomorrow tab in the today sidebar

**Files:**
- Modify: `src/CourtDesk/UI/ViewModels/TodaySidebarViewModel.cs` (`Tab` accepts `today`/`tomorrow`, `TomorrowCards`, `TomorrowTabText`, `SwitchDay` helper)
- Modify: `src/CourtDesk/UI/Views/TodaySidebar.xaml` (`UniformGrid` in `TabRow` becomes 3 columns: اليوم / بكره / فاتت)
- Modify: `src/CourtDesk/UI/Views/TodaySidebar.xaml.cs` (`ApplyBands` shows the right list per tab)
- Modify: `src/CourtDesk/Resources/Strings.cs` (`SideTabTomorrow = "بكره ({0})"`)
- Test: `tests/CourtDesk.Tests/SidebarTests.cs` (extend)

**Interfaces:**
- Produces: `ObservableCollection<SessionCard> TomorrowCards`, `string TomorrowTabText`, `bool ShowTomorrow`

- [ ] **Step 1: Failing test** — add records for today and tomorrow, switch `Tab` to `tomorrow`, assert `TomorrowCards.Count` matches and the tab text contains the count.
- [ ] **Step 2: Run** `--filter FullyQualifiedName~SidebarTests` → FAIL.
- [ ] **Step 3: Implement** — `TodayService.Snapshot` already carries tomorrow count (`TomorrowCount`); add a `Tomorrow` row list to the snapshot or read it through `Register` index; default tab stays `today`; `ApplyBands` shows `TomorrowList` for the tomorrow tab.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "sidebar: tomorrow tab beside today and overdue"`.

### Task 3: Small screens and header crowding

**Files:**
- Modify: `src/CourtDesk/UI/Shell/TopBar.xaml` + `.xaml.cs` (station line hides below 1280 * zoom; brand min width 200; nav labels trim before the title does)
- Modify: `src/CourtDesk/MainWindow.xaml` (status row: wide toggle first, then the three counters, with 8 px gaps and no overlap at 1024)
- Modify: `src/CourtDesk/UI/Views/RegisterView.xaml` (action bar wraps to two rows below 1200 instead of squeezing the void button)
- Test: `tests/CourtDesk.Tests/LayoutAudit.cs` (add 1366x600 and 1152x720 scenarios)

**Interfaces:**
- Produces: `void Bar.ApplyWidthMode(bool wide)` already exists; add `void Bar.ApplyNarrowMode(bool narrow)`

- [ ] **Step 1: Failing test** — add the two new sizes to `Sizes`; expect failures naming the brand and the wide toggle.
- [ ] **Step 2: Run** `--filter FullyQualifiedName~LayoutAudit` → FAIL.
- [ ] **Step 3: Implement** — hide the station line under 1280 * zoom, give the brand a 200 px floor with no trimming, move the wide toggle to the left of the counters in the status bar, and let the action bar wrap.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "layout: header and status bar survive small screens"`.

### Task 4: Roster prints the decision, bail and custody outcome

**Files:**
- Modify: `src/CourtDesk/Application/RosterService.cs` (add `DecisionLabel`, `BailAmount`, `DetainedSince` to `RosterEntry`)
- Modify: `src/CourtDesk/Printing/RosterDocumentBuilder.cs` (rename the last column to the decision, print bail under it, keep the page width)
- Test: `tests/CourtDesk.Tests/PrintingTests.cs` (extend `Invariants_HoldForZeroToTwoHundredRandomRows`)

- [ ] **Step 1: Failing test** — build a roster for a released-on-bail record and assert the row text contains the decision label and the bail amount.
- [ ] **Step 2: Run** `--filter FullyQualifiedName~PrintingTests` → FAIL.
- [ ] **Step 3: Implement** — populate the new fields from the last event, render them in the existing column.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "roster: print decision, bail amount and custody"`.

### Task 5: Medical notes field (migration v2)

**Files:**
- Modify: `src/CourtDesk/Persistence/MigrationRunner.cs` (version 2, `ALTER TABLE defendants ADD COLUMN medical_notes TEXT NOT NULL DEFAULT ''`)
- Modify: `src/CourtDesk/Persistence/SqliteDefendantStore.cs` (read/write the column)
- Modify: `src/CourtDesk/Domain/Defendant.cs` and `DefendantSnapshot.cs` (`MedicalNotes`)
- Modify: `src/CourtDesk/Application/DefendantService.cs` (input, create, edit, duplicates)
- Modify: `src/CourtDesk/UI/ViewModels/DefendantEditViewModel.cs` + `UI/Dialogs/DefendantEditDialog.xaml` (multi-line field under the notes)
- Modify: `src/CourtDesk/UI/Dialogs/DefendantCardDialog.xaml` (read-only block)
- Modify: `src/CourtDesk/UI/ViewModels/HistoryViewModel.cs` (audit line records edits, not the text)
- Test: `tests/CourtDesk.Tests/PersistenceTests.cs` (migration keeps data), `tests/CourtDesk.Tests/ServiceTests.cs`

- [ ] **Step 1: Failing tests** — assert `user_version == 2` after opening an old database, and that create/edit round-trips `MedicalNotes`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** — migration, model, service, UI.
- [ ] **Step 4: Run** the whole suite → PASS.
- [ ] **Step 5: Commit** — `git commit -m "medical notes: optional text field with migration v2"`.

### Task 6: Optional defendant photos (migration v3)

**Files:**
- Modify: `src/CourtDesk/Persistence/MigrationRunner.cs` (version 3, `CREATE TABLE defendant_photos(id INTEGER PRIMARY KEY, defendant_id INTEGER NOT NULL REFERENCES defendants(id), path TEXT NOT NULL, caption TEXT, taken_on TEXT, created_utc TEXT NOT NULL)` + index)
- Create: `src/CourtDesk/Application/PhotoService.cs` (save into `DbPaths.MediaDir(home)/<defendantId>/`, copy instead of referencing the source, 1600 px cap, delete, list)
- Modify: `src/CourtDesk/UI/Dialogs/DefendantCardDialog.xaml(.cs)` (strip of thumbnails, add-photo button, open full size)
- Modify: `src/CourtDesk/Bootstrap/CompositionRoot.cs` (wire `PhotoService`)
- Modify: `src/CourtDesk/Persistence/BackupService.cs` (include the media folder in snapshots)
- Test: `tests/CourtDesk.Tests/PhotoServiceTests.cs`

- [ ] **Step 1: Failing tests** — save/load/delete round trip, migration creates the table, backup lists the media folder.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** service, migration, UI strip, backup inclusion.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "photos: optional per-defendant images shown on the case card"`.

### Task 7: Freeze investigation

**Files:** none until the log exists.

- [ ] **Step 1: Ask for the log** at `%LocalAppData%\CourtDesk\logs\` from the moment the window stops responding.
- [ ] **Step 2: Reproduce** the reported flow (print preview then close, calendar popup, forced exit) under a debugger breakpoint.
- [ ] **Step 3: Fix** the single owner of the blocking call and add a regression test.
- [ ] **Step 4: Commit.**

## Self-Review
- Spec coverage: items 2–7 of the user's list map to Tasks 1–6; the freeze is Task 7.
- Placeholder scan: Task 7 is intentionally blocked on evidence, and says so.
- Type consistency: `ExcelTransferService`, `ImportReport`, `SessionCard`, `Defendant.MedicalNotes` and `PhotoService` are introduced once and reused by name.
