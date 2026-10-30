using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace MrBeanLauncher.Ui
{
    public static class Theme
    {
        public static float Scale = 1f;
        public static int S(float v) { return (int)Math.Round(v * Scale); }
        public static float Sf(float v) { return v * Scale; }

        public static readonly Color Bg = Color.FromArgb(12, 13, 18);
        public static readonly Color Side = Color.FromArgb(16, 17, 24);
        public static readonly Color Card = Color.FromArgb(22, 24, 33);
        public static readonly Color CardHover = Color.FromArgb(30, 33, 45);
        public static readonly Color Border = Color.FromArgb(40, 44, 58);
        public static readonly Color Fg = Color.FromArgb(240, 242, 248);
        public static readonly Color Muted = Color.FromArgb(138, 145, 165);
        public static readonly Color Dim = Color.FromArgb(90, 96, 116);
        public static readonly Color Green = Color.FromArgb(124, 230, 60);
        public static readonly Color Lime = Color.FromArgb(190, 255, 70);
        public static readonly Color Yellow = Color.FromArgb(246, 222, 70);
        public static readonly Color Red = Color.FromArgb(255, 86, 96);

        static string Family(params string[] names)
        {
            using (var fonts = new InstalledFontCollection())
                foreach (var n in names)
                    foreach (var f in fonts.Families) if (f.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) return n;
            return FontFamily.GenericSansSerif.Name;
        }

        static string ui, uiBold, uiBlack, icons, mono;
        public static void InitFonts()
        {
            ui = Family("Segoe UI Variable Text", "Segoe UI");
            uiBold = Family("Segoe UI Semibold", "Segoe UI");
            uiBlack = Family("Segoe UI Black", "Segoe UI");
            icons = Family("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");
            mono = Family("Cascadia Mono", "Consolas", "Courier New");
        }

        public static Font Reg(float px, FontStyle st = FontStyle.Regular) { return new Font(ui, Sf(px), st, GraphicsUnit.Pixel); }
        public static Font Bold(float px) { return new Font(uiBold, Sf(px), uiBold == ui ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
        public static Font Black(float px) { return new Font(uiBlack, Sf(px), uiBlack == ui ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
        public static Font Icon(float px) { return new Font(icons, Sf(px), FontStyle.Regular, GraphicsUnit.Pixel); }
        public static Font Mono(float px) { return new Font(mono, Sf(px), FontStyle.Regular, GraphicsUnit.Pixel); }

        // Иконки Segoe MDL2 / Fluent
        public const string IcHome = "", IcMods = "", IcSettings = "", IcConsole = "",
            IcInfo = "", IcFolder = "", IcPlay = "", IcAdd = "", IcDelete = "",
            IcClose = "", IcMin = "", IcGlobe = "", IcRefresh = "", IcCopy = "",
            IcMemory = "", IcMonitor = "", IcCode = "", IcSword = "", IcLeaf = "",
            IcEye = "", IcKeyboard = "", IcStop = "", IcCheck = "", IcWarn = "",
            IcPerson = "", IcCamera = "", IcPicture = "", IcDoc = "";

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
        }

        public static void FillRound(Graphics g, Brush b, RectangleF r, float rad) { using (var p = Round(r, rad)) g.FillPath(b, p); }
        public static void FillRound(Graphics g, Color c, RectangleF r, float rad) { using (var b = new SolidBrush(c)) FillRound(g, b, r, rad); }
        public static void StrokeRound(Graphics g, Color c, RectangleF r, float rad, float w = 1)
        { using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p); }

        public static Brush Accent(RectangleF r, float angle = 0)
        {
            if (r.Width < 1) r.Width = 1; if (r.Height < 1) r.Height = 1;
            var b = new LinearGradientBrush(r, Green, Yellow, angle);
            return b;
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        public static Color A(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }

        public static Image Res(string name)
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("MrBeanLauncher.Assets." + name);
            if (s == null) return null;
            var ms = new MemoryStream(); s.CopyTo(ms); ms.Position = 0;
            return Image.FromStream(ms);
        }

        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center)
        {
            using (var sf = new StringFormat { Alignment = h, LineAlignment = v, Trimming = StringTrimming.EllipsisCharacter })
            using (var b = new SolidBrush(c))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                g.DrawString(s, f, b, r, sf);
            }
        }

        public static void TextWrap(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r);
        }
    }
}
