using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MrBeanLauncher.Ui;

namespace MrBeanLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "MrBeanLauncher_SingleInstance", out created))
            {
                if (!created) { MessageBox.Show("MrBean Launcher уже открыт.", "MrBean Launcher"); return; }
                try { Native.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string root = exeDir;
                // exe можно положить куда угодно (например, на рабочий стол) — игра всё равно живёт на D:\MrBeanLauncher
                bool exeHasData = File.Exists(Path.Combine(exeDir, "launcher.json")) || Directory.Exists(Path.Combine(exeDir, "game"));
                if (!exeHasData && Directory.Exists(@"D:\")) root = @"D:\MrBeanLauncher";
                try { Directory.CreateDirectory(root); } catch { root = exeDir; }
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MRBEAN_ROOT"))) root = Environment.GetEnvironmentVariable("MRBEAN_ROOT");
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(root, e.ExceptionObject as Exception);
                Application.ThreadException += (s, e) => Crash(root, e.Exception);

                try
                {
                    using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) Theme.Scale = Math.Max(1f, g.DpiX / 96f);
                    // окно не должно вылезать за экран на ноутбуках
                    var wa = Screen.PrimaryScreen.WorkingArea;
                    float fit = Math.Min(wa.Width / (float)MainForm.W, wa.Height / (float)MainForm.H) * 0.96f;
                    if (Theme.Scale > fit) Theme.Scale = Math.Max(0.75f, fit);
                }
                catch { }
                Theme.InitFonts();
                string shot = Environment.GetEnvironmentVariable("MRBEAN_PAGE");
                if (string.IsNullOrEmpty(shot)) Native.EnsureDesktopShortcut(Application.ExecutablePath);
                var form = new MainForm(root);
                if (!string.IsNullOrEmpty(shot)) form.Shown += (s, e) => { form.ShowPage(int.Parse(shot)); if (Environment.GetEnvironmentVariable("MRBEAN_AUTOPLAY") == "1") form.PlayClicked(); };
                Application.Run(form);
            }
        }

        static void Crash(string root, Exception ex)
        {
            try { File.AppendAllText(Path.Combine(root, "crash-launcher.txt"), DateTime.Now + "\n" + ex + "\n\n"); } catch { }
            MessageBox.Show("Ошибка лаунчера:\n" + (ex == null ? "?" : ex.Message) + "\n\nПодробности: crash-launcher.txt", "MrBean Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
