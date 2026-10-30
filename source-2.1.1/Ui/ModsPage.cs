using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    public class ModsPage : Page
    {
        readonly ModList list;
        readonly XLabel count;

        public ModsPage(MainForm f) : base(f)
        {
            var add = Add(new XButton { Style = BtnStyle.Primary, Text = "Добавить мод", Icon = IcAdd, FontPx = 13, Radius = 10 }, 610, 14, 170, 42);
            add.Click += (s, e) => AddMods();
            var folder = Add(new XButton { Text = "Папка", Icon = IcFolder, FontPx = 13 }, 790, 14, 100, 42);
            folder.Click += (s, e) => MainForm.OpenFolder(F.Inst.ModsDir);
            var web = Add(new XButton { Text = "Modrinth", Icon = IcGlobe, FontPx = 13 }, 900, 14, 100, 42);
            web.Click += (s, e) => { try { Process.Start("https://modrinth.com/mods?g=categories:fabric&v=1.21.11"); } catch { } };

            list = Add(new ModList(this), 36, 92, 964, 470);
            count = Add(new XLabel { Px = 12, Color = Muted }, 38, 570, 900, 20);

            AllowDrop = true; list.AllowDrop = true;
            DragEnter += OnDragEnter; list.DragEnter += OnDragEnter;
            DragDrop += OnDrop; list.DragDrop += OnDrop;
        }

        void OnDragEnter(object s, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }
        void OnDrop(object s, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) Import(files);
        }

        void AddMods()
        {
            using (var d = new OpenFileDialog { Filter = "Моды Fabric (*.jar)|*.jar", Multiselect = true, Title = "Выбери моды для 1.21.11 (Fabric)" })
                if (d.ShowDialog(this) == DialogResult.OK) Import(d.FileNames);
        }

        void Import(IEnumerable<string> files)
        {
            Directory.CreateDirectory(F.Inst.ModsDir);
            int n = 0;
            foreach (var f in files)
            {
                if (!f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Copy(f, Path.Combine(F.Inst.ModsDir, Path.GetFileName(f)), true); n++; } catch { }
            }
            Reload();
            F.Toast(n > 0 ? "Добавлено модов: " + n : "Нужны файлы .jar");
        }

        public void Reload() { list.Load(F.Inst.ModsDir); count.Text = list.Summary(); }

        protected override void Paint2(Graphics g) { Header(g, "Моды", "Перетащи .jar сюда или нажми «Добавить». Только Fabric 1.21.11."); }

        // -------------------------------------------------------------- список
        class ModList : XControl
        {
            class Mod { public string Path, Name; public long Size; public bool On; public bool Builtin; }
            readonly ModsPage page;
            List<Mod> mods = new List<Mod>();
            float scroll, scrollT; int hoverRow = -1; int hoverPart; // 1 = переключатель, 2 = удалить
            const int ROW = 66;

            public ModList(ModsPage p) { page = p; Cursor = Cursors.Default; }

            public void Load(string dir)
            {
                mods.Clear();
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir))
                    {
                        bool on = f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
                        bool off = f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
                        if (!on && !off) continue;
                        string name = System.IO.Path.GetFileName(f);
                        name = name.Substring(0, name.Length - (on ? 4 : 13));
                        mods.Add(new Mod { Path = f, Name = name, Size = new FileInfo(f).Length, On = on, Builtin = name.StartsWith("MrBeanVisuals") || name.StartsWith("fabric-api") });
                    }
                mods = mods.OrderByDescending(m => m.Builtin).ThenBy(m => m.Name).ToList();
                Invalidate();
            }

            public string Summary()
            {
                return mods.Count == 0 ? "Модов пока нет — они появятся после первого запуска"
                    : string.Format("Всего: {0}  •  включено: {1}  •  {2:0.0} МБ", mods.Count, mods.Count(m => m.On), mods.Sum(m => m.Size) / 1048576.0);
            }

            float MaxScroll { get { return Math.Max(0, mods.Count * S(ROW) - Height); } }
            protected override void OnMouseWheel(MouseEventArgs e) { scrollT = Math.Max(0, Math.Min(MaxScroll, scrollT - e.Delta * 0.6f)); }
            public override void Step()
            {
                base.Step();
                float n = scroll + (scrollT - scroll) * 0.25f; if (Math.Abs(n - scrollT) < 0.5f) n = scrollT;
                if (n != scroll) { scroll = n; Invalidate(); }
            }

            void Hit(int x, int y, out int row, out int part)
            {
                row = (int)((y + scroll) / S(ROW)); part = 0;
                if (row < 0 || row >= mods.Count) { row = -1; return; }
                if (x > Width - S(130) && x < Width - S(70)) part = 1;
                else if (x >= Width - S(62) && x < Width - S(14)) part = 2;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int r, p; Hit(e.X, e.Y, out r, out p);
                if (r != hoverRow || p != hoverPart) { hoverRow = r; hoverPart = p; Cursor = p > 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverRow = -1; Invalidate(); }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int r, p; Hit(e.X, e.Y, out r, out p);
                if (r < 0) return;
                var m = mods[r];
                try
                {
                    if (p == 1)
                    {
                        string target = m.On ? m.Path + ".disabled" : m.Path.Substring(0, m.Path.Length - ".disabled".Length);
                        File.Move(m.Path, target);
                        if (m.Name.StartsWith("MrBeanVisuals")) { page.F.Cfg.WithVisuals = !m.On; page.F.SaveCfg(); page.F.Home.Profile.Selected = m.On ? 1 : 0; page.F.Home.Profile.Invalidate(); }
                        page.Reload();
                    }
                    else if (p == 2)
                    {
                        if (MessageBox.Show(page, "Удалить мод «" + m.Name + "»?", "MrBean Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        { File.Delete(m.Path); page.Reload(); }
                    }
                }
                catch (Exception ex) { MessageBox.Show(page, "Не получилось: " + ex.Message + "\nЗакрой игру и попробуй снова.", "MrBean Launcher"); }
            }

            protected override void Draw(Graphics g)
            {
                var full = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                FillRound(g, A(Card, 225), full, Sf(16));
                StrokeRound(g, A(Border, 200), full, Sf(16));
                if (mods.Count == 0)
                {
                    using (var fi = Theme.Icon(40)) Theme.Text(g, IcMods, fi, Dim, new RectangleF(0, Height / 2f - Sf(60), Width, Sf(50)), StringAlignment.Center);
                    using (var f = Bold(16)) Theme.Text(g, "Здесь появятся твои моды", f, Muted, new RectangleF(0, Height / 2f, Width, Sf(24)), StringAlignment.Center);
                    using (var f = Reg(13)) Theme.Text(g, "Нажми «Играть» один раз — лаунчер поставит Fabric API и MrBean Visuals", f, Dim, new RectangleF(0, Height / 2f + Sf(26), Width, Sf(20)), StringAlignment.Center);
                    return;
                }
                var st = g.Save();
                using (var clip = Round(full, Sf(16))) g.SetClip(clip);
                for (int i = 0; i < mods.Count; i++)
                {
                    float y = i * S(ROW) - scroll;
                    if (y + S(ROW) < 0 || y > Height) continue;
                    var m = mods[i];
                    var rr = new RectangleF(Sf(8), y + Sf(6), Width - Sf(16), S(ROW) - Sf(8));
                    if (i == hoverRow) FillRound(g, A(Color.White, 10), rr, Sf(12));
                    // иконка
                    var ic = new RectangleF(rr.X + Sf(10), rr.Y + (rr.Height - Sf(40)) / 2, Sf(40), Sf(40));
                    if (m.Name.StartsWith("MrBeanVisuals"))
                        using (var b = Accent(ic, 45)) FillRound(g, b, ic, Sf(10));
                    else
                        FillRound(g, Color.FromArgb(255, 36 + (m.Name.GetHashCode() & 31), 40, 56), ic, Sf(10));
                    using (var f = Black(18)) Theme.Text(g, m.Name.Substring(0, 1).ToUpper(), f, m.Name.StartsWith("MrBeanVisuals") ? Color.FromArgb(14, 18, 10) : Theme.Fg, ic, StringAlignment.Center);
                    // текст
                    using (var f = Bold(15)) Theme.Text(g, m.Name, f, m.On ? Theme.Fg : Dim, new RectangleF(ic.Right + Sf(14), rr.Y + Sf(10), rr.Width - Sf(280), Sf(22)));
                    string sub = (m.Size / 1024.0 < 1024 ? (m.Size / 1024.0).ToString("0") + " КБ" : (m.Size / 1048576.0).ToString("0.0") + " МБ") + (m.On ? "" : "  •  выключен");
                    using (var f = Reg(12)) Theme.Text(g, sub, f, Muted, new RectangleF(ic.Right + Sf(14), rr.Y + Sf(32), Sf(300), Sf(18)));
                    if (m.Builtin)
                    {
                        string badge = m.Name.StartsWith("MrBean") ? "ВСТРОЕН" : "НУЖЕН";
                        using (var f = Bold(10))
                        {
                            var sz = g.MeasureString(badge, f);
                            var br = new RectangleF(rr.Right - Sf(140) - sz.Width - Sf(14), rr.Y + (rr.Height - Sf(22)) / 2, sz.Width + Sf(14), Sf(22));
                            FillRound(g, A(Green, 30), br, Sf(11));
                            Theme.Text(g, badge, f, Lime, br, StringAlignment.Center);
                        }
                    }
                    // переключатель
                    var tr = new RectangleF(rr.Right - Sf(116), rr.Y + (rr.Height - Sf(24)) / 2, Sf(44), Sf(24));
                    FillRound(g, m.On ? Green : Color.FromArgb(46, 50, 66), tr, Sf(12));
                    if (m.On) using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(tr, Green, Yellow, 0f)) FillRound(g, b, tr, Sf(12));
                    float kd = Sf(18);
                    using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, m.On ? tr.Right - kd - Sf(3) : tr.X + Sf(3), tr.Y + Sf(3), kd, kd);
                    // удалить
                    var dr = new RectangleF(rr.Right - Sf(52), rr.Y + (rr.Height - Sf(36)) / 2, Sf(36), Sf(36));
                    bool dh = i == hoverRow && hoverPart == 2;
                    if (dh) FillRound(g, A(Red, 40), dr, Sf(9));
                    using (var fi = Theme.Icon(15)) Theme.Text(g, IcDelete, fi, dh ? Red : Muted, dr, StringAlignment.Center);
                }
                g.Restore(st);
                if (MaxScroll > 0)
                {
                    float h = Height * Height / (float)(mods.Count * S(ROW));
                    float y = (Height - h) * scroll / MaxScroll;
                    FillRound(g, A(Color.White, 40), new RectangleF(Width - Sf(6), y + Sf(4), Sf(3), h - Sf(8)), Sf(2));
                }
            }
        }
    }
}
