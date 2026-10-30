using System;
using System.Drawing;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class TitleBar : Control
{
	private readonly MainForm f;

	public TitleBar(MainForm f)
	{
		TitleBar titleBar = this;
		this.f = f;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		XButton close = new XButton
		{
			Style = BtnStyle.Ghost,
			Icon = "\ue8bb",
			FontPx = 11f,
			Size = new Size(Theme.S(46f), Theme.S(32f)),
			Radius = 8f
		};
		XButton min = new XButton
		{
			Style = BtnStyle.Ghost,
			Icon = "\ue921",
			FontPx = 11f,
			Size = new Size(Theme.S(46f), Theme.S(32f)),
			Radius = 8f
		};
		close.Click += (object s, EventArgs e) =>
		{
			f.Close();
		};
		min.Click += (object s, EventArgs e) =>
		{
			f.WindowState = FormWindowState.Minimized;
		};
		Controls.Add(close);
		Controls.Add(min);
		Layout += (object s, LayoutEventArgs e) =>
		{
			close.Location = new Point(titleBar.Width - Theme.S(54f), Theme.S(6f));
			min.Location = new Point(titleBar.Width - Theme.S(102f), Theme.S(6f));
		};
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.Button == MouseButtons.Left)
		{
			Native.DragWindow(f.Handle);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Hq(graphics);
		using (Font font = Theme.Bold(13f))
		{
			Theme.Text(graphics, "MrBean Launcher", font, Theme.Fg, new RectangleF(Theme.S(28f), 0f, Theme.S(200f), Height));
		}
		using Font font2 = Theme.Reg(12f);
		Theme.Text(graphics, "v2.1  •  Minecraft 1.21.11", font2, Theme.Muted, new RectangleF(Theme.S(150f), 0f, Theme.S(300f), Height));
	}
}
