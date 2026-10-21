using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

namespace XianPal
{
    // No desktop switching, creation or registry writes. Registry enumeration is
    // a compatibility detail; always verify the real window GUID after moving.
    internal static class VirtualDesktopService
    {
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

        internal static List<Guid> ParseDesktopIds(byte[] bytes)
        {
            List<Guid> result = new List<Guid>();
            if (bytes == null || bytes.Length == 0 || bytes.Length % 16 != 0) return result;
            for (int i = 0; i < bytes.Length; i += 16)
            {
                byte[] item = new byte[16];
                Array.Copy(bytes, i, item, 0, 16);
                Guid id = new Guid(item);
                if (id == Guid.Empty || result.Contains(id)) return new List<Guid>();
                result.Add(id);
            }
            return result;
        }

        internal static List<Guid> GetDesktopIds()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            {
                List<Guid> ids = ParseDesktopIds(key == null ? null : key.GetValue("VirtualDesktopIDs") as byte[]);
                if (ids.Count > 0) return ids;
            }
            // Windows 10 stores the ordering per interactive session.
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\" + Process.GetCurrentProcess().SessionId + @"\VirtualDesktops"))
                return ParseDesktopIds(key == null ? null : key.GetValue("VirtualDesktopIDs") as byte[]);
        }

        internal static Guid ResolveDesktop(int number)
        {
            if (number == 0) return Guid.Empty;
            List<Guid> ids = GetDesktopIds();
            if (number < 1 || number > ids.Count)
                throw new InvalidOperationException("未找到桌面 " + number + "。请先按 Win+Tab 创建该虚拟桌面，再运行任务；任务不会改在其他桌面执行。");
            return ids[number - 1];
        }

        internal static Guid GetWindowDesktop(IntPtr window)
        {
            object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
            try { return ((IDesktopManager)instance).GetWindowDesktopId(window); }
            finally { Marshal.ReleaseComObject(instance); }
        }

        internal static bool IsWindowOnCurrentDesktop(IntPtr window)
        {
            object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
            try { return ((IDesktopManager)instance).IsWindowOnCurrentVirtualDesktop(window); }
            finally { Release(instance); }
        }

