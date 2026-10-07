using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CourtDesk.Platform
{
    public static class InputLanguageAssist
    {
        public const string ArabicCulture = "ar-EG";

        public static void ApplyTo(Window window)
        {
            if (window == null)
            {
                return;
            }
            try
            {
                window.PreviewTextInput += OnPreviewTextInput;
            }
            catch (Exception)
            {
            }
        }

        public static string NormalizeDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= '\u0660' && c <= '\u0669')
                {
                    sb.Append((char)('0' + (c - '\u0660')));
                }
                else if (c >= '\u06F0' && c <= '\u06F9')
                {
                    sb.Append((char)('0' + (c - '\u06F0')));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static bool HasArabicDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            foreach (char c in s)
            {
                if ((c >= '\u0660' && c <= '\u0669') || (c >= '\u06F0' && c <= '\u06F9'))
                {
                    return true;
                }
            }
            return false;
        }

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var box = sender as TextBox;
            if (box == null || !HasArabicDigits(e.Text))
            {
                return;
            }
            string norm = NormalizeDigits(e.Text);
            int start = box.SelectionStart;
            string text = box.Text;
            box.Text = text.Substring(0, start) + norm + text.Substring(
                Math.Min(text.Length, start + e.Text.Length));
            box.SelectionStart = start + norm.Length;
            e.Handled = true;
        }
    }
}