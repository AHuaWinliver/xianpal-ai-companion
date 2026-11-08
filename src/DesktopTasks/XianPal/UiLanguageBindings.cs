// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace XianPal
{
    // Translate UI captions only. Editable fields, transcripts and task payloads
    // retain their original contents when the interface language changes.
    internal static class UiLanguageBindings
    {
        private sealed class Caption { public Caption() { } internal string Source, Rendered; }
        private static readonly ConditionalWeakTable<object, Caption> captions = new ConditionalWeakTable<object, Caption>();
        private static readonly ConditionalWeakTable<ComboBox, List<Caption>> choices = new ConditionalWeakTable<ComboBox, List<Caption>>();
        private static readonly ConditionalWeakTable<object, object> protectedContent = new ConditionalWeakTable<object, object>();
        private static DateTime nextRefresh;
        private static bool initialized;

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Application.Idle += delegate
            {
                if (DateTime.UtcNow < nextRefresh) return;
                nextRefresh = DateTime.UtcNow.AddMilliseconds(250);
                RefreshAll();
            };
            UiLanguage.Changed += RefreshAll;
        }

        internal static void PreserveContent(object control)
        { if (control != null) protectedContent.GetValue(control, delegate { return new object(); }); }

        private static string Translate(object key, string current)
        {
            Caption state = captions.GetOrCreateValue(key);
            if (state.Source == null || current != state.Rendered) state.Source = current;
            state.Rendered = UiLanguage.Text(state.Source);
            return state.Rendered;
        }

        internal static void Apply(Control control)
        {
            object marker;
            if (protectedContent.TryGetValue(control, out marker)) return;
            if (!(control is TextBoxBase) && !(control is ListBox) && !(control is ComboBox) && !(control is WebBrowser))
            {
                string text = Translate(control, control.Text);
                if (control.Text != text) control.Text = text;
            }
            ComboBox combo = control as ComboBox;
            if (combo != null && combo.DataSource == null && combo.DropDownStyle == ComboBoxStyle.DropDownList)
            {
                List<Caption> source = choices.GetOrCreateValue(combo);
                if (source.Count != combo.Items.Count)
                {
                    source.Clear();
                    foreach (object item in combo.Items) source.Add(new Caption { Source = item as string, Rendered = item as string });
                }
                int selected = combo.SelectedIndex;
                for (int i = 0; i < source.Count; i++)
                    if (source[i].Source != null)
                    {
                        string current = combo.Items[i] as string;
                        if (current != source[i].Rendered) source[i].Source = current;
                        string translated = UiLanguage.Text(source[i].Source);
                        source[i].Rendered = translated;
                        if (!String.Equals(combo.Items[i] as string, translated, StringComparison.Ordinal)) combo.Items[i] = translated;
                    }
                if (combo.SelectedIndex != selected) combo.SelectedIndex = selected;
            }
            ToolStrip strip = control as ToolStrip;
            if (strip != null) ApplyItems(strip.Items);
            ListView list = control as ListView;
            if (list != null) foreach (ColumnHeader column in list.Columns) column.Text = Translate(column, column.Text);
            if (control.ContextMenuStrip != null) ApplyItems(control.ContextMenuStrip.Items);
            foreach (Control child in control.Controls) Apply(child);
        }

        internal static void ApplyItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                string text = Translate(item, item.Text);
                if (item.Text != text) item.Text = text;
                ToolStripDropDownItem parent = item as ToolStripDropDownItem;
                if (parent != null && parent.HasDropDownItems) ApplyItems(parent.DropDownItems);
            }
        }

        private static void RefreshAll()
        {
            var windows = new List<Form>();
            foreach (Form window in Application.OpenForms) windows.Add(window);
            foreach (Form window in windows) if (!window.IsDisposed) Apply(window);
        }
    }
}
