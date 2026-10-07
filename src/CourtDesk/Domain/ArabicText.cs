using System.Text;

namespace CourtDesk.Domain
{
    public static class ArabicText
    {
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            string t = s.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(t.Length);
            bool prevSpace = true;
            foreach (char c in t)
            {
                if (IsRemovable(c))
                {
                    continue;
                }
                char m = Map(c);
                if (m == ' ')
                {
                    if (prevSpace)
                    {
                        continue;
                    }
                    prevSpace = true;
                    sb.Append(' ');
                    continue;
                }
                prevSpace = false;
                sb.Append(m);
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
            {
                sb.Length -= 1;
            }
            return sb.ToString();
        }

        public static string Joined(string s)
        {
            string n = Normalize(s);
            if (n.Length == 0)
            {
                return n;
            }
            var sb = new StringBuilder(n.Length);
            foreach (char c in n)
            {
                if (c != ' ')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool IsRemovable(char c)
        {
            if (c >= '\u064B' && c <= '\u065F')
            {
                return true;
            }
            if (c == '\u0670' || c == '\u0640')
            {
                return true;
            }
            if (c >= '\u200B' && c <= '\u200F')
            {
                return true;
            }
            if (c >= '\u202A' && c <= '\u202E')
            {
                return true;
            }
            if (c >= '\u2066' && c <= '\u2069')
            {
                return true;
            }
            return c == '\uFEFF';
        }

        private static char Map(char c)
        {
            switch (c)
            {
                case '\u0623':
                case '\u0625':
                case '\u0622':
                case '\u0671':
                    return '\u0627';
                case '\u0649':
                    return '\u064A';
                case '\u0624':
                    return '\u0648';
                case '\u0626':
                    return '\u064A';
                case '\u0629':
                    return '\u0647';
            }
            if (c >= '\u0660' && c <= '\u0669')
            {
                return (char)('0' + (c - '\u0660'));
            }
            if (c >= '\u06F0' && c <= '\u06F9')
            {
                return (char)('0' + (c - '\u06F0'));
            }
            if (c >= 'A' && c <= 'Z')
            {
                return (char)(c + 32);
            }
            if (c == '\t' || c == '\n' || c == '\r')
            {
                return ' ';
            }
            return c;
        }
    }
}
