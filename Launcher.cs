// Windows-side launcher for the Nordic WSL apps. Exists so the icon lives
// inside the .exe: Start, search and pins read it from there reliably.
// Usage: <exe> <linux script path> <AUMID> <profile name>
// Runs the script in Ubuntu via wslg, then stamps the shortcut's AUMID on the
// Chrome app window so the taskbar groups it under the pin; Chrome otherwise
// gives the window its own ID and a second taskbar button. The window is found
// by its Chrome profile, profiles\<name> beside this exe, which the script
// must pass as --user-data-dir.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

static class Launcher
{
    static int Main(string[] args)
    {
        if (args.Length != 3) return 2;
        var profile = "--user-data-dir=" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles", args[2]);
        var before = new HashSet<IntPtr>(ChromeWindows());

        var psi = new ProcessStartInfo(@"C:\Program Files\WSL\wslg.exe",
            "-d Ubuntu --cd \"~\" -- " + args[0]);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        Process.Start(psi);

        // A second click opens another window in the running profile, so wait
        // for a window that wasn't there before rather than any match.
        var ours = new Dictionary<uint, bool>();
        for (var deadline = DateTime.Now.AddSeconds(30); DateTime.Now < deadline; Thread.Sleep(250))
        {
            var found = false;
            foreach (var hwnd in ChromeWindows())
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                bool match;
                if (!ours.TryGetValue(pid, out match))
                    ours[pid] = match = HasArg(CommandLine(pid), profile);
                if (!match) continue;
                SetAppId(hwnd, args[1]);
                found |= !before.Contains(hwnd);
            }
            if (found) return 0;
        }
        return 1;
    }

    static List<IntPtr> ChromeWindows()
    {
        var list = new List<IntPtr>();
        var cls = new StringBuilder(64);
        EnumWindows((hwnd, _) =>
        {
            cls.Length = 0;
            if (IsWindowVisible(hwnd) && GetClassName(hwnd, cls, cls.Capacity) > 0
                && cls.ToString() == "Chrome_WidgetWin_1")
                list.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    static string CommandLine(uint pid)
    {
        using (var q = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + pid))
            foreach (var o in q.Get())
                return (o["CommandLine"] as string) ?? "";
        return "";
    }

    static bool HasArg(string commandLine, string arg)
    {
        foreach (var token in commandLine.Split(' '))
            if (string.Equals(token.Trim('"'), arg, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static void SetAppId(IntPtr hwnd, string id)
    {
        var iid = typeof(IPropertyStore).GUID;
        IPropertyStore store;
        if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0) return;
        var key = new PKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
        var v = new PV { vt = 31, p = Marshal.StringToCoTaskMemUni(id) };
        store.SetValue(ref key, ref v);
        Marshal.FreeCoTaskMem(v.p);
        Marshal.ReleaseComObject(store);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)] struct PKEY { public Guid fmtid; public uint pid; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] struct PV { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr p; }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out uint c); void GetAt(uint i, out PKEY k); void GetValue(ref PKEY k, out PV v);
        void SetValue(ref PKEY k, ref PV v); void Commit();
    }

    delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
}
