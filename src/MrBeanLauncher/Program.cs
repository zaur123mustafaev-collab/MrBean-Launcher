using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MrBeanLauncher.Ui;

namespace MrBeanLauncher;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "MrBeanLauncher_SingleInstance", out createdNew))
		{
			if (!createdNew)
			{
				MessageBox.Show("MrBean Launcher уже открыт.", "MrBean Launcher");
				return;
			}
			try
			{
				Native.SetProcessDPIAware();
			}
			catch
			{
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MRBEAN_ROOT")))
			{
				root = Environment.GetEnvironmentVariable("MRBEAN_ROOT");
			}
			AppDomain.CurrentDomain.UnhandledException += (object s, UnhandledExceptionEventArgs e) =>
			{
				Crash(root, e.ExceptionObject as Exception);
			};
			Application.ThreadException += (object s, ThreadExceptionEventArgs e) =>
			{
				Crash(root, e.Exception);
			};
			try
			{
				using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
				{
					Theme.Scale = Math.Max(1f, graphics.DpiX / 96f);
				}
				Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
				float num = Math.Min((float)workingArea.Width / 1120f, (float)workingArea.Height / 680f) * 0.96f;
				if (Theme.Scale > num)
				{
					Theme.Scale = Math.Max(0.75f, num);
				}
			}
			catch
			{
			}
			Theme.InitFonts();
			string shot = Environment.GetEnvironmentVariable("MRBEAN_PAGE");
			if (string.IsNullOrEmpty(shot))
			{
				Native.EnsureDesktopShortcut(Application.ExecutablePath);
			}
			MainForm form = new MainForm(root);
			if (!string.IsNullOrEmpty(shot))
			{
				form.Shown += (object s, EventArgs e) =>
				{
					form.ShowPage(int.Parse(shot));
					if (Environment.GetEnvironmentVariable("MRBEAN_AUTOPLAY") == "1")
					{
						form.PlayClicked();
					}
				};
			}
			Application.Run(form);
		}
	}

	private static void Crash(string root, Exception ex)
	{
		try
		{
			File.AppendAllText(Path.Combine(root, "crash-launcher.txt"), DateTime.Now.ToString() + "\n" + ex?.ToString() + "\n\n");
		}
		catch
		{
		}
		MessageBox.Show("Ошибка лаунчера:\n" + ((ex == null) ? "?" : ex.Message) + "\n\nПодробности: crash-launcher.txt", "MrBean Launcher", MessageBoxButtons.OK, MessageBoxIcon.Hand);
	}
}
