using System;
using System.Collections.Generic;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public static class SettingKeys
    {
        public const string FirstRunDone = "first_run_done";
        public const string StationName = "station_name";
        public const string DirectorateName = "directorate_name";
        public const string RosterHeaderLine1 = "roster_header_line1";
        public const string OperatorName = "operator_name";
        public const string FontScale = "font_scale";
        public const string GridWideMode = "grid_wide_mode";
        public const string LastFilter = "last_filter";
        public const string QuickOffsets = "quick_offsets";
        public const string PrintDirect = "print_direct";
        public const string UsbBackupReminderDays = "usb_backup_reminder_days";
        public const string LastUsbBackupUtc = "last_usb_backup_utc";
        public const string PostponeOffsets = "postpone_offsets";
        public const string SecondaryBackupDir = "secondary_backup_dir";
        public const string ShortcutCreated = "shortcut_created";
    }

    public static class SettingDefaults
    {
        public const string FirstRunDone = "0";
        public const string Empty = "";
        public const string FontScale = "100";
        public const string GridWideMode = "0";
        public const string LastFilter = "DETAINED";
        public const string QuickOffsets = "7,15,30,45";
        public const string PrintDirect = "1";
        public const string UsbBackupReminderDays = "7";
    }

    public sealed class SettingsService
    {
        private readonly IDatabase _db;
        private readonly ISettingsStore _store;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.Ordinal);

        public SettingsService(IDatabase db, ISettingsStore store)
        {
            _db = db;
            _store = store;
            SeedDefaults();
            foreach (string key in Keys)
            {
                _cache[key] = Get(key);
            }
        }

        public static readonly string[] Keys = new string[]
        {
            SettingKeys.FirstRunDone, SettingKeys.StationName, SettingKeys.DirectorateName,
            SettingKeys.RosterHeaderLine1, SettingKeys.OperatorName, SettingKeys.FontScale,
            SettingKeys.GridWideMode, SettingKeys.LastFilter, SettingKeys.QuickOffsets,
            SettingKeys.PrintDirect, SettingKeys.UsbBackupReminderDays, SettingKeys.LastUsbBackupUtc,
            SettingKeys.SecondaryBackupDir, SettingKeys.ShortcutCreated
        };

        private void SeedDefaults()
        {
            _db.Write(s =>
            {
                foreach (string key in Keys)
                {
                    if (_store.Get(s, key) != null)
                    {
                        continue;
                    }
                    _store.Set(s, key, DefaultFor(key));
                }
                return 0;
            });
        }

        private static string DefaultFor(string key)
        {
            switch (key)
            {
                case SettingKeys.FirstRunDone: return SettingDefaults.FirstRunDone;
                case SettingKeys.RosterHeaderLine1: return CourtDesk.Resources.Strings.RosterMinistry;
                case SettingKeys.FontScale: return SettingDefaults.FontScale;
                case SettingKeys.GridWideMode: return SettingDefaults.GridWideMode;
                case SettingKeys.LastFilter: return SettingDefaults.LastFilter;
                case SettingKeys.QuickOffsets: return SettingDefaults.QuickOffsets;
                case SettingKeys.PrintDirect: return SettingDefaults.PrintDirect;
                case SettingKeys.UsbBackupReminderDays: return SettingDefaults.UsbBackupReminderDays;
                default: return SettingDefaults.Empty;
            }
        }

        public string Get(string key)
        {
            lock (_cache)
            {
                string v;
                if (_cache.TryGetValue(key, out v))
                {
                    return v;
                }
            }
            string read = _db.Read(s => _store.Get(s, key));
            lock (_cache)
            {
                _cache[key] = read;
            }
            return read;
        }

        public void Set(string key, string value)
        {
            _db.Write(s =>
            {
                _store.Set(s, key, value);
                return 0;
            });
            lock (_cache)
            {
                _cache[key] = value;
            }
        }

        public int GetInt(string key, int fallback)
        {
            int n;
            string v = Get(key);
            if (v != null && int.TryParse(v, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
            return fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            string v = Get(key);
            if (v == "1") return true;
            if (v == "0") return false;
            return fallback;
        }

        public string OperatorOrUnknown()
        {
            string v = Get(SettingKeys.OperatorName);
            if (string.IsNullOrEmpty(v) || v.Trim().Length == 0)
            {
                return CourtDesk.Resources.Strings.OperatorUnknown;
            }
            return v.Trim();
        }

        public int[] QuickOffsets()
        {
            string raw = Get(SettingKeys.QuickOffsets);
            var out_ = new List<int>();
            foreach (string part in (raw ?? "").Split(','))
            {
                int n;
                if (int.TryParse(part.Trim(), out n) && n > 0 && n <= 1100)
                {
                    out_.Add(n);
                }
            }
            if (out_.Count == 0)
            {
                out_.AddRange(new int[] { 7, 15, 30, 45 });
            }
            return out_.ToArray();
        }

        public string LastUsbBackupUtc()
        {
            return Get(SettingKeys.LastUsbBackupUtc);
        }
    }
}
