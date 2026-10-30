using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MrBeanLauncher.Core;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    /// <summary>
    /// Окно входа по коду: большой код, кнопка «Скопировать и открыть сайт».
    /// Человек вводит код на microsoft.com/link, входит там — и окно само закрывается.
    /// </summary>
    public class DeviceCodeDialog : Form
    {
        string code = "", url = "https://www.microsoft.com/link", state = "Подключение к Microsoft...";
        bool error, done;
        readonly XButton open, cancel;
        readonly Timer pulse = new Timer { Interval = 40 };
        float t;
        public event Action Cancelled = delegate { };

        public DeviceCodeDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Bg;
            Size = new Size(S(480), S(380));
            DoubleBuffered = true;
            Text = "Вход через Microsoft";

            open = new XButton { Style = BtnStyle.Primary, Text = "Скопировать код и открыть сайт", Icon = IcGlobe, FontPx = 14, Radius = 12,
                Bounds = new Rectangle(S(40), S(250), S(400), S(46)), Enabled = false };
            open.Click += (s, e) => OpenSite();
            Controls.Add(open);
            cancel = new XButton { Style = BtnStyle.Ghost, Text = "Отмена", FontPx = 13, Radius = 10,
                Bounds = new Rectangle(S(170), S(310), S(140), S(38)) };
            cancel.Click += (s, e) => { if (!done) Cancelled(); Close(); };
            Controls.Add(cancel);

            pulse.Tick += (s, e) => { t += 0.08f; Invalidate(new Rectangle(0, S(205), Width, S(40))); };
            pulse.Start();
            Shown += (s, e) => Native.RoundCorners(Handle);
            FormClosed += (s, e) => pulse.Stop();
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Native.DragWindow(Handle); }

        public void ShowCode(MicrosoftAuth.DeviceCode dc)
        {
            code = dc.UserCode; url = dc.VerificationUri;
            state = "Ожидание входа на сайте Microsoft...";
            open.Enabled = true;
            Invalidate();
            OpenSite();   // сразу копируем код и открываем страницу — человеку остаётся только вставить
        }

        public void SetState(string s, bool err = false) { state = s; error = err; Invalidate(); }

        public void Success(string name)
        {
            done = true; error = false;
            state = "Вы успешно вошли в MrBean Visuals! (" + name + ")";
            open.Visible = false;
            cancel.Text = "Готово";
            Invalidate();
            var close = new Timer { Interval = 1800 };
            close.Tick += (s, e) => { close.Stop(); if (!IsDisposed) Close(); };
            close.Start();
        }

        void OpenSite()
        {
            try { Clipboard.SetText(code); } catch { }
            // ?otc= подставляет код на странице Microsoft сам
            string u = url + (url.Contains("?") ? "&" : "?") + "otc=" + Uri.EscapeDataString(code);
            try { Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); } catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Hq(g);
            StrokeRound(g, Border, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Sf(14));
            using (var fb = Black(22)) Theme.Text(g, "Вход через Microsoft", fb, Fg, new RectangleF(0, Sf(28), Width, Sf(30)), StringAlignment.Center);
            using (var fr = Reg(13)) Theme.Text(g, "Введите код на сайте " + url.Replace("https://", "").Replace("www.", ""), fr, Muted, new RectangleF(0, Sf(66), Width, Sf(20)), StringAlignment.Center);

            var box = new RectangleF(Sf(60), Sf(100), Width - Sf(120), Sf(84));
            FillRound(g, Card, box, Sf(16));
            StrokeRound(g, done ? Green : Border, box, Sf(16), done ? 2 : 1);
            if (code.Length > 0)
                using (var fc = Mono(40)) Theme.Text(g, code, fc, done ? Green : Lime, box, StringAlignment.Center);
            else
                using (var fr = Reg(14)) Theme.Text(g, "Получаю код...", fr, Dim, box, StringAlignment.Center);
            if (code.Length > 0 && !done)
                using (var fs = Reg(11)) Theme.Text(g, "код уже скопирован — просто вставьте (Ctrl+V)", fs, Dim, new RectangleF(0, Sf(188), Width, Sf(16)), StringAlignment.Center);

            // статус с пульсирующей точкой
            Color c = error ? Red : done ? Green : Muted;
            if (!error && !done)
            {
                int a = (int)(140 + 115 * Math.Sin(t * 3));
                using (var b = new SolidBrush(A(Lime, a))) g.FillEllipse(b, Sf(40), Sf(219), Sf(8), Sf(8));
            }
            using (var fs = Bold(13)) Theme.Text(g, state, fs, c, new RectangleF(Sf(54), Sf(210), Width - Sf(94), Sf(26)), StringAlignment.Center);
        }
    }
}
