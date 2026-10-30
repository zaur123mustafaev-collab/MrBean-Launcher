using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    public class SettingsPage : Page
    {
        public SettingsPage(MainForm f) : base(f)
        {
            var c = f.Cfg;

            // ---------------- Память
            var mem = Add(new XCard { Title = "Оперативная память", Icon = IcMemory, Hint = "В ПК " + Math.Round(f.TotalRam / 1024.0) + " ГБ  •  для игры рекомендуем 4–6 ГБ" }, 36, 84, 470, 128);
            int maxGb = Math.Max(2, Math.Min(32, (f.TotalRam - 1536) / 1024));
            var ram = new XSlider { Min = 1, Max = maxGb, Value = Math.Max(1, Math.Min(maxGb, c.RamMb / 1024)), Format = v => v + " ГБ" };
            ram.Bounds = new Rectangle(S(12), S(62), S(446), S(44));
            ram.Changed += (s, e) => { c.RamMb = ram.Value * 1024; f.SaveCfg(); f.Home.UpdateRamHint(); };
            mem.Controls.Add(ram);

            // ---------------- Окно игры
            var win = Add(new XCard { Title = "Окно игры", Icon = IcMonitor, Hint = "Размер окна при запуске" }, 36, 228, 470, 132);
            var wI = new XInput { Placeholder = "1280", Bounds = new Rectangle(S(18), S(64), S(110), S(44)) }; wI.Text = c.Width.ToString();
            var hI = new XInput { Placeholder = "720", Bounds = new Rectangle(S(160), S(64), S(110), S(44)) }; hI.Text = c.Height.ToString();
            win.Controls.Add(wI); win.Controls.Add(hI);
            win.Controls.Add(new XLabel { Text = "×", Px = 18, Color = Muted, Align = StringAlignment.Center, Bounds = new Rectangle(S(128), S(64), S(32), S(44)) });
            EventHandler saveWH = (s, e) =>
            {
                int w, h;
                if (int.TryParse(wI.Text, out w) && w >= 640 && w <= 7680) c.Width = w;
                if (int.TryParse(hI.Text, out h) && h >= 480 && h <= 4320) c.Height = h;
                f.SaveCfg();
            };
            wI.Box.TextChanged += saveWH; hI.Box.TextChanged += saveWH;
            var fs = new XToggle { Checked = c.Fullscreen, Location = new Point(S(408), S(74)) };
            fs.Changed += (s, e) => { c.Fullscreen = fs.Checked; f.SaveCfg(); };
            win.Controls.Add(fs);
            win.Controls.Add(new XLabel { Text = "Полный экран", Px = 13, Color = Theme.Fg, Align = StringAlignment.Far, Bounds = new Rectangle(S(286), S(64), S(114), S(44)) });

            // ---------------- Java
            var java = Add(new XCard { Title = "Java и производительность", Icon = IcCode, Hint = "Сборщик мусора и свои аргументы JVM" }, 36, 376, 470, 196);
            var gc = new XSegmented { Items = new[] { "G1 (реком.)", "ZGC", "Parallel" }, Bounds = new Rectangle(S(18), S(62), S(434), S(40)) };
            gc.Selected = c.GcPreset.StartsWith("ZGC") ? 1 : c.GcPreset.StartsWith("Parallel") ? 2 : 0;
            gc.Changed += (s, e) => { c.GcPreset = new[] { "G1 (рекомендуется)", "ZGC", "Parallel" }[gc.Selected]; f.SaveCfg(); };
            java.Controls.Add(gc);
            var args = new XInput { Icon = IcCode, Placeholder = "Доп. аргументы, напр. -XX:+UseStringDeduplication", Bounds = new Rectangle(S(18), S(116), S(434), S(44)) };
            args.Text = c.JavaArgs; args.Box.Font = Mono(13);
            args.Box.TextChanged += (s, e) => { c.JavaArgs = args.Text; f.SaveCfg(); };
            java.Controls.Add(args);
            java.Controls.Add(new XLabel { Text = "Java 21 скачивается автоматически (официальная от Mojang)", Px = 11.5f, Color = Dim, Bounds = new Rectangle(S(20), S(166), S(430), S(18)) });

            // ---------------- Лаунчер
            var l = Add(new XCard { Title = "Лаунчер", Icon = IcSettings, Hint = "Поведение и загрузка" }, 530, 84, 470, 268);
            AddToggleRow(l, 56, "Сворачивать в трей во время игры", c.HideOnLaunch, v => c.HideOnLaunch = v);
            AddToggleRow(l, 94, "Открывать консоль при запуске", c.ShowConsoleOnLaunch, v => c.ShowConsoleOnLaunch = v);
            AddToggleRow(l, 132, "Русский язык при первом запуске", c.RussianLang, v => c.RussianLang = v);
            AddToggleRow(l, 170, "Моды оптимизации (Sodium, Lithium…) с Modrinth", c.OptMods, v => { c.OptMods = v; if (!v) { int n = new MrBeanLauncher.Core.OptMods(f.Inst.ModsDir, f.Inst.CacheDir).RemoveOurs(); f.Console.Append("[Лаунчер] Моды оптимизации выключены, убрано файлов: " + n, ConsolePage.Kind.Launcher); } });
            l.Controls.Add(new XLabel { Text = "Потоков загрузки", Px = 13, Color = Theme.Fg, Bounds = new Rectangle(S(20), S(216), S(150), S(40)) });
            var par = new XSlider { Min = 4, Max = 48, Value = c.Parallel, Format = v => v.ToString(), Bounds = new Rectangle(S(170), S(210), S(286), S(44)) };
            par.Changed += (s, e) => { c.Parallel = par.Value; f.SaveCfg(); };
            l.Controls.Add(par);

            // ---------------- Файлы
            var files = Add(new XCard { Title = "Файлы и папки", Icon = IcFolder, Hint = "Всё лежит в " + f.Root }, 530, 366, 470, 206);
            string g = f.Inst.GameDir;
            AddBtn(files, 0, 0, "Папка игры", IcFolder, () => MainForm.OpenFolder(g));
            AddBtn(files, 1, 0, "Моды", IcMods, () => MainForm.OpenFolder(f.Inst.ModsDir));
            AddBtn(files, 2, 0, "Скриншоты", IcCamera, () => MainForm.OpenFolder(Path.Combine(g, "screenshots")));
            AddBtn(files, 0, 1, "Ресурспаки", IcPicture, () => MainForm.OpenFolder(Path.Combine(g, "resourcepacks")));
            AddBtn(files, 1, 1, "Миры", IcGlobe, () => MainForm.OpenFolder(Path.Combine(g, "saves")));
            AddBtn(files, 2, 1, "Логи", IcDoc, () => MainForm.OpenFolder(Path.Combine(g, "logs")));
            var verify = new XButton { Text = "Проверить файлы", Icon = IcRefresh, FontPx = 12.5f, Bounds = new Rectangle(S(18), S(152), S(210), S(38)) };
            verify.Click += async (s, e) => { f.ShowPage(0); await f.RunInstall(false); };
            files.Controls.Add(verify);
            var java2 = new XButton { Text = "Переустановить Java", Icon = IcRefresh, FontPx = 12.5f, Bounds = new Rectangle(S(240), S(152), S(212), S(38)) };
            java2.Click += async (s, e) => { f.ShowPage(0); await f.RunInstall(false, true); };
            files.Controls.Add(java2);
        }

        void AddToggleRow(XCard card, int y, string text, bool value, Action<bool> set)
        {
            card.Controls.Add(new XLabel { Text = text, Px = 13, Color = Theme.Fg, Bounds = new Rectangle(S(20), S(y), S(380), S(36)) });
            var t = new XToggle { Checked = value, Location = new Point(S(408), S(y + 6)) };
            t.Changed += (s, e) => { set(t.Checked); F.SaveCfg(); };
            card.Controls.Add(t);
        }

        void AddBtn(XCard card, int col, int row, string text, string icon, Action a)
        {
            var b = new XButton { Text = text, Icon = icon, FontPx = 12.5f, Bounds = new Rectangle(S(18 + col * 146), S(56 + row * 46), S(138), S(38)) };
            b.Click += (s, e) => a();
            card.Controls.Add(b);
        }

        protected override void Paint2(Graphics g) { Header(g, "Настройки", "Всё сохраняется автоматически"); }
    }
}
