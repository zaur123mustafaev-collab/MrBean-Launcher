using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    public class ConsolePage : Page
    {
        public enum Kind { Game, Launcher, Error }
        readonly RichTextBox box;
        readonly ConcurrentQueue<Tuple<string, Kind>> queue = new ConcurrentQueue<Tuple<string, Kind>>();
        readonly Timer flush;
        readonly string logFile;

        public ConsolePage(MainForm f) : base(f)
        {
            logFile = Path.Combine(f.Root, "launcher.log");
            var card = Add(new XCard(), 36, 84, 964, 488);
            box = new RichTextBox
            {
                BorderStyle = BorderStyle.None, BackColor = Card, ForeColor = Theme.Fg, ReadOnly = true,
                Font = Mono(12.5f), DetectUrls = false, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both,
                Bounds = new Rectangle(S(16), S(14), S(932), S(460))
            };
            card.Controls.Add(box);

            var copy = Add(new XButton { Text = "Копировать", Icon = IcCopy, FontPx = 12.5f }, 520, 18, 130, 40);
            copy.Click += (s, e) => { try { if (box.TextLength > 0) Clipboard.SetText(box.Text); } catch { } };
            var clear = Add(new XButton { Text = "Очистить", Icon = IcDelete, FontPx = 12.5f }, 660, 18, 110, 40);
            clear.Click += (s, e) => box.Clear();
            var latest = Add(new XButton { Text = "latest.log", Icon = IcDoc, FontPx = 12.5f }, 780, 18, 110, 40);
            latest.Click += (s, e) =>
            {
                string p = Path.Combine(F.Inst.GameDir, "logs", "latest.log");
                try { if (File.Exists(p)) System.Diagnostics.Process.Start("notepad.exe", "\"" + p + "\""); } catch { }
            };
            var stop = Add(new XButton { Style = BtnStyle.Danger, Text = "Стоп", Icon = IcStop, FontPx = 12.5f }, 900, 18, 100, 40);
            stop.Click += (s, e) => { if (F.GameRunning && MessageBox.Show(this, "Закрыть игру принудительно?", "MrBean Launcher", MessageBoxButtons.YesNo) == DialogResult.Yes) F.KillGame(); };

            Append("[Лаунчер] MrBean Launcher 2.1.1 готов. Здесь будет лог игры в реальном времени.", Kind.Launcher);
            flush = new Timer { Interval = 100 };
            flush.Tick += (s, e) => Flush();
            flush.Start();
        }

        public void Append(string line, Kind k)
        {
            queue.Enqueue(Tuple.Create(line, k));
            if (k != Kind.Game) try { File.AppendAllText(logFile, DateTime.Now.ToString("[HH:mm:ss] ") + line + Environment.NewLine); } catch { }
        }

        void Flush()
        {
            if (queue.IsEmpty || box.IsDisposed) return;
            int n = 0;
            box.SuspendLayout();
            Tuple<string, Kind> it;
            while (n < 400 && queue.TryDequeue(out it))
            {
                Color c = Theme.Fg;
                string l = it.Item1;
                if (it.Item2 == Kind.Launcher) c = Green;
                else if (it.Item2 == Kind.Error || l.Contains("/ERROR]") || l.Contains("Exception")) c = Red;
                else if (l.Contains("/WARN]")) c = Yellow;
                else if (l.Contains("MrBean")) c = Lime;
                else c = Color.FromArgb(190, 196, 212);
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;
                box.SelectionColor = c;
                box.AppendText(l + "\n");
                n++;
            }
            if (box.TextLength > 2000000) { box.Select(0, box.TextLength - 1500000); box.SelectedText = ""; }
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
            box.ResumeLayout();
        }

        protected override void Paint2(Graphics g) { Header(g, "Консоль", "Лог игры и лаунчера в реальном времени"); }
    }

    public class AboutPage : Page
    {
        readonly Image hero; Bitmap cache;
        public AboutPage(MainForm f, Image hero) : base(f)
        {
            this.hero = hero;
            var site = Add(new XButton { Text = "Папка лаунчера", Icon = IcFolder, FontPx = 13 }, 336, 512, 200, 44);
            site.Click += (s, e) => MainForm.OpenFolder(f.Root);
        }

        protected override void Paint2(Graphics g)
        {
            Header(g, "О моде", "MrBean Visuals 1.0 для Minecraft 1.21.11 (Fabric)");
            var hr = new RectangleF(S(36), S(88), S(270), S(270));
            if (hero != null)
            {
                if (cache == null)
                {
                    cache = new Bitmap((int)hr.Width, (int)hr.Height);
                    using (var hg = Graphics.FromImage(cache)) using (var p = Round(new RectangleF(0, 0, hr.Width, hr.Height), S(20)))
                    { Hq(hg); hg.SetClip(p); hg.DrawImage(hero, 0, 0, hr.Width, hr.Height); }
                }
                g.DrawImageUnscaled(cache, (int)hr.X, (int)hr.Y);
            }
            string[,] keys = {
                { "Правый Shift", "открыть меню мода (ЛКМ — вкл/выкл, ПКМ — настройки)" },
                { "C", "зум, колёсико — сила приближения" },
                { "HUD редактор", "кнопка в меню — двигай элементы мышкой" },
                { "Макросы", "Настройки → Управление → MrBean Visuals" },
            };
            float y = S(92);
            using (var fb = Bold(13)) using (var fu = Reg(13))
                for (int i = 0; i < keys.GetLength(0); i++)
                {
                    var kr = new RectangleF(S(336), y, S(130), S(30));
                    FillRound(g, CardHover, kr, S(8)); StrokeRound(g, Border, kr, S(8));
                    Theme.Text(g, keys[i, 0], fb, Lime, kr, StringAlignment.Center);
                    Theme.Text(g, keys[i, 1], fu, Theme.Fg, new RectangleF(S(480), y, S(520), S(30)));
                    y += S(42);
                }
            using (var f = Reg(13))
                TextWrap(g,
                    "Что внутри: ватермарка, список модулей, клавиши с CPS, броня, эффекты, координаты, таргет-HUD, " +
                    "счётчик тотемов и перлов, комбо, дистанция удара, кулдаун, предупреждения о HP и броне, свой прицел, " +
                    "хитмаркер, авто-спринт, уровень света, день мира, точка смерти, сытость, зум, яркость, без тряски, " +
                    "без покачивания, меньше огня, без дождя, время суток, скрытие скорборда, стабильный FOV, макросы и 8 тем оформления.\n\n" +
                    "Ник в лаунчере — офлайн: одиночная игра и серверы без лицензии. Читов нет — только визуал и удобство.",
                    f, Muted, new RectangleF(S(336), S(270), S(660), S(230)));
        }
    }
}
