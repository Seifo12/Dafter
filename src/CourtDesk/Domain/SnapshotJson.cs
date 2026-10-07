using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace CourtDesk.Domain
{
    public static class SnapshotJson
    {
        private static readonly JavaScriptSerializer Reader = new JavaScriptSerializer();

        public static string Write(DefendantSnapshot s)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"id\":");
            sb.Append(s.Id);
            FieldStr(sb, "full_name", s.FullName);
            FieldStrNull(sb, "national_id", s.NationalId);
            FieldStr(sb, "case_number", s.CaseNumber);
            Field(sb, "case_year", s.CaseYear.ToString());
            FieldStr(sb, "case_type", s.CaseType);
            FieldStr(sb, "charges", s.Charges);
            Field(sb, "court_id", Null(s.CourtId));
            Field(sb, "circuit_id", Null(s.CircuitId));
            Field(sb, "custody_location_id", Null(s.CustodyLocationId));
            FieldStrNull(sb, "session_date", s.SessionDate.HasValue ? s.SessionDate.Value.ToIso() : null);
            FieldStr(sb, "status", s.Status);
            FieldStrNull(sb, "detained_since", s.DetainedSince.HasValue ? s.DetainedSince.Value.ToIso() : null);
            FieldStr(sb, "notes", s.Notes);
            sb.Append(",\"is_deleted\":");
            sb.Append(s.IsDeleted ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        public static DefendantSnapshot Read(string json)
        {
            var dict = Reader.Deserialize<Dictionary<string, object>>(json);
            var s = new DefendantSnapshot
            {
                Id = GetInt(dict, "id"),
                FullName = GetString(dict, "full_name"),
                NationalId = GetStringNull(dict, "national_id"),
                CaseNumber = GetString(dict, "case_number"),
                CaseYear = GetInt(dict, "case_year"),
                CaseType = GetString(dict, "case_type"),
                Charges = GetString(dict, "charges"),
                CourtId = GetNullableInt(dict, "court_id"),
                CircuitId = GetNullableInt(dict, "circuit_id"),
                CustodyLocationId = GetNullableInt(dict, "custody_location_id"),
                Status = GetString(dict, "status"),
                Notes = GetString(dict, "notes"),
                IsDeleted = GetBool(dict, "is_deleted")
            };
            string session = GetStringNull(dict, "session_date");
            s.SessionDate = session == null ? (LocalDate?)null : LocalDate.Parse(session);
            string since = GetStringNull(dict, "detained_since");
            s.DetainedSince = since == null ? (LocalDate?)null : LocalDate.Parse(since);
            return s;
        }

        private static void Field(StringBuilder sb, string key, string value)
        {
            sb.Append(',');
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value);
        }

        private static void FieldStr(StringBuilder sb, string key, string value)
        {
            sb.Append(',');
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            AppendString(sb, value ?? string.Empty);
        }

        private static void FieldStrNull(StringBuilder sb, string key, string value)
        {
            sb.Append(',');
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            if (value == null)
            {
                sb.Append("null");
            }
            else
            {
                AppendString(sb, value);
            }
        }

        private static string Null(int? v)
        {
            return v.HasValue ? v.Value.ToString() : "null";
        }

        public static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            object v;
            if (!dict.TryGetValue(key, out v) || v == null)
            {
                return string.Empty;
            }
            return v.ToString();
        }

        private static string GetStringNull(Dictionary<string, object> dict, string key)
        {
            object v;
            if (!dict.TryGetValue(key, out v) || v == null)
            {
                return null;
            }
            return v.ToString();
        }

        private static int GetInt(Dictionary<string, object> dict, string key)
        {
            object v;
            if (!dict.TryGetValue(key, out v) || v == null)
            {
                return 0;
            }
            if (v is int)
            {
                return (int)v;
            }
            if (v is long)
            {
                return (int)(long)v;
            }
            int n;
            if (int.TryParse(v.ToString(), out n))
            {
                return n;
            }
            return 0;
        }

        private static int? GetNullableInt(Dictionary<string, object> dict, string key)
        {
            object v;
            if (!dict.TryGetValue(key, out v) || v == null)
            {
                return null;
            }
            if (v is int)
            {
                return (int)v;
            }
            if (v is long)
            {
                return (int)(long)v;
            }
            int n;
            if (int.TryParse(v.ToString(), out n))
            {
                return n;
            }
            return null;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key)
        {
            object v;
            if (!dict.TryGetValue(key, out v) || v == null)
            {
                return false;
            }
            if (v is bool)
            {
                return (bool)v;
            }
            return string.Equals(v.ToString(), "true", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
