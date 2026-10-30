using System.Drawing;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class Page : Panel
{
	protected readonly MainForm F;

	public Page(MainForm f)
	{
		F = f;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
	}

	protected void Header(Graphics g, string title, string sub)
	{
		using (Font f = Theme.Black(30f))
		{
			Theme.Text(g, title, f, Theme.Fg, new RectangleF(Theme.S(36f), Theme.S(4f), Theme.S(600f), Theme.S(42f)));
		}
		using Font f2 = Theme.Reg(14f);
		Theme.Text(g, sub, f2, Theme.Muted, new RectangleF(Theme.S(38f), Theme.S(46f), Theme.S(700f), Theme.S(22f)));
	}

	protected T Add<T>(T c, int x, int y, int w, int h) where T : Control
	{
		c.Bounds = new Rectangle(Theme.S(x), Theme.S(y), Theme.S(w), Theme.S(h));
		Controls.Add(c);
		return c;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Theme.Hq(e.Graphics);
		Paint2(e.Graphics);
	}

	protected virtual void Paint2(Graphics g)
	{
	}
}
