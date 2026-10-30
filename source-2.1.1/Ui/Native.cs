using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MrBeanLauncher.Ui
{
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] static extern IntPtr SendMessageStr(IntPtr h, int msg, IntPtr w, string l);
        public static void SetCue(IntPtr edit, string text) { if (!string.IsNullOrEmpty(text)) SendMessageStr(edit, 0x1501, (IntPtr)1, text); }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX m);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        public static int TotalRamMb()
        {
            try { var m = new MEMORYSTATUSEX(); if (GlobalMemoryStatusEx(m)) return (int)(m.ullTotalPhys / 1048576UL); } catch { }
            return 8192;
        }

        public static void DragWindow(IntPtr h) { ReleaseCapture(); SendMessage(h, 0xA1, 0x2, 0); }

        public static void RoundCorners(IntPtr h)
        {
            try { int pref = 2; DwmSetWindowAttribute(h, 33, ref pref, 4); } catch { }
            try { int dark = 1; DwmSetWindowAttribute(h, 20, ref dark, 4); } catch { }
        }

        // ---------------- ярлык (IShellLink)
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder f, int cch, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder n, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string n);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder d, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string d);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder a, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string a);
            void GetHotkey(out short h);
            void SetHotkey(short h);
            void GetShowCmd(out int c);
            void SetShowCmd(int c);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder p, int cch, out int i);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string p, int i);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string p, int r);
            void Resolve(IntPtr hwnd, int flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string p);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
        interface IPersistFile
        {
            void GetClassID(out Guid id);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string f, int mode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string f, bool remember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string f);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string f);
        }

        public static void CreateShortcut(string lnk, string target, string workDir, string desc)
        {
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(target);
            link.SetWorkingDirectory(workDir);
            link.SetDescription(desc);
            link.SetIconLocation(target, 0);
            ((IPersistFile)link).Save(lnk, true);
        }

        /// <summary>Ярлык на рабочем столе + удаление старого .bat.</summary>
        public static void EnsureDesktopShortcut(string exe)
        {
            try
            {
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desk, "MrBean Launcher.lnk");
                // если сам exe лежит на рабочем столе — ярлык рядом не нужен
                if (string.Equals(Path.GetDirectoryName(exe).TrimEnd('\\'), desk.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                // ярлык всегда ведёт на основной MrBeanLauncher.exe, даже если запущена копия
                string main = Path.Combine(Path.GetDirectoryName(exe), "MrBeanLauncher.exe");
                if (File.Exists(main)) exe = main;
                CreateShortcut(lnk, exe, Path.GetDirectoryName(exe), "MrBean Launcher — Minecraft 1.21.11 + MrBean Visuals");
                string bat = Path.Combine(desk, "MrBean Launcher.bat");
                if (File.Exists(bat) && File.ReadAllText(bat).Contains("MrBeanLauncher")) File.Delete(bat);
            }
            catch { }
        }
    }
}
