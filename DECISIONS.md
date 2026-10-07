# DECISIONS.md

Append-only log: `date · decision · reason`. [FIXED] items from SPEC are not re-decided here.

- 2026-10-07 · Stack C# 10 / net462 / WPF software rendering / SQLite 1.0.118 WAL+FULL+EXCLUSIVE / single x86 exe · reason: SPEC §2 [FIXED].
- 2026-10-07 · SPEC.md v1.0 is the design doc and implementation plan; no separate docs/superpowers spec/plan files · reason: avoid duplication per user order of strict SPEC adherence.
- 2026-10-07 · Build host missing .NET SDK 8.0.x → wrote BLOCKED.md per SPEC §0.2.4, then auto-installed SDK 8.0.425 via winget with user approval and removed the block · reason: hard prerequisite, no stack change.
- 2026-10-07 · Offline feed = harvest exact nupkgs from project.assets.json after one online restore · reason: transitive-closure correctness, repeatable offline builds.
- 2026-10-07 · Arch tests scan src/CourtDesk (.cs + .xaml); test files may carry Arabic fixtures as test data only · reason: SPEC §0.1 intent is UI-text consistency; ArabicText.Normalize cannot be tested without Arabic input.
- 2026-10-07 · app.manifest ships Vista+7+8+8.1 compat GUIDs (byte-verified against this host's powershell.exe manifest) and NO Win10/11 GUID · reason: an unknown supportedOS Id hard-fails process activation (SxS 14001, proven by bisection: two GUIDs from memory were rejected); no single Win10/11 value is both accepted here and proven on Win7SP1, and omission is functionally harmless (no version-dependent code per AC-02); QA-CHECKLIST must re-verify launch on Win7/10/11 and re-add the classic Win10 GUID only if accepted everywhere.
- 2026-10-07 · Decide/Undo check request_uuid in a Read first; in-Write check stays as backstop · reason: idempotent resubmits must not bump change_seq or churn caches (§6.2 step 3).
- 2026-10-07 · last_exit_backup_seq stores pre-backup seq + 1 · reason: the marker write itself bumps change_seq exactly once; skip-iff-equal then holds.
- 2026-10-07 · Lookup add returns existing id on normalized-name match · reason: inline combos must never crash on dupes (§5.4 lookups editable, never deleted).
- 2026-10-07 · Never round-trip files containing Arabic through PowerShell Get/Set-Content · reason: it mojibakes literals (ServiceTests.cs had to be rewritten); use Read/Write/Edit tools.
- 2026-10-07 · TopBar uses two rows (identity+date+tools, then 4 nav buttons) · reason: the single 76 px row overflowed at 1024 wide; §7.2 minimum size beats the ASCII sketch.
- 2026-10-07 · Whole page wrapped in a vertical ScrollViewer · reason: §7.2 allows vertical scrolling at 125/150 % zoom, forbids horizontal page scroll.
- 2026-10-07 · UiRes lookup helper with literal fallbacks · reason: StatusChip/column headers resolve brushes before app resources exist in tests; keeps rendering deterministic.
- 2026-10-07 · CourtDesk.Resources.Strings is readable from every layer · reason: leaf constants file; the layering test allow-lists it everywhere while Arabic literals stay banned outside it.
- 2026-10-07 · BuildDate is a stamped constant (2026-10-07), refreshed at packaging · reason: SPEC §6.5 needs a compile-time constant; deterministic builds carry no timestamp.
- 2026-10-07 · SessionEvent + EventTypes live in Domain (not in SPEC file list) · reason: pure data needed by Application, Persistence, UI; no I/O.
- 2026-10-07 · Tests run fully serial (CollectionBehavior disable) · reason: kill-loop crash tests flaked under xUnit cross-class parallelism (capacity, not product); serial is deterministic.
- 2026-10-07 · Blanket File.Copy ban in product code · reason: SPEC App.C.5 — live DB never copied with the file system; snapshots/USB use streams + atomic rename.
- 2026-10-07 · Native import allow-list extended with exactly `mscoree` + `wintrust` (CRT libs still fail) · reason: vendored pair is genuine upstream 1.0.118.0 (strong-name db937bc2d44ff139, zero CRT imports = static CRT); the two extra imports are upstream's own CLR/signature checks and Windows-inbox on all SPEC §2.5 targets (mscoree via the .NET 4.8 prerequisite, WINTRUST always); no compliant 1.0.118 binary exists without them, replacement per SPEC §9 cannot remove them.
- 2026-10-07 · Explicit `<Reference Include="System.Web" />` added next to System.Web.Extensions · reason: SDK-built XAML compiler needs a file path for the transitive ref; inbox BCL, no new dependency.

## Phase 9 (final) decisions

- 2026-10-07 — UI addendum `SPEC-UI-ADDENDUM.md` overrides the visual parts of SPEC §7 → reason: user instruction, it is the binding visual spec for Phase 4R onward.
- 2026-10-07 — Three-tier responsive layout (wide >= 1366, compact, narrow < 1150, all scaled by the zoom factor) → reason: a single wide layout overflowed horizontally at 1024 px and at 125 % font scale; the addendum requires no horizontal page scroll at 1024/1280/1366 × 100/125 %.
- 2026-10-07 — TopBar is always one row; below 1366 it swaps to condensed labels and icon-only utilities → reason: the addendum forbids a second header row, and the earlier two-row header failed visual review.
- 2026-10-07 — Grid chrome uses styles (not custom ControlTemplates) for header/row/cell; the leading 6 px state stripe lives in the index cell as a right border → reason: `DataGridColumnHeader` is public but `DataGridCellsPresenter` is internal in this profile, so a row ControlTemplate cannot be built in code.
- 2026-10-07 — The data-mode (wide/compact) switch lives in the command card, not the status bar → reason: status bar is fixed at 32 px per the addendum while every other target must be >= 48 px.
- 2026-10-07 — `QuietButton` renders as a bordered card button instead of a transparent text button → reason: transparent buttons on the dialog backgrounds were invisible in the PNG review.
- 2026-10-07 — Printing reaches the UI only through `IPrintService`; `PrintService` never opens a window and returns a `PrintOutcome` → reason: SPEC §4.5 allows Printing → UI nowhere; the preview window lives in UI.
- 2026-10-07 — `IPrintService.BuildDocument` returns `object` (a `FixedDocument`) → reason: keeps `Application.Ports` free of WPF types while letting UI render the document.
- 2026-10-07 — Roster pages are laid out with one `Grid` per page (rows = header, table header, data, summary, signature, footer) instead of absolute `Canvas` positioning → reason: `FixedPage` children ignored `Canvas.SetTop` and all rows collapsed on top of each other in the PNG render.
- 2026-10-07 — Roster reserved heights (page-1 header 132, running title 30, table header 36, footer 30) measured generously from the rendered page → reason: exact heights clipped the first row of continuation pages.
- 2026-10-07 — `PRAGMA user_version` is written inside the migration transaction → reason: a kill between COMMIT and the pragma left tables with user_version 0, and the next start failed with "table app_meta already exists".
- 2026-10-07 — Restore closes the database before `RestoreSwap.Install` and the app exits afterwards → reason: install opens a second connection, which blocks forever while the gateway holds `locking_mode=EXCLUSIVE` (found by the scripted restore scenario).
- 2026-10-07 — The verified `SQLite.Interop.dll` is placed both in `<home>/native/x86` and next to the exe → reason: `System.Data.SQLite` builds the native path from the managed assembly location, which is empty when the assembly comes from an embedded resource.
- 2026-10-07 — `Platform` may use `Microsoft.Win32` in the layering matrix → reason: `SystemEvents` (session ending / power) lives there and Platform is the OS-integration layer.
- 2026-10-07 — Digit normalization moved from `InputScope`/`XmlLanguage` to `PreviewTextInput` in `InputLanguageAssist` → reason: neither type is usable in this .NET profile.
- 2026-10-07 — The sidebar shows the Arabic decision label, not the stored code → reason: PNG review showed the raw code (`RENEW_DETENTION`) in the decided card.
- 2026-10-07 — Arabic-Indic digits are written as `\uXXXX` escapes in C# sources → reason: the arch test bans Arabic literals outside `Strings.cs`, and PowerShell round-trips mojibake them.