CourtDesk vendored dependencies (offline-repeatable builds).
- managed/System.Data.SQLite.dll + native/x86/SQLite.Interop.dll: Stub.System.Data.SQLite.Core.NetFramework 1.0.118 (lib/net46 + build/net46/x86, same package).
- packages/*.nupkg: offline NuGet feed harvested from project.assets.json after one online restore.
Verify: compare Get-FileHash output against MANIFEST.sha256.
