using System;
using System.Drawing;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class XLabel : Control
{
	public Color Color = Theme.Fg;

	public float Px = 13f;

	public bool IsBold;

	public StringAlignment Align;

	public bool Wrap;

	public XLabel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
	}

	protected override void OnTextChanged(EventArgs e)
	{
		base.OnTextChanged(e);
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Theme.Hq(e.Graphics);
		using Font f = (IsBold ? Theme.Bold(Px) : Theme.Reg(Px));
		if (Wrap)
		{
			Theme.TextWrap(e.Graphics, Text, f, Color, new RectangleF(0f, 0f, Width, Height));
		}
		else
		{
			Theme.Text(e.Graphics, Text, f, Color, new RectangleF(0f, 0f, Width, Height), Align);
		}
	}
}
