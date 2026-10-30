using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    /// <summary>Глобальный таймер анимаций.</summary>
    public static class Anim
    {
        static readonly Timer timer = new Timer { Interval = 15 };
        static readonly List<XControl> list = new List<XControl>();
        static Anim() { timer.Tick += (s, e) => { foreach (var c in list.ToArray()) if (!c.IsDisposed && c.Visible) c.Step(); }; }
        public static void Add(XControl c) { list.Add(c); if (!timer.Enabled) timer.Start(); }
        public static void Remove(XControl c) { list.Remove(c); }
    }

    /// <summary>База для своих контролов: двойная буферизация, прозрачность, hover/press-анимация.</summary>
    public class XControl : Control
    {
        public float HoverT, PressT;
        protected bool hover, press;
        public XControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Anim.Add(this);
        }
        protected override void Dispose(bool d) { Anim.Remove(this); base.Dispose(d); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; press = false; base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) press = true; base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { press = false; base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        public virtual void Step()
        {
            float th = hover && Enabled ? 1 : 0, tp = press && Enabled ? 1 : 0;
            float nh = HoverT + (th - HoverT) * 0.22f, np = PressT + (tp - PressT) * 0.35f;
            if (Math.Abs(nh - th) < 0.01f) nh = th; if (Math.Abs(np - tp) < 0.01f) np = tp;
            if (nh != HoverT || np != PressT) { HoverT = nh; PressT = np; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e) { Hq(e.Graphics); Draw(e.Graphics); }
        protected virtual void Draw(Graphics g) { }
    }

    public enum BtnStyle { Primary, Secondary, Ghost, Danger }

    public class XButton : XControl
    {
        public BtnStyle Style = BtnStyle.Secondary;
        public string Icon;
        public string Sub;
        public float Radius = 10;
        public float FontPx = 14;
        public float Progress = -1;   // для кнопки «Играть» во время загрузки

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Sf(Radius);
            float shrink = PressT * Sf(1.5f);
            r.Inflate(-shrink, -shrink);
            bool en = Enabled;
            switch (Style)
            {
                case BtnStyle.Primary:
                    // свечение
                    if (en)
                        for (int i = 3; i >= 1; i--)
                        {
                            var gr = r; gr.Inflate(Sf(i * 2), Sf(i * 2));
                            FillRound(g, A(Green, (int)((10 + 14 * HoverT) / i)), gr, rad + Sf(i * 2));
                        }
                    using (var b = new LinearGradientBrush(r, en ? Lerp(Green, Lime, HoverT * 0.4f) : Color.FromArgb(60, 70, 60),
                                                           en ? Lerp(Yellow, Lime, HoverT * 0.3f) : Color.FromArgb(70, 70, 60), 0f))
                        FillRound(g, b, r, rad);
                    if (Progress >= 0)
                    {
                        FillRound(g, A(Color.Black, 120), r, rad);
                        var pr = r; pr.Width = Math.Max(rad * 2, r.Width * Math.Min(1, Progress));
                        using (var b = new LinearGradientBrush(r, Green, Yellow, 0f)) FillRound(g, b, pr, rad);
                    }
                    // блик
                    using (var hl = new LinearGradientBrush(r, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    { var hr = r; hr.Height /= 2; FillRound(g, hl, hr, rad); }
                    break;
                case BtnStyle.Secondary:
                    FillRound(g, Lerp(Card, CardHover, HoverT), r, rad);
                    StrokeRound(g, Lerp(Border, A(Green, 160), HoverT), r, rad);
                    break;
                case BtnStyle.Ghost:
                    if (HoverT > 0) FillRound(g, A(Color.White, (int)(18 * HoverT)), r, rad);
                    break;
                case BtnStyle.Danger:
                    FillRound(g, Lerp(Card, Color.FromArgb(70, 30, 36), HoverT), r, rad);
                    StrokeRound(g, Lerp(Border, Red, HoverT), r, rad);
                    break;
            }
            Color fg = Style == BtnStyle.Primary ? (Progress >= 0 ? Color.White : Color.FromArgb(14, 18, 10)) : (Style == BtnStyle.Danger ? Lerp(Muted, Red, HoverT) : Lerp(Theme.Fg, Color.White, HoverT));
            if (!en && Style != BtnStyle.Primary) fg = Dim;
            using (var f = Style == BtnStyle.Primary ? Black(FontPx) : Bold(FontPx))
            using (var fi = Theme.Icon(FontPx + 2))
            {
                float iw = 0;
                if (!string.IsNullOrEmpty(Icon) && !string.IsNullOrEmpty(Text)) iw = g.MeasureString(Icon, fi).Width;
                var ts = g.MeasureString(Text ?? "", f);
                float total = iw + (iw > 0 ? Sf(4) : 0) + ts.Width;
                float x = r.X + (r.Width - total) / 2;
                float cy = r.Y + r.Height / 2 - (Sub != null ? Sf(9) : 0);
                if (!string.IsNullOrEmpty(Icon) && string.IsNullOrEmpty(Text))
                { Theme.Text(g, Icon, fi, fg, r, StringAlignment.Center, StringAlignment.Center); return; }
                if (iw > 0) Theme.Text(g, Icon, fi, fg, new RectangleF(x, cy - Sf(12), iw, Sf(24)));
                Theme.Text(g, Text, f, fg, new RectangleF(x + iw + (iw > 0 ? Sf(4) : 0), cy - ts.Height / 2, ts.Width + 4, ts.Height));
                if (Sub != null)
                    using (var fs = Reg(11.5f))
                        Theme.Text(g, Sub, fs, A(fg, 180), new RectangleF(r.X, cy + Sf(13), r.Width, Sf(16)), StringAlignment.Center);
            }
        }
    }

    public class XToggle : XControl
    {
        bool on; float t;
        public event EventHandler Changed;
        public bool Checked { get { return on; } set { if (on != value) { on = value; Invalidate(); } } }
        public XToggle() { Size = new Size(S(44), S(24)); }
        protected override void OnClick(EventArgs e) { on = !on; Changed?.Invoke(this, e); base.OnClick(e); }
        public override void Step()
        {
            base.Step();
            float tt = on ? 1 : 0; float n = t + (tt - t) * 0.25f; if (Math.Abs(n - tt) < 0.01f) n = tt;
            if (n != t) { t = n; Invalidate(); }
        }
        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = r.Height / 2;
            FillRound(g, Lerp(Color.FromArgb(46, 50, 66), Green, t), r, rad);
            if (t > 0.01f) using (var b = new LinearGradientBrush(r, A(Green, (int)(255 * t)), A(Yellow, (int)(255 * t)), 0f)) FillRound(g, b, r, rad);
            float d = r.Height - Sf(6);
            float x = r.X + Sf(3) + (r.Width - d - Sf(6)) * t;
            using (var b = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(b, x, r.Y + Sf(4), d, d);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x, r.Y + Sf(3), d, d);
        }
    }

    public class XSlider : XControl
    {
        public int Min = 1, Max = 10, Value = 4;
        public Func<int, string> Format = v => v.ToString();
        public event EventHandler Changed;
        bool drag;
        public XSlider() { Height = S(40); }
        void SetFromMouse(int mx)
        {
            float pad = Sf(10);
            float t = (mx - pad) / Math.Max(1, Width - pad * 2);
            int v = (int)Math.Round(Min + (Max - Min) * Math.Max(0, Math.Min(1, t)));
            if (v != Value) { Value = v; Changed?.Invoke(this, EventArgs.Empty); Invalidate(); }
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); drag = true; SetFromMouse(e.X); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (drag) SetFromMouse(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = false; }
        protected override void Draw(Graphics g)
        {
            float pad = Sf(10);
            float cy = Height - Sf(12);
            float w = Width - pad * 2;
            float t = Max == Min ? 0 : (Value - Min) / (float)(Max - Min);
            var track = new RectangleF(pad, cy - Sf(3), w, Sf(6));
            FillRound(g, Color.FromArgb(40, 44, 58), track, Sf(3));
            var fill = track; fill.Width = Math.Max(Sf(6), w * t);
            using (var b = new LinearGradientBrush(track, Green, Yellow, 0f)) FillRound(g, b, fill, Sf(3));
            // деления
            int steps = Max - Min;
            if (steps <= 32)
                using (var b = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    for (int i = 0; i <= steps; i++) g.FillEllipse(b, pad + w * i / steps - Sf(1), cy + Sf(6), Sf(2), Sf(2));
            float kx = pad + w * t;
            float kr = Sf(8 + 2 * HoverT);
            using (var b = new SolidBrush(A(Green, (int)(60 * HoverT)))) g.FillEllipse(b, kx - kr - Sf(4), cy - kr - Sf(4), (kr + Sf(4)) * 2, (kr + Sf(4)) * 2);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, kx - kr, cy - kr, kr * 2, kr * 2);
            // пузырь со значением
            using (var f = Bold(12))
            {
                string s = Format(Value);
                var sz = g.MeasureString(s, f);
                var br = new RectangleF(Math.Max(0, Math.Min(Width - sz.Width - Sf(12), kx - sz.Width / 2 - Sf(6))), 0, sz.Width + Sf(12), Sf(20));
                FillRound(g, CardHover, br, Sf(6));
                Theme.Text(g, s, f, Theme.Fg, br, StringAlignment.Center);
            }
        }
    }

    /// <summary>Поле ввода в скруглённой рамке.</summary>
    public class XInput : XControl
    {
        public readonly TextBox Box = new TextBox();
        public string Icon;
        public string Placeholder = "";
        public XInput()
        {
            Cursor = Cursors.IBeam;
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Card;
            Box.ForeColor = Theme.Fg;
            Box.Font = Reg(15);
            Controls.Add(Box);
            Box.HandleCreated += (s, e) => { try { Native.SetCue(Box.Handle, Placeholder); } catch { } };
            Box.GotFocus += (s, e) => Invalidate();
            Box.LostFocus += (s, e) => Invalidate();
            Box.TextChanged += (s, e) => Invalidate();
            Box.MouseEnter += (s, e) => hover = true;
            Box.MouseLeave += (s, e) => hover = false;
            Height = S(44);
        }
        protected override void OnClick(EventArgs e) { Box.Focus(); base.OnClick(e); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int left = string.IsNullOrEmpty(Icon) ? S(14) : S(40);
            Box.SetBounds(left, (Height - Box.PreferredHeight) / 2, Width - left - S(12), Box.PreferredHeight);
        }
        public override string Text { get { return Box.Text; } set { Box.Text = value; } }
        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            FillRound(g, Card, r, Sf(10));
            bool f = Box.Focused;
            StrokeRound(g, f ? Green : Lerp(Border, Color.FromArgb(70, 76, 96), HoverT), r, Sf(10), f ? Sf(1.5f) : 1);
            if (!string.IsNullOrEmpty(Icon)) using (var fi = Theme.Icon(16)) Theme.Text(g, Icon, fi, f ? Green : Muted, new RectangleF(S(12), 0, S(24), Height));
        }
    }

    /// <summary>Переключатель из нескольких вариантов.</summary>
    public class XSegmented : XControl
    {
        public string[] Items = new string[0];
        public int Selected;
        float anim = -1;
        int hoverIdx = -1;
        public event EventHandler Changed;
        public XSegmented() { Height = S(38); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = Index(e.X); if (i != hoverIdx) { hoverIdx = i; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverIdx = -1; Invalidate(); }
        int Index(int x) { return Items.Length == 0 ? -1 : Math.Max(0, Math.Min(Items.Length - 1, x * Items.Length / Math.Max(1, Width))); }
        protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); int i = Index(e.X); if (i != Selected) { Selected = i; Changed?.Invoke(this, e); } }
        public override void Step()
        {
            base.Step();
            if (anim < 0) anim = Selected;
            float n = anim + (Selected - anim) * 0.25f; if (Math.Abs(n - Selected) < 0.01f) n = Selected;
            if (n != anim) { anim = n; Invalidate(); }
        }
        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            FillRound(g, Card, r, Sf(10));
            StrokeRound(g, Border, r, Sf(10));
            if (Items.Length == 0) return;
            float w = (r.Width - Sf(6)) / Items.Length;
            float a = anim < 0 ? Selected : anim;
            var sel = new RectangleF(r.X + Sf(3) + w * a, r.Y + Sf(3), w, r.Height - Sf(6));
            using (var b = new LinearGradientBrush(sel, Green, Yellow, 0f)) FillRound(g, b, sel, Sf(8));
            using (var f = Bold(13))
                for (int i = 0; i < Items.Length; i++)
                {
                    var ir = new RectangleF(r.X + Sf(3) + w * i, r.Y, w, r.Height);
                    bool s = Math.Abs(a - i) < 0.5f;
                    Theme.Text(g, Items[i], f, s ? Color.FromArgb(14, 18, 10) : (i == hoverIdx ? Theme.Fg : Muted), ir, StringAlignment.Center);
                }
        }
    }

    /// <summary>Пункт бокового меню.</summary>
    public class NavItem : XControl
    {
        public string Icon; public bool Active; float act;
        public NavItem() { Size = new Size(S(72), S(64)); }
        public override void Step()
        {
            base.Step();
            float t = Active ? 1 : 0; float n = act + (t - act) * 0.2f; if (Math.Abs(n - t) < 0.01f) n = t;
            if (n != act) { act = n; Invalidate(); }
        }
        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(S(8), S(4), Width - S(16), Height - S(8));
            if (HoverT > 0 || act > 0) FillRound(g, A(Color.White, (int)(10 * HoverT + 14 * act)), r, Sf(12));
            if (act > 0.01f)
                using (var b = new LinearGradientBrush(new RectangleF(0, r.Y, Sf(4), r.Height), Green, Yellow, 90f))
                    FillRound(g, b, new RectangleF(Sf(1), r.Y + r.Height * (0.5f - 0.3f * act), Sf(3), r.Height * 0.6f * act), Sf(2));
            Color c = Lerp(Lerp(Muted, Theme.Fg, HoverT), Green, act);
            using (var fi = Theme.Icon(20)) Theme.Text(g, Icon, fi, c, new RectangleF(0, r.Y + Sf(6), Width, Sf(26)), StringAlignment.Center);
            using (var f = Reg(11)) Theme.Text(g, Text, f, Lerp(Muted, Theme.Fg, Math.Max(HoverT, act)), new RectangleF(0, r.Y + Sf(32), Width, Sf(18)), StringAlignment.Center);
        }
    }

    /// <summary>Скруглённая карточка-подложка.</summary>
    public class XCard : Panel
    {
        public string Title, Icon, Hint;
        public int Radius = 16;
        public XCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            FillRound(g, A(Card, 235), r, Sf(Radius));
            StrokeRound(g, A(Border, 200), r, Sf(Radius));
            if (Title != null)
            {
                float x = Sf(18);
                if (Icon != null)
                {
                    var ir = new RectangleF(Sf(16), Sf(14), Sf(30), Sf(30));
                    FillRound(g, A(Green, 30), ir, Sf(8));
                    using (var fi = Theme.Icon(15)) Theme.Text(g, Icon, fi, Green, ir, StringAlignment.Center);
                    x = Sf(56);
                }
                using (var f = Bold(15)) Theme.Text(g, Title, f, Theme.Fg, new RectangleF(x, Sf(12), Width - x - Sf(10), Sf(20)));
                if (Hint != null) using (var f = Reg(12)) Theme.Text(g, Hint, f, Muted, new RectangleF(x, Sf(30), Width - x - Sf(10), Sf(18)));
            }
        }
    }

    /// <summary>Подпись без фона.</summary>
    public class XLabel : Control
    {
        public Color Color = Theme.Fg; public float Px = 13; public bool IsBold; public StringAlignment Align = StringAlignment.Near;
        public bool Wrap;
        public XLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Hq(e.Graphics);
            using (var f = IsBold ? Bold(Px) : Reg(Px))
            {
                if (Wrap) TextWrap(e.Graphics, Text, f, Color, new RectangleF(0, 0, Width, Height));
                else Theme.Text(e.Graphics, Text, f, Color, new RectangleF(0, 0, Width, Height), Align);
            }
        }
    }
}
