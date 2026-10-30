using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class ContentHost : Panel
{
	private readonly Image bg;

	private Bitmap cache;

	public ContentHost(Image bg)
	{
		this.bg = bg;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		cache?.Dispose();
		cache = null;
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		if (cache == null && Width > 0 && Height > 0)
		{
			cache = new Bitmap(Width, Height);
			using Graphics graphics = Graphics.FromImage(cache);
			Theme.Hq(graphics);
			graphics.Clear(Theme.Bg);
			if (bg != null)
			{
				float num = Math.Max((float)Width / (float)bg.Width, (float)Height / (float)bg.Height);
				float num2 = (float)bg.Width * num;
				float num3 = (float)bg.Height * num;
				graphics.DrawImage(bg, ((float)Width - num2) / 2f, ((float)Height - num3) / 2f - num3 * 0.08f, num2, num3);
			}
			Rectangle rect = new Rectangle(0, 0, Width, Height);
			using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(235, 12, 13, 18), Color.FromArgb(150, 12, 13, 18), 0f))
			{
				graphics.FillRectangle(brush, rect);
			}
			using (LinearGradientBrush brush2 = new LinearGradientBrush(rect, Color.FromArgb(0, 12, 13, 18), Color.FromArgb(245, 12, 13, 18), 90f))
			{
				graphics.FillRectangle(brush2, rect);
			}
			using GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse((float)Width * 0.45f, (float)(-Height) * 0.3f, (float)Width * 0.8f, (float)Height * 0.9f);
			PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb(40, 124, 230, 60);
			pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, 124, 230, 60) };
			using PathGradientBrush brush3 = pathGradientBrush;
			graphics.FillPath(brush3, graphicsPath);
		}
		if (cache != null)
		{
			e.Graphics.DrawImage(cache, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel);
		}
	}
}
