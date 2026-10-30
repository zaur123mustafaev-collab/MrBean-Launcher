using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MrBeanLauncher.Core;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    public class HomePage : Page
    {
        readonly Image hero;
        public readonly XInput Nick;
        readonly AccountChip chip;
        readonly XLabel nickL;
        public readonly XSegmented Profile;
        readonly XButton play;
        readonly XLabel status, speedL;
        readonly ProgressLine bar;
        float glow;
        Bitmap heroCache;
        readonly System.Windows.Forms.Timer pulse;

        public HomePage(MainForm f, Image hero) : base(f)
        {
            this.hero = hero;

            // нижняя панель запуска
            var dock = Add(new XCard { Radius = 20 }, 36, 476, 964, 140);
            nickL = new XLabel { Text = "НИК В ИГРЕ", Px = 11, IsBold = true, Color = Muted, Bounds = new Rectangle(S(22), S(16), S(200), S(16)) };
            dock.Controls.Add(nickL);
            Nick = new XInput { Icon = IcPerson, Placeholder = "Например, MrBean", Bounds = new Rectangle(S(20), S(36), S(250), S(46)) };
            Nick.Text = f.Cfg.Nick; Nick.Box.MaxLength = 16;
            dock.Controls.Add(Nick);
            chip = new AccountChip(f) { Bounds = new Rectangle(S(20), S(36), S(250), S(46)), Visible = false };
            chip.Click += (s, e) => f.ShowPage(MainForm.PageAccounts);
            dock.Controls.Add(chip);

            var profL = new XLabel { Text = "СБОРКА", Px = 11, IsBold = true, Color = Muted, Bounds = new Rectangle(S(292), S(16), S(200), S(16)) };
            dock.Controls.Add(profL);
            Profile = new XSegmented { Items = new[] { "MrBean Visuals", "Чистый Fabric" }, Selected = f.Cfg.WithVisuals ? 0 : 1, Bounds = new Rectangle(S(290), S(36), S(320), S(46)) };
            Profile.Changed += (s, e) => { f.Cfg.WithVisuals = Profile.Selected == 0; f.SaveCfg(); };
            dock.Controls.Add(Profile);

            play = new XButton { Style = BtnStyle.Primary, Text = "ИГРАТЬ", Sub = "1.21.11 • Fabric", FontPx = 24, Radius = 16, Bounds = new Rectangle(S(636), S(18), S(306), S(70)) };
            play.Click += (s, e) => f.PlayClicked();
            dock.Controls.Add(play);

            bar = new ProgressLine { Bounds = new Rectangle(S(22), S(104), S(600), S(6)) };
            dock.Controls.Add(bar);
            status = new XLabel { Text = "Готов к запуску", Px = 13, Color = Muted, Bounds = new Rectangle(S(20), S(112), S(640), S(22)) };
            dock.Controls.Add(status);
            speedL = new XLabel { Text = "", Px = 13, IsBold = true, Color = Green, Align = StringAlignment.Far, Bounds = new Rectangle(S(636), S(106), S(306), S(24)) };
            dock.Controls.Add(speedL);
            UpdateRamHint();

            // плитки возможностей
            AddTile(36, 236, IcSword, "PvP HUD", "Таргет-HUD, тотемы, комбо, кулдаун");
            AddTile(282, 236, IcLeaf, "PvE помощники", "Свет, день мира, точка смерти");
            AddTile(36, 326, IcEye, "Визуалы", "Зум, яркость, прицел, без тряски");
            AddTile(282, 326, IcKeyboard, "Макросы", "6 клавиш с командами");

            pulse = new System.Windows.Forms.Timer { Interval = 30 };
            pulse.Tick += (s, e) => { glow += 0.04f; Invalidate(new Rectangle(S(560), S(0), S(480), S(470))); };
            pulse.Start();
        }

        void AddTile(int x, int y, string icon, string title, string text)
        {
            var c = Add(new Tile { Icon = icon, Title = title, Desc = text }, x, y, 232, 78);
        }

        public void UpdateRamHint()
        {
            speedL.Text = "";
            speedL.Color = Muted;
            speedL.Text = "ОЗУ: " + (F.Cfg.RamMb / 1024) + " ГБ";
        }

        protected override void Paint2(Graphics g)
        {
            // чип
            var chip = new RectangleF(S(36), S(8), S(330), S(28));
            FillRound(g, A(Green, 28), chip, S(14));
            StrokeRound(g, A(Green, 90), chip, S(14));
            using (var b = new SolidBrush(Green)) g.FillEllipse(b, chip.X + S(12), chip.Y + S(10), S(8), S(8));
            using (var fch = Bold(12)) Theme.Text(g, "Minecraft " + Installer.MC + "  •  Fabric  •  Java 21", fch, Lime, new RectangleF(chip.X + S(26), chip.Y, chip.Width, chip.Height));

            // заголовок
            using (var f1 = Black(56))
            {
                Theme.Text(g, "MrBean", f1, Color.White, new RectangleF(S(30), S(40), S(520), S(70)));
                var r = new RectangleF(S(30), S(100), S(520), S(70));
                using (var path = new GraphicsPath())
                using (var sf = new StringFormat())
                {
                    path.AddString("Visuals", f1.FontFamily, (int)f1.Style, f1.Size, new PointF(r.X, r.Y + S(2)), sf);
                    using (var gb = new LinearGradientBrush(new RectangleF(r.X, r.Y, S(280), S(70)), Green, Yellow, 0f)) g.FillPath(gb, path);
                }
            }
            using (var fs = Reg(15))
                TextWrap(g, "Красивые визуалы, HUD для PvP и PvE и макросы.\nОдна кнопка — и Minecraft со всеми модами готов.", fs, Muted, new RectangleF(S(36), S(174), S(500), S(50)));

            // клавиши
            float kx = S(36), ky = S(420);
            kx = Key(g, "Правый Shift", kx, ky); kx = Hint(g, "меню мода", kx, ky);
            kx = Key(g, "C", kx + S(14), ky); kx = Hint(g, "зум", kx, ky);

            // картинка
            if (hero != null)
            {
                var hr = new RectangleF(S(560), S(4), S(440), S(440));
                float gl = (float)(Math.Sin(glow) * 0.5 + 0.5);
                for (int i = 5; i >= 1; i--)
                {
                    var rr = hr; rr.Inflate(S(i * 3), S(i * 3));
                    FillRound(g, A(Lerp(Green, Yellow, gl), (int)((12 + 10 * gl) / i * 2)), rr, S(22 + i * 3));
                }
                if (heroCache == null)
                {
                    heroCache = new Bitmap((int)hr.Width, (int)hr.Height);
                    using (var hg = Graphics.FromImage(heroCache))
                    using (var path0 = Round(new RectangleF(0, 0, hr.Width, hr.Height), S(22)))
                    {
                        Hq(hg); hg.SetClip(path0); hg.DrawImage(hero, 0, 0, hr.Width, hr.Height);
                    }
                }
                g.DrawImageUnscaled(heroCache, (int)hr.X, (int)hr.Y);
                using (var path = Round(hr, S(22)))
                using (var p = new Pen(A(Lime, 160), S(2))) g.DrawPath(p, path);
            }
        }

        float Key(Graphics g, string k, float x, float y)
        {
            using (var f = Bold(12))
            {
                var sz = g.MeasureString(k, f);
                var r = new RectangleF(x, y, sz.Width + S(16), S(28));
                FillRound(g, CardHover, r, S(7));
                FillRound(g, Card, new RectangleF(r.X, r.Y, r.Width, r.Height - S(3)), S(7));
                StrokeRound(g, Border, r, S(7));
                Theme.Text(g, k, f, Theme.Fg, new RectangleF(r.X, r.Y, r.Width, r.Height - S(3)), StringAlignment.Center);
                return r.Right + S(8);
            }
        }
        float Hint(Graphics g, string t, float x, float y)
        {
            using (var f = Reg(13)) { var sz = g.MeasureString(t, f); Theme.Text(g, t, f, Muted, new RectangleF(x, y, sz.Width + 4, S(28))); return x + sz.Width; }
        }

        /// <summary>Показать аккаунт Microsoft или поле офлайн-ника.</summary>
        public void RefreshAccount()
        {
            bool ms = F.Accounts.Current != null;
            chip.Visible = ms; Nick.Visible = !ms;
            nickL.Text = ms ? "АККАУНТ" : "НИК В ИГРЕ (ОФЛАЙН)";
            nickL.Invalidate(); chip.Invalidate();
        }

        public void SyncNick() { if (Nick.Text != F.Cfg.Nick) Nick.Text = F.Cfg.Nick; }

        class AccountChip : XControl
        {
            readonly MainForm f;
            public AccountChip(MainForm f) { this.f = f; }
            protected override void Draw(Graphics g)
            {
                var a = f.Accounts.Current;
                var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                FillRound(g, Lerp(Card, CardHover, HoverT), r, Sf(10));
                StrokeRound(g, Lerp(A(Green, 120), Green, HoverT), r, Sf(10));
                if (a == null) return;
                SkinFaces.DrawHead(g, SkinFaces.Get(f, a.Uuid, a.SkinUrl), new RectangleF(Sf(8), Sf(7), Height - Sf(14), Height - Sf(14)), a.Name);
                using (var fb = Bold(14)) Theme.Text(g, a.Name, fb, Fg, new RectangleF(Height + Sf(2), Sf(5), Width - Height - Sf(30), Sf(20)));
                using (var fu = Reg(11.5f)) Theme.Text(g, a.NeedsLogin ? "Нужен вход" : "Microsoft ✓ лицензия", fu, a.NeedsLogin ? Yellow : Green, new RectangleF(Height + Sf(2), Sf(24), Width, Sf(16)));
                using (var fi = Theme.Icon(12)) Theme.Text(g, "\uE76C", fi, Muted, new RectangleF(Width - Sf(26), 0, Sf(20), Height), StringAlignment.Center);
            }
        }

        public void SetStatus(string s, bool error) { status.Text = s; status.Color = error ? Red : Muted; status.Invalidate(); }

        public void SetBusy(bool b)
        {
            Nick.Enabled = !b; Profile.Enabled = !b; chip.Enabled = !b;
            play.Text = b ? "ОТМЕНА" : (F.GameRunning ? "В ИГРЕ" : "ИГРАТЬ");
            play.Sub = b ? "идёт загрузка..." : "1.21.11 • Fabric";
            if (!b) { play.Progress = -1; bar.Value = -1; UpdateRamHint(); }
            play.Invalidate();
        }

        public void SetRunning(bool r)
        {
            play.Text = r ? "В ИГРЕ" : "ИГРАТЬ";
            play.Sub = r ? "открыть консоль" : "1.21.11 • Fabric";
            play.Invalidate();
        }

        public void SetProgress(double p, string text, double bytesPerSec)
        {
            play.Progress = (float)p; play.Invalidate();
            bar.Value = (float)p; bar.Invalidate();
            if (!string.IsNullOrEmpty(text)) SetStatus(text, false);
            speedL.Color = Green;
            speedL.Text = (int)(p * 100) + "%" + (bytesPerSec > 1024 ? "  •  " + (bytesPerSec / 1048576.0).ToString("0.0") + " МБ/с" : "");
        }

        class Tile : XControl
        {
            public string Icon, Title, Desc;
            protected override void Draw(Graphics g)
            {
                var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                FillRound(g, A(Lerp(Card, CardHover, HoverT), 225), r, Sf(14));
                StrokeRound(g, Lerp(A(Border, 200), A(Green, 150), HoverT), r, Sf(14));
                var ir = new RectangleF(Sf(14), (Height - Sf(40)) / 2, Sf(40), Sf(40));
                using (var b = new LinearGradientBrush(ir, A(Green, 60 + (int)(40 * HoverT)), A(Yellow, 40), 45f)) FillRound(g, b, ir, Sf(10));
                using (var fi = Theme.Icon(18)) Theme.Text(g, Icon, fi, Lime, ir, StringAlignment.Center);
                using (var f = Bold(14)) Theme.Text(g, Title, f, Theme.Fg, new RectangleF(Sf(66), Sf(16), Width - Sf(70), Sf(22)));
                using (var f = Reg(12)) TextWrap(g, Desc, f, Muted, new RectangleF(Sf(66), Sf(38), Width - Sf(72), Sf(36)));
            }
        }

        class ProgressLine : Control
        {
            public float Value = -1; float shimmer;
            readonly System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 30 };
            public ProgressLine()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                t.Tick += (s, e) => { if (Value >= 0) { shimmer = (shimmer + 0.02f) % 1.4f; Invalidate(); } };
                t.Start();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; Hq(g);
                var r = new RectangleF(0, 0, Width, Height);
                FillRound(g, Color.FromArgb(36, 40, 54), r, Height / 2f);
                if (Value < 0) return;
                var fr = r; fr.Width = Math.Max(Height, Width * Math.Min(1, Value));
                using (var b = new LinearGradientBrush(r, Green, Yellow, 0f)) FillRound(g, b, fr, Height / 2f);
                float sx = Width * (shimmer - 0.2f);
                var sr = new RectangleF(sx, 0, Width * 0.2f, Height);
                if (sr.Right > 0 && sr.X < fr.Right)
                    using (var b = new LinearGradientBrush(new RectangleF(sr.X - 1, 0, sr.Width + 2, Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 255, 255), 0f))
                    {
                        b.SetBlendTriangularShape(0.5f);
                        var st = g.Save(); using (var p = Round(fr, Height / 2f)) { g.SetClip(p); g.FillRectangle(b, sr); } g.Restore(st);
                    }
            }
        }
    }
}
