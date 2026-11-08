// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using System.Runtime.CompilerServices;

namespace XianPal.Linux;

internal static class LinuxLanguageBindings
{
    private static readonly ConditionalWeakTable<Control, object> bound = new();
    internal static void Preserve(Control control) => bound.GetValue(control, _ => new object());

    internal static void Caption(Control element)
    {
        if (bound.TryGetValue(element, out _)) return;
        bound.Add(element, new object());
        string source = Read(element), rendered = null;
        bool applying = false;
        void Apply()
        {
            if (applying) return;
            applying = true;
            try { rendered = UiLanguage.Text(source); Write(element, rendered); }
            finally { applying = false; }
        }
        element.PropertyChanged += (_, e) =>
        {
            if (applying) return;
            if (e.Property == TextBlock.TextProperty || e.Property == ContentControl.ContentProperty || e.Property == HeaderedSelectingItemsControl.HeaderProperty || e.Property == HeaderedContentControl.HeaderProperty)
            {
                string current = Read(element);
                if (current != rendered) { source = current; Apply(); }
            }
        };
        bool subscribed = false;
        void Attach() { if (!subscribed) { UiLanguage.Changed += Apply; subscribed = true; } Apply(); }
        element.AttachedToVisualTree += (_, _) => Attach();
        element.DetachedFromVisualTree += (_, _) => { if (subscribed) UiLanguage.Changed -= Apply; subscribed = false; };
        if (TopLevel.GetTopLevel(element) != null) Attach();
        Apply();
    }

    internal static void Choices(ComboBox combo)
    {
        if (bound.TryGetValue(combo, out _)) return;
        object[] original = combo.ItemsSource?.Cast<object>().ToArray() ?? Array.Empty<object>();
        if (original.Length == 0 || original.Any(item => item is not string) || !original.Cast<string>().Any(UiLanguage.IsCaption)) return;
        bound.Add(combo, new object());
        string[] source = combo.ItemsSource?.Cast<object>().OfType<string>().ToArray() ?? Array.Empty<string>();
        bool applying = false;
        void Apply()
        {
            applying = true;
            try
            {
                int selected = combo.SelectedIndex;
                combo.ItemsSource = source.Select(UiLanguage.Text).ToArray();
                combo.SelectedIndex = selected;
            }
            finally { applying = false; }
        }
        combo.PropertyChanged += (_, e) =>
        {
            if (!applying && e.Property == ItemsControl.ItemsSourceProperty)
            { source = combo.ItemsSource?.Cast<object>().OfType<string>().ToArray() ?? Array.Empty<string>(); Apply(); }
        };
        bool subscribed = false;
        void Attach() { if (!subscribed) { UiLanguage.Changed += Apply; subscribed = true; } Apply(); }
        combo.AttachedToVisualTree += (_, _) => Attach();
        combo.DetachedFromVisualTree += (_, _) => { if (subscribed) UiLanguage.Changed -= Apply; subscribed = false; };
        if (TopLevel.GetTopLevel(combo) != null) Attach();
    }

    internal static void Window(Window window)
    {
        string title = window.Title;
        void ApplyTitle() => window.Title = UiLanguage.Text(title);
        window.Opened += (_, _) =>
        {
            ApplyTitle();
            foreach (Control element in window.GetVisualDescendants().OfType<Control>().Concat(window.GetLogicalDescendants().OfType<Control>()).Distinct().ToArray())
            {
                if (element is ComboBox choices) Choices(choices);
                else if (element is TextBlock or Button or CheckBox or TabItem) Caption(element);
                else if (element is TextBox input && input.Watermark != null) Watermark(input);
            }
            UiLanguage.Changed += ApplyTitle;
        };
        window.Closed += (_, _) => UiLanguage.Changed -= ApplyTitle;
    }

    private static string Read(Control element)
    {
        if (element is TextBlock text) return text.Text;
        if (element is MenuItem menu) return menu.Header as string;
        if (element is TabItem tab) return tab.Header as string;
        return (element as ContentControl)?.Content as string;
    }

    private static void Write(Control element, string value)
    {
        if (element is TextBlock text) text.Text = value;
        else if (element is MenuItem menu) menu.Header = value;
        else if (element is TabItem tab) tab.Header = value;
        else if (element is ContentControl content) content.Content = value;
    }

    private static void Watermark(TextBox input)
    {
        if (bound.TryGetValue(input, out _)) return;
        bound.Add(input, new object());
        string source = input.Watermark;
        void Apply() => input.Watermark = UiLanguage.Text(source);
        bool subscribed = false;
        void Attach() { if (!subscribed) { UiLanguage.Changed += Apply; subscribed = true; } Apply(); }
        input.AttachedToVisualTree += (_, _) => Attach();
        input.DetachedFromVisualTree += (_, _) => { if (subscribed) UiLanguage.Changed -= Apply; subscribed = false; };
        if (TopLevel.GetTopLevel(input) != null) Attach();
        Apply();
    }
}
