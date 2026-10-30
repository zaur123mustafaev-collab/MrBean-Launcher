using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class XSegmented : XControl
{
	public string[] Items = new string[0];

	public int Selected;

	private float anim = -1f;

	private int hoverIdx = -1;

	public event EventHandler Changed;

	public XSegmented()
	{
		Height = Theme.S(38f);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		int num = Index(e.X);
		if (num != hoverIdx)
		{
			hoverIdx = num;
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		hoverIdx = -1;
		Invalidate();
	}

	private int Index(int x)
	{
		if (Items.Length != 0)
		{
			return Math.Max(0, Math.Min(Items.Length - 1, x * Items.Length / Math.Max(1, Width)));
		}
		return -1;
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		int num = Index(e.X);
		if (num != Selected)
		{
			Selected = num;
			Changed?.Invoke(this, e);
		}
	}

	public override void Step()
	{
		base.Step();
		if (anim < 0f)
		{
			anim = Selected;
		}
		float num = anim + ((float)Selected - anim) * 0.25f;
		if (Math.Abs(num - (float)Selected) < 0.01f)
		{
			num = Selected;
		}
		if (num != anim)
		{
			anim = num;
			Invalidate();
		}
	}

	protected override void Draw(Graphics g)
	{
		RectangleF r = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
		Theme.FillRound(g, Theme.Card, r, Theme.Sf(10f));
		Theme.StrokeRound(g, Theme.Border, r, Theme.Sf(10f));
		if (Items.Length == 0)
		{
			return;
		}
		float num = (r.Width - Theme.Sf(6f)) / (float)Items.Length;
		float num2 = ((anim < 0f) ? ((float)Selected) : anim);
		RectangleF rectangleF = new RectangleF(r.X + Theme.Sf(3f) + num * num2, r.Y + Theme.Sf(3f), num, r.Height - Theme.Sf(6f));
		using (LinearGradientBrush b = new LinearGradientBrush(rectangleF, Theme.Green, Theme.Yellow, 0f))
		{
			Theme.FillRound(g, b, rectangleF, Theme.Sf(8f));
		}
		using Font font = Theme.Bold(13f);
		for (int i = 0; i < Items.Length; i++)
		{
			RectangleF r2 = new RectangleF(r.X + Theme.Sf(3f) + num * (float)i, r.Y, num, r.Height);
			bool flag = Math.Abs(num2 - (float)i) < 0.5f;
			Graphics g2 = g;
			string s = Items[i];
			Font f = font;
			Color c;
			if (flag)
			{
				c = Color.FromArgb(14, 18, 10);
			}
			else
			{
				c = ((i == hoverIdx) ? Theme.Fg : Theme.Muted);
			}
			Theme.Text(g2, s, f, c, r2, StringAlignment.Center);
		}
	}
}
