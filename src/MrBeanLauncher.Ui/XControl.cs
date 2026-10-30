using System;
using System.Drawing;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class XControl : Control
{
	public float HoverT;

	public float PressT;

	protected bool hover;

	protected bool press;

	public XControl()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		Anim.Add(this);
	}

	protected override void Dispose(bool d)
	{
		Anim.Remove(this);
		base.Dispose(d);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		press = false;
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			press = true;
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		press = false;
		base.OnMouseUp(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		base.OnEnabledChanged(e);
		Invalidate();
	}

	public virtual void Step()
	{
		float num = ((hover && Enabled) ? 1 : 0);
		float num2 = ((press && Enabled) ? 1 : 0);
		float num3 = HoverT + (num - HoverT) * 0.22f;
		float num4 = PressT + (num2 - PressT) * 0.35f;
		if (Math.Abs(num3 - num) < 0.01f)
		{
			num3 = num;
		}
		if (Math.Abs(num4 - num2) < 0.01f)
		{
			num4 = num2;
		}
		if (num3 != HoverT || num4 != PressT)
		{
			HoverT = num3;
			PressT = num4;
			Invalidate();
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Theme.Hq(e.Graphics);
		Draw(e.Graphics);
	}

	protected virtual void Draw(Graphics g)
	{
	}
}
