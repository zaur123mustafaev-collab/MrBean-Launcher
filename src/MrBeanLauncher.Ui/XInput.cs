using System;
using System.Drawing;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class XInput : XControl
{
	public readonly TextBox Box = new TextBox();

	public string Icon;

	public string Placeholder = "";

	public override string Text
	{
		get
		{
			return Box.Text;
		}
		set
		{
			Box.Text = value;
		}
	}

	public XInput()
	{
		Cursor = Cursors.IBeam;
		Box.BorderStyle = BorderStyle.None;
		Box.BackColor = Theme.Card;
		Box.ForeColor = Theme.Fg;
		Box.Font = Theme.Reg(15f);
		Controls.Add(Box);
		Box.HandleCreated += (object s, EventArgs e) =>
		{
			try
			{
				Native.SetCue(Box.Handle, Placeholder);
			}
			catch
			{
			}
		};
		Box.GotFocus += (object s, EventArgs e) =>
		{
			Invalidate();
		};
		Box.LostFocus += (object s, EventArgs e) =>
		{
			Invalidate();
		};
		Box.TextChanged += (object s, EventArgs e) =>
		{
			Invalidate();
		};
		Box.MouseEnter += (object s, EventArgs e) =>
		{
			hover = true;
		};
		Box.MouseLeave += (object s, EventArgs e) =>
		{
			hover = false;
		};
		Height = Theme.S(44f);
	}

	protected override void OnClick(EventArgs e)
	{
		Box.Focus();
		base.OnClick(e);
	}

	protected override void OnLayout(LayoutEventArgs e)
	{
		base.OnLayout(e);
		int num = (string.IsNullOrEmpty(Icon) ? Theme.S(14f) : Theme.S(40f));
		Box.SetBounds(num, (Height - Box.PreferredHeight) / 2, Width - num - Theme.S(12f), Box.PreferredHeight);
	}

	protected override void Draw(Graphics g)
	{
		RectangleF r = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
		Theme.FillRound(g, Theme.Card, r, Theme.Sf(10f));
		bool focused = Box.Focused;
		Theme.StrokeRound(g, focused ? Theme.Green : Theme.Lerp(Theme.Border, Color.FromArgb(70, 76, 96), HoverT), r, Theme.Sf(10f), focused ? Theme.Sf(1.5f) : 1f);
		if (!string.IsNullOrEmpty(Icon))
		{
			using (Font f = Theme.Icon(16f))
			{
				Theme.Text(g, Icon, f, focused ? Theme.Green : Theme.Muted, new RectangleF(Theme.S(12f), 0f, Theme.S(24f), Height));
			}
		}
	}
}
