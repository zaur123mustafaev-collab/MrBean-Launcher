using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MrBeanLauncher.Ui;

internal static class Native
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private class MEMORYSTATUSEX
	{
		public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;
	}

	[ComImport]
	[Guid("00021401-0000-0000-C000-000000000046")]
	private class ShellLink
	{
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("000214F9-0000-0000-C000-000000000046")]
	private interface IShellLinkW
	{
		void GetPath([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder f, int cch, IntPtr pfd, int fFlags);

		void GetIDList(out IntPtr ppidl);

		void SetIDList(IntPtr pidl);

		void GetDescription([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder n, int cch);

		void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string n);

		void GetWorkingDirectory([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder d, int cch);

		void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string d);

		void GetArguments([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder a, int cch);

		void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string a);

		void GetHotkey(out short h);

		void SetHotkey(short h);

		void GetShowCmd(out int c);

		void SetShowCmd(int c);

		void GetIconLocation([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder p, int cch, out int i);

		void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string p, int i);

		void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string p, int r);

		void Resolve(IntPtr hwnd, int flags);

		void SetPath([MarshalAs(UnmanagedType.LPWStr)] string p);
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("0000010b-0000-0000-C000-000000000046")]
	private interface IPersistFile
	{
		void GetClassID(out Guid id);

		[PreserveSig]
		int IsDirty();

		void Load([MarshalAs(UnmanagedType.LPWStr)] string f, int mode);

		void Save([MarshalAs(UnmanagedType.LPWStr)] string f, bool remember);

		void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string f);

		void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string f);
	}

	[DllImport("user32.dll")]
	public static extern bool SetProcessDPIAware();

	[DllImport("user32.dll")]
	public static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	public static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
	private static extern IntPtr SendMessageStr(IntPtr h, int msg, IntPtr w, string l);

	public static void SetCue(IntPtr edit, string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			SendMessageStr(edit, 5377, (IntPtr)1, text);
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	private static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX m);

	public static int TotalRamMb()
	{
		try
		{
			MEMORYSTATUSEX mEMORYSTATUSEX = new MEMORYSTATUSEX();
			if (GlobalMemoryStatusEx(mEMORYSTATUSEX))
			{
				return (int)(mEMORYSTATUSEX.ullTotalPhys / 1048576);
			}
		}
		catch
		{
		}
		return 8192;
	}

	public static void DragWindow(IntPtr h)
	{
		ReleaseCapture();
		SendMessage(h, 161, 2, 0);
	}

	public static void RoundCorners(IntPtr h)
	{
		try
		{
			int val = 2;
			DwmSetWindowAttribute(h, 33, ref val, 4);
		}
		catch
		{
		}
		try
		{
			int val2 = 1;
			DwmSetWindowAttribute(h, 20, ref val2, 4);
		}
		catch
		{
		}
	}

	public static void CreateShortcut(string lnk, string target, string workDir, string desc)
	{
		IShellLinkW shellLinkW = (IShellLinkW)new ShellLink();
		shellLinkW.SetPath(target);
		shellLinkW.SetWorkingDirectory(workDir);
		shellLinkW.SetDescription(desc);
		shellLinkW.SetIconLocation(target, 0);
		((IPersistFile)shellLinkW).Save(lnk, remember: true);
	}

	public static void EnsureDesktopShortcut(string exe)
	{
		try
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string lnk = Path.Combine(folderPath, "MrBean Launcher.lnk");
			string text = Path.Combine(Path.GetDirectoryName(exe), "MrBeanLauncher.exe");
			if (File.Exists(text))
			{
				exe = text;
			}
			CreateShortcut(lnk, exe, Path.GetDirectoryName(exe), "MrBean Launcher — Minecraft 1.21.11 + MrBean Visuals");
			string path = Path.Combine(folderPath, "MrBean Launcher.bat");
			if (File.Exists(path) && File.ReadAllText(path).Contains("MrBeanLauncher"))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}
}
