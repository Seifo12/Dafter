# QA-CHECKLIST.md — CourtDesk

Manual acceptance for a delivery. Run on the target PC, tick every box.
AC ids refer to `SPEC.md` §3. Arabic quotes are from the in-app text.

## 0. Build host (before shipping)

- [ ] `powershell -File tools\Fetch-Vendor.ps1` completed once (Phase 0)
- [ ] `build.cmd` exits 0 (Phase 8)
- [ ] `dist\CourtDesk.exe` and `dist\CourtDesk.exe.sha256` exist (Phase 8)
- [ ] `dist\kit\` contains exe + README + PREREQUISITES + `اقرأني.txt` (Phase 9)
- [ ] `dotnet test ... -c Release` is 100 % green (Phase 9)
- [ ] `CourtDesk.exe --selftest --home <temp>` exits 0 and prints `selftest OK` (Phase 8)

## 1. Launch and first run (AC-18, AC-17)

- [ ] Splash appears in < 400 ms; usable window < 3 s
- [ ] Window opens maximised, RTL, Arabic, at 1366x768 and at 1024x768
- [ ] First run creates `%LOCALAPPDATA%\CourtDesk\db\courtdesk.db`
- [ ] First run extracts `x86\SQLite.Interop.dll` next to the exe and verifies it
- [ ] Desktop shortcut appears
- [ ] Launching a second copy shows the friendly "another copy is running" message

## 2. Record keeping (AC-01 … AC-05)

- [ ] «إضافة متهم» saves and the row shows in «السجل»
- [ ] Arabic-Indic digits typed in a field are stored as plain digits
- [ ] National ID rejects 13 or 15 digits, accepts 14
- [ ] Case year outside 1950..2100 is refused with a sentence, not a grey button
- [ ] Soft delete only: a deleted defendant disappears from lists but rows stay
- [ ] Duplicate detection (name + case + year) warns before saving

## 3. Decision workflow (AC-06 … AC-09)

- [ ] Wizard step 1 offers exactly the codes for the current status
- [ ] «تجديد» requires a next date; «فراج»/«حكم» do not
- [ ] «تأجيل» offers the configured offsets
- [ ] Double-click on the wizard button records one event only (idempotent)
- [ ] Stale version (two windows) shows "someone else changed it", no data loss
- [ ] «تراجع» restores the previous state and keeps both events visible in «القرارات»
- [ ] Editing a decided record requires an explicit reason

## 4. Search, filters, grid (AC-10, AC-11)

- [ ] Search filters on every keystroke, by name and by case number
- [ ] Chips (all / today / overdue / upcoming / decided) switch and survive restart
- [ ] Grid shows two lines per row, header row repeats, no whole-row colour tint
- [ ] Leading 6 px state stripe matches the chip colour and the word
- [ ] Selecting a row draws the 2 px primary outline
- [ ] 200 rows scroll smoothly; at least 6 rows visible at 1366x768
- [ ] Wide/compact switch in the command card adds and removes charges/custody

## 5. Sidebar and clock (AC-12, AC-13)

- [ ] Today's sessions list shows name + case + court + custody
- [ ] Overdue tab shows its count and appears only when count > 0
- [ ] No sessions today -> the default tab is overdue when overdue > 0
- [ ] Decided cards collapse to 5 with a "+N" hint
- [ ] Print button is pinned and always fully inside the sidebar
- [ ] Setting the clock one day back shows the warning dialog

## 6. Printing (AC-16)

- [ ] «طباعة كشف الترحيل» prints A4 landscape; page header, repeated table
      header, summary line, 4 signature boxes and footer on every page
- [ ] Roster includes pending + decided-today, excludes undone and REOPEN
- [ ] Numbering restarts at 1 on each print
- [ ] Empty roster shows "nothing to print" and prints nothing
- [ ] No printer installed -> message + preview opens automatically
- [ ] Preview window prints with «طباعة» / «اختيار طابعة تانية» / «إغلاق»
- [ ] Black and white laser print is legible (no reliance on colour)

## 7. Backups and recovery (AC-14, AC-15)

- [ ] Auto snapshot appears in `backups\auto` within the configured interval
- [ ] The auto ring never exceeds 10 files
- [ ] Closing the app writes `backups\exit\exit-*.db` once per change
- [ ] USB export verifies the copy and reports the final path
- [ ] Removing the USB mid-copy reports the failure, no partial file is trusted
- [ ] Restore writes a pre-restore copy, then asks for a restart
- [ ] Restoring returns the earlier roster and all invariants hold
- [ ] Corrupt the DB file, start the app: it quarantines, restores the newest
      verified snapshot and shows an Arabic notice

## 8. Kill tests (scripted, Category=Crash)

- [ ] 300 kills during normal ops: `quick_check` ok, I1..I6 hold, no partial op
- [ ] 300 kills during backup: same guarantees, main DB untouched
- [ ] Kill during migration leaves either the old or the new schema, never both

## 9. Accessibility (AC-16, SPEC §7.2)

- [ ] `Category=LayoutAudit` green for 3 sizes × 2 font scales × 2 datasets
- [ ] No horizontal page scroll at 1366x768, 1280x720, 1024x768 at 100 % and 125 %
- [ ] Every button/field target is >= 48 px (chips 40 px, status bar 32 px)
- [ ] Every state has colour + icon + word (never colour alone)
- [ ] Every button has an Arabic label; icons only accompany text
- [ ] `Category=Arch` green: no Arabic outside `Strings.cs`, no font < 18

## 10. Logging and platform

- [ ] `logs\app-YYYYMMDD.log` is written; keeps the newest 30 files
- [ ] Closing the Windows session writes an exit snapshot
- [ ] Second instance refuses to start
- [ ] Unhandled exception logs and shows a friendly Arabic message

## 11. Deliverable

- [ ] `dist\kit\اقرأني.txt` matches the in-app help text
- [ ] `README.md` build/deploy/prerequisite steps work on a clean machine
- [ ] `PROGRESS.md` has the final entry and `DECISIONS.md` is complete
- [ ] User confirms the walk-through of AC-18: first-time user reaches
      "print roster" and "record a decision" with zero configuration