using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class Sidebar : Panel
{
	private readonly MainForm f;

	private readonly Image face;

	public Sidebar(MainForm f, Image face)
	{
		this.f = f;
		this.face = face;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
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
		graphics.Clear(Theme.Side);
		using (Pen pen = new Pen(Theme.Border))
		{
			graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
		}
		RectangleF rectangleF = new RectangleF(Theme.S(18f), Theme.S(20f), Theme.S(48f), Theme.S(48f));
		RectangleF rectangleF2 = rectangleF;
		rectangleF2.Inflate(Theme.S(3f), Theme.S(3f));
		using (Brush brush = Theme.Accent(rectangleF2, 45f))
		{
			graphics.FillEllipse(brush, rectangleF2);
		}
		if (face != null)
		{
			using (GraphicsPath graphicsPath = new GraphicsPath())
			{
				graphicsPath.AddEllipse(rectangleF);
				GraphicsState gstate = graphics.Save();
				graphics.SetClip(graphicsPath);
				graphics.DrawImage(face, rectangleF);
				graphics.Restore(gstate);
			}
		}
	}
}
