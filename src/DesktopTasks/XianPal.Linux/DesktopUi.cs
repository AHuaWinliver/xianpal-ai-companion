// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace XianPal.Linux;

internal static class DesktopUi
{
    internal static Button Action(string label, Action perform)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => perform();
        return button;
    }

    internal static TextBlock Label(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap
    };

    internal static StackPanel Stack(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (Control control in children) panel.Children.Add(control);
        return panel;
    }

    internal static StackPanel Row(params Control[] children)
    {
        StackPanel row = Stack(children);
        row.Orientation = Orientation.Horizontal;
        return row;
    }

    internal static Window Dialog(string title, Control body, double width = 580, double height = 480)
    {
        return new Window
        {
            Title = title, Width = width, Height = height, MinWidth = 330, MinHeight = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border { Padding = new Thickness(16), Child = body }
        };
    }

    internal static void Notice(Window owner, string title, string message)
    {
        Window window = Dialog(title, Label(message), 440, 190);
        _ = window.ShowDialog(owner);
    }

    internal static async Task<string> Folder(Window window, string title)
    {
        var selected = await window.StorageProvider.OpenFolderPickerAsync(new() { Title = title });
        return selected.Count == 1 ? selected[0].TryGetLocalPath() : null;
    }

    internal static async Task<string> JsonFile(Window window, string title)
    {
        var selected = await window.StorageProvider.OpenFilePickerAsync(new()
        {
            Title = title, FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
        });
        return selected.Count == 1 ? selected[0].TryGetLocalPath() : null;
    }

    internal static void OpenFolder(string directory)
    {
        Directory.CreateDirectory(directory);
        using var launcher = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open")
        {
            UseShellExecute = false, ArgumentList = { directory }
        });
    }
}
