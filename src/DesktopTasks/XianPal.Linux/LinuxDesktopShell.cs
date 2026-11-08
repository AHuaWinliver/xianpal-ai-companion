// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace XianPal.Linux;

internal sealed class LinuxDesktopShell : IDisposable
{
    private readonly PetWindow pet;
    private readonly DispatcherTimer restoreTimer;
    private TrayIcon tray;
    internal static string RestoreRequest => Path.Combine(Program.StateDirectory, "restore-desktop.request");

    internal LinuxDesktopShell(PetWindow window)
    {
        pet = window;
        string icon = Path.Combine(AppContext.BaseDirectory, "xianpal.png");
        try
        {
            if (File.Exists(icon))
            {
                tray = new TrayIcon { ToolTipText = "XianPal", Icon = new WindowIcon(icon), IsVisible = true };
                pet.Icon = tray.Icon;
                tray.Clicked += (_, _) => pet.ShowCompanion();
                TrayIcon.SetIcons(Application.Current, new TrayIcons { tray });
                RefreshMenu();
                UiLanguage.Changed += RefreshMenu;
            }
        }
        catch (Exception error)
        {
            tray?.Dispose(); tray = null;
            Console.Error.WriteLine("Desktop tray unavailable: " + error.Message);
        }
        restoreTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
        {
            if (!File.Exists(RestoreRequest)) return;
            try { File.Delete(RestoreRequest); pet.ShowCompanion(); }
            catch (IOException error) { Console.Error.WriteLine(error.Message); }
        });
        restoreTimer.Start();
    }

    private void RefreshMenu()
    {
        if (tray == null) return;
        var menu = new NativeMenu();
        void Add(string caption, Action action)
        {
            var item = new NativeMenuItem(UiLanguage.Text(caption));
            item.Click += (_, _) => Dispatcher.UIThread.Post(action);
            menu.Items.Add(item);
        }
        Add("显示宠物", pet.ShowCompanion);
        Add("隐藏宠物", pet.HideCompanion);
        Add("默认设置", () => { pet.ShowCompanion(); pet.ShowSettings(); });
        Add("Claude Code 定时任务", () => { pet.ShowCompanion(); TaskWindow.Show(pet, pet.Services.Tasks); });
        menu.Items.Add(new NativeMenuItemSeparator());
        Add("退出仙伴", pet.Close);
        tray.Menu = menu;
    }

    public void Dispose()
    {
        restoreTimer.Stop();
        UiLanguage.Changed -= RefreshMenu;
        tray?.Dispose();
    }
}
