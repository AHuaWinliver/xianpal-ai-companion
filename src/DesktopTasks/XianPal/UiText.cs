using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XianPal
{
    internal static class UiText
    {
        private static readonly Regex AnsiEscape = new Regex(@"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*(?:\x07|\x1B\\))", RegexOptions.Compiled);

        internal static string Normalize(string value)
        {
            if (String.IsNullOrEmpty(value)) return value ?? "";
            string clean = value.Replace("\uFEFF", "").Replace("\0", "");
            clean = AnsiEscape.Replace(clean, "");
            try { clean = clean.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { }
            return clean;
        }

        internal static Font Font(float size)
        {
            return Font(size, FontStyle.Regular);
        }

        internal static Font Font(float size, FontStyle style)
        {
            return CreateFont(size, style, new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "NSimSun", "Arial Unicode MS", "Segoe UI" });
        }

        internal static Font ContentFont(float size)
        {
            return ContentFont(size, FontStyle.Regular);
        }

        internal static Font ContentFont(float size, FontStyle style)
        {
            return Font(size, style);
        }

        private static Font CreateFont(float size, FontStyle style, string[] families)
        {
            foreach (string family in families)
            {
                Font candidate = null;
                try
                {
                    candidate = new Font(family, size, style, GraphicsUnit.Point);
                    if (String.Equals(candidate.FontFamily.Name, family, StringComparison.OrdinalIgnoreCase)) return candidate;
                }
                catch { }
                if (candidate != null) candidate.Dispose();
            }
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }

        internal static string TailTextElements(string value, int maximumElements)
        {
            string clean = Normalize(value);
            if (maximumElements <= 0 || clean.Length == 0) return "";
            int[] starts = StringInfo.ParseCombiningCharacters(clean);
            if (starts.Length <= maximumElements) return clean;
            return "…" + clean.Substring(starts[starts.Length - maximumElements]);
        }

        internal static string TailCodeUnits(string value, int maximumLength)
        {
            string clean = Normalize(value);
            if (clean.Length <= maximumLength) return clean;
            int start = clean.Length - Math.Max(1, maximumLength);
            if (start < clean.Length && Char.IsLowSurrogate(clean[start]) && start > 0 && Char.IsHighSurrogate(clean[start - 1])) start--;
            while (start < clean.Length && CharUnicodeInfo.GetUnicodeCategory(clean, start) == UnicodeCategory.NonSpacingMark) start++;
            return clean.Substring(start);
        }

        internal static bool HasUnpairedSurrogate(string value)
        {
            for (int i = 0; i < (value ?? "").Length; i++)
            {
                if (Char.IsHighSurrogate(value[i]))
                {
                    if (i + 1 >= value.Length || !Char.IsLowSurrogate(value[i + 1])) return true;
                    i++;
                }
                else if (Char.IsLowSurrogate(value[i])) return true;
            }
            return false;
        }

        internal static bool IsSymbolTextElement(string element)
        {
            if (String.IsNullOrEmpty(element)) return false;
            for (int i = 0; i < element.Length; i++)
            {
                if (Char.IsSurrogate(element[i])) return true;
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(element, i);
                if (category == UnicodeCategory.OtherSymbol || category == UnicodeCategory.MathSymbol || category == UnicodeCategory.ModifierSymbol) return true;
                if (element[i] == '\uFE0F' || element[i] == '\u200D') return true;
            }
            return false;
        }

        internal static void ApplyMixedGlyphFonts(RichTextBox box)
        {
            if (box == null || box.TextLength == 0) return;
            int selectionStart = box.SelectionStart;
            box.SelectAll();
            box.SelectionFont = box.Font;
            string text = box.Text;
            int[] starts = StringInfo.ParseCombiningCharacters(text);
            using (Font symbols = CreateFont(box.Font.Size, box.Font.Style, new[] { "Segoe UI Emoji", "Segoe UI Symbol", "Arial Unicode MS", "Segoe UI" }))
            {
                for (int i = 0; i < starts.Length; i++)
                {
                    int end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
                    string element = text.Substring(starts[i], end - starts[i]);
                    if (!IsSymbolTextElement(element)) continue;
                    box.Select(starts[i], end - starts[i]);
                    box.SelectionFont = symbols;
                }
            }
            box.Select(Math.Min(selectionStart, box.TextLength), 0);
        }
    }
}
