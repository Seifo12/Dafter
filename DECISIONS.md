# DECISIONS.md

Append-only log: `date · decision · reason`. [FIXED] items from SPEC are not re-decided here.

- 2026-10-07 · Stack C# 10 / net462 / WPF software rendering / SQLite 1.0.118 WAL+FULL+EXCLUSIVE / single x86 exe · reason: SPEC §2 [FIXED].
- 2026-10-07 · SPEC.md v1.0 is the design doc and implementation plan; no separate docs/superpowers spec/plan files · reason: avoid duplication per user order of strict SPEC adherence.
- 2026-10-07 · Build host missing .NET SDK 8.0.x → wrote BLOCKED.md per SPEC §0.2.4, then auto-installed SDK 8.0.425 via winget with user approval and removed the block · reason: hard prerequisite, no stack change.
- 2026-10-07 · Offline feed = harvest exact nupkgs from project.assets.json after one online restore · reason: transitive-closure correctness, repeatable offline builds.
- 2026-10-07 · Arch tests scan src/CourtDesk (.cs + .xaml); test files may carry Arabic fixtures as test data only · reason: SPEC §0.1 intent is UI-text consistency; ArabicText.Normalize cannot be tested without Arabic input.
- 2026-10-07 · app.manifest ships Vista+7+8+8.1 compat GUIDs (byte-verified against this host's powershell.exe manifest) and NO Win10/11 GUID · reason: an unknown supportedOS Id hard-fails process activation (SxS 14001, proven by bisection: two GUIDs from memory were rejected); no single Win10/11 value is both accepted here and proven on Win7SP1, and omission is functionally harmless (no version-dependent code per AC-02); QA-CHECKLIST must re-verify launch on Win7/10/11 and re-add the classic Win10 GUID only if accepted everywhere.
- 2026-10-07 · SessionEvent + EventTypes live in Domain (not in SPEC file list) · reason: pure data needed by Application, Persistence, UI; no I/O.
- 2026-10-07 · Tests run fully serial (CollectionBehavior disable) · reason: kill-loop crash tests flaked under xUnit cross-class parallelism (capacity, not product); serial is deterministic.
- 2026-10-07 · Blanket File.Copy ban in product code · reason: SPEC App.C.5 — live DB never copied with the file system; snapshots/USB use streams + atomic rename.
- 2026-10-07 · Native import allow-list extended with exactly `mscoree` + `wintrust` (CRT libs still fail) · reason: vendored pair is genuine upstream 1.0.118.0 (strong-name db937bc2d44ff139, zero CRT imports = static CRT); the two extra imports are upstream's own CLR/signature checks and Windows-inbox on all SPEC §2.5 targets (mscoree via the .NET 4.8 prerequisite, WINTRUST always); no compliant 1.0.118 binary exists without them, replacement per SPEC §9 cannot remove them.
- 2026-10-07 · Explicit `<Reference Include="System.Web" />` added next to System.Web.Extensions · reason: SDK-built XAML compiler needs a file path for the transitive ref; inbox BCL, no new dependency.