        internal static Guid GetCurrentDesktop()
        {
            object shell = null, controller = null, desktop = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239")));
                IShellServices services = (IShellServices)shell;
                Guid service = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
                Guid modern = typeof(IModernDesktopController).GUID;
                try
                {
                    controller = services.QueryService(ref service, ref modern);
                    desktop = ((IModernDesktopController)controller).GetCurrentDesktop();
                    return ((IModernDesktop)desktop).GetId();
                }
                catch (COMException)
                {
                    Release(desktop); desktop = null; Release(controller); controller = null;
                    Guid legacy = typeof(ILegacyDesktopController).GUID;
                    controller = services.QueryService(ref service, ref legacy);
                    desktop = ((ILegacyDesktopController)controller).GetCurrentDesktop();
                    return ((ILegacyDesktop)desktop).GetId();
                }
            }
            finally { Release(desktop); Release(controller); Release(shell); }
        }

        // Explorer's per-view pin interface is not a public Windows contract.
        // Validate the result and allow callers to fall back to following the
        // current desktop. Never use PinAppID, which could pin task windows too.
        internal static bool TrySetWindowPinned(IntPtr window, bool pinned, out string error)
        {
            object shell = null, collection = null, pins = null, view = null;
            error = "";
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239")));
                IShellServices services = (IShellServices)shell;
                Guid collectionId = typeof(IViewCollection).GUID;
                collection = services.QueryService(ref collectionId, ref collectionId);
                ((IViewCollection)collection).GetViewForHwnd(window, out view);
                if (view == null) throw new InvalidOperationException("Explorer 尚未注册此窗口。");
                Guid service = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD"), iid = typeof(IPinnedViews).GUID;
                pins = services.QueryService(ref service, ref iid);
                IPinnedViews api = (IPinnedViews)pins;
                if (api.IsViewPinned(view) != pinned)
                {
                    if (pinned) api.PinView(view);
                    else api.UnpinView(view);
                }
                if (api.IsViewPinned(view) != pinned) throw new InvalidOperationException("Windows 未确认窗口的跨桌面固定状态。");
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
            finally { Release(view); Release(pins); Release(collection); Release(shell); }
        }

        internal static void MoveWindow(IntPtr window, Guid target)
        {
            if (window == IntPtr.Zero) throw new InvalidOperationException("无法取得 PowerShell 窗口。请使用独立控制台窗口。");
            object manager = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
            try
            {
                IDesktopManager api = (IDesktopManager)manager;
                if (api.GetWindowDesktopId(window) == target) return;
                try { api.MoveWindowToDesktop(window, ref target); }
                catch (COMException) { MoveConsoleView(window, target); }
                catch (UnauthorizedAccessException) { MoveConsoleView(window, target); }
                if (api.GetWindowDesktopId(window) != target)
                    throw new InvalidOperationException("Windows 未确认 PowerShell 窗口已进入指定桌面，已取消任务执行。");
            }
            finally { Marshal.ReleaseComObject(manager); }
        }

        // Console HWNDs are owned by conhost, not PowerShell. The public API may
        // deny that cross-process move. Only query known Shell interfaces; never
        // switch desktops or move an existing Windows Terminal shared window.
        private static void MoveConsoleView(IntPtr window, Guid target)
        {
            object shell = null, collection = null, controller = null, desktops = null, view = null, desktop = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239")));
                IShellServices services = (IShellServices)shell;
                Guid collectionId = typeof(IViewCollection).GUID;
                collection = services.QueryService(ref collectionId, ref collectionId);
                ((IViewCollection)collection).GetViewForHwnd(window, out view);
                Guid serviceId = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
                Guid modernId = typeof(IModernDesktopController).GUID;
                IObjectArray array;
                try
                {
                    controller = services.QueryService(ref serviceId, ref modernId);
                    ((IModernDesktopController)controller).GetDesktops(out array);
                }
                catch (COMException)
                {
                    Release(controller); controller = null;
                    Guid legacyId = typeof(ILegacyDesktopController).GUID;
                    controller = services.QueryService(ref serviceId, ref legacyId);
                    ((ILegacyDesktopController)controller).GetDesktops(out array);
                }
                desktops = array;
                uint count; array.GetCount(out count);
                Guid unknown = new Guid("00000000-0000-0000-C000-000000000046");
                for (uint i = 0; i < count; i++)
                {
                    array.GetAt(i, ref unknown, out desktop);
                    Guid id;
                    IModernDesktop modern = desktop as IModernDesktop;
                    if (modern != null) id = modern.GetId();
                    else id = ((ILegacyDesktop)desktop).GetId();
                    if (id == target)
                    {
                        IModernDesktopController modernController = controller as IModernDesktopController;
                        if (modernController != null) modernController.MoveViewToDesktop(view, desktop);
                        else ((ILegacyDesktopController)controller).MoveViewToDesktop(view, desktop);
                        return;
                    }
                    Release(desktop); desktop = null;
                }
                throw new InvalidOperationException("目标虚拟桌面已关闭，任务未执行。");
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("当前 Windows 不支持此控制台的虚拟桌面定位（0x" + ex.ErrorCode.ToString("X8") + "）。任务未执行，请改用当前桌面或后台运行。", ex);
            }
            finally { Release(desktop); Release(view); Release(desktops); Release(controller); Release(collection); Release(shell); }
        }

        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

        internal static void PlaceParentConsole(Guid target, uint powershellProcessId)
        {
            FreeConsole();
            if (!AttachConsole(powershellProcessId)) throw new InvalidOperationException("无法连接独立 PowerShell 控制台（Win32 " + Marshal.GetLastWin32Error() + "）。");
            try
            {
                IntPtr window = GetConsoleWindow();
                // Conhost needs a short time to register the newly created view.
                Exception last = null;
                for (int i = 0; i < 20; i++)
                {
                    try { MoveWindow(window, target); return; }
                    catch (Exception ex) { last = ex; Thread.Sleep(100); }
                }
                throw last;
            }
            finally { FreeConsole(); }
        }

        internal static IntPtr FindTaskTerminalWindow(string token)
        {
            // Only a fresh Terminal window carrying this run's unguessable
            // title token can be moved. Never target the user's existing tabs.
            if (String.IsNullOrEmpty(token) || !token.StartsWith("OrbitTask-", StringComparison.Ordinal)) return IntPtr.Zero;
            Guid id;
            if (!Guid.TryParseExact(token.Substring("OrbitTask-".Length), "N", out id)) return IntPtr.Zero;
            IntPtr match = IntPtr.Zero;
            EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                StringBuilder className = new StringBuilder(128), title = new StringBuilder(512);
                GetClassName(window, className, className.Capacity);
                GetWindowText(window, title, title.Capacity);
                if (className.ToString() == "CASCADIA_HOSTING_WINDOW_CLASS" && title.ToString().Contains(token)) match = window;
                return true;
            }, IntPtr.Zero);
            return match;
        }

        internal static void PlaceTaskTerminal(Guid target, string token)
        {
            Exception last = null;
            for (int i = 0; i < 100; i++)
            {
                IntPtr window = FindTaskTerminalWindow(token);
                if (window != IntPtr.Zero)
                {
                    try { MoveWindow(window, target); return; }
                    catch (Exception ex) { last = ex; }
                }
                Thread.Sleep(100);
            }
            throw new InvalidOperationException("无法定位本次任务独立创建的终端窗口，任务未执行。", last);
        }

        private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder title, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);

        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(uint processId);
        [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll")] private static extern bool FreeConsole();

        [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDesktopManager
        {
            [return: MarshalAs(UnmanagedType.Bool)] bool IsWindowOnCurrentVirtualDesktop(IntPtr window);
            Guid GetWindowDesktopId(IntPtr window);
            void MoveWindowToDesktop(IntPtr window, ref Guid target);
        }
        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellServices
        {
            [return: MarshalAs(UnmanagedType.IUnknown)] object QueryService(ref Guid service, ref Guid iid);
        }
        [ComImport, Guid("4CE81583-1E4C-4632-A621-07A53543148F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPinnedViews
        {
            [return: MarshalAs(UnmanagedType.Bool)] bool IsAppIdPinned([MarshalAs(UnmanagedType.LPWStr)] string appId);
            void PinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
            void UnpinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
            [return: MarshalAs(UnmanagedType.Bool)] bool IsViewPinned([MarshalAs(UnmanagedType.IUnknown)] object view);
            void PinView([MarshalAs(UnmanagedType.IUnknown)] object view);
            void UnpinView([MarshalAs(UnmanagedType.IUnknown)] object view);
        }
        [ComImport, Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IViewCollection
        {
            void GetViews(out IObjectArray array);
            void GetViewsByZOrder(out IObjectArray array);
            void GetViewsByAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] string id, out IObjectArray array);
            void GetViewForHwnd(IntPtr window, [MarshalAs(UnmanagedType.IUnknown)] out object view);
        }
        // Only the common first five vtable entries are declared/used.
        [ComImport, Guid("53F5CA0B-158F-4124-900C-057158060B27"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IModernDesktopController
        {
            int GetCount();
            void MoveViewToDesktop([MarshalAs(UnmanagedType.IUnknown)] object view, [MarshalAs(UnmanagedType.IUnknown)] object desktop);
            [return: MarshalAs(UnmanagedType.Bool)] bool CanViewMoveDesktops([MarshalAs(UnmanagedType.IUnknown)] object view);
            [return: MarshalAs(UnmanagedType.IUnknown)] object GetCurrentDesktop();
            void GetDesktops(out IObjectArray array);
        }
        [ComImport, Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ILegacyDesktopController
        {
            int GetCount();
            void MoveViewToDesktop([MarshalAs(UnmanagedType.IUnknown)] object view, [MarshalAs(UnmanagedType.IUnknown)] object desktop);
            [return: MarshalAs(UnmanagedType.Bool)] bool CanViewMoveDesktops([MarshalAs(UnmanagedType.IUnknown)] object view);
            [return: MarshalAs(UnmanagedType.IUnknown)] object GetCurrentDesktop();
            void GetDesktops(out IObjectArray array);
        }
        [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IObjectArray
        {
            void GetCount(out uint count);
            void GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object item);
        }
        [ComImport, Guid("3F07F4BE-B107-441A-AF0F-39D82529072C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IModernDesktop
        {
            [return: MarshalAs(UnmanagedType.Bool)] bool IsViewVisible([MarshalAs(UnmanagedType.IUnknown)] object view);
            Guid GetId();
        }
        [ComImport, Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ILegacyDesktop
        {
            [return: MarshalAs(UnmanagedType.Bool)] bool IsViewVisible([MarshalAs(UnmanagedType.IUnknown)] object view);
            Guid GetId();
        }
    }
}
