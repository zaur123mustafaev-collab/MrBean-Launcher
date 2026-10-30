using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MrBeanLauncher.Ui;

public class NavItem : XControl
{
	public string Icon;

	public bool Active;

	private float act;

	public NavItem()
	{
		Size = new Size(Theme.S(72f), Theme.S(64f));
	}

	public override void Step()
	{
		base.Step();
		float num = (Active ? 1 : 0);
		float num2 = act + (num - act) * 0.2f;
		if (Math.Abs(num2 - num) < 0.01f)
		{
			num2 = num;
		}
		if (num2 != act)
		{
			act = num2;
			Invalidate();
		}
	}

	protected override void Draw(Graphics g)
	{
		RectangleF r = new RectangleF(Theme.S(8f), Theme.S(4f), Width - Theme.S(16f), Height - Theme.S(8f));
		if (HoverT > 0f || act > 0f)
		{
			Theme.FillRound(g, Theme.A(Color.White, (int)(10f * HoverT + 14f * act)), r, Theme.Sf(12f));
		}
		if (act > 0.01f)
		{
			using LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0f, r.Y, Theme.Sf(4f), r.Height), Theme.Green, Theme.Yellow, 90f);
			Theme.FillRound(g, b, new RectangleF(Theme.Sf(1f), r.Y + r.Height * (0.5f - 0.3f * act), Theme.Sf(3f), r.Height * 0.6f * act), Theme.Sf(2f));
		}
		Color c = Theme.Lerp(Theme.Lerp(Theme.Muted, Theme.Fg, HoverT), Theme.Green, act);
		using (Font f = Theme.Icon(20f))
		{
			Theme.Text(g, Icon, f, c, new RectangleF(0f, r.Y + Theme.Sf(6f), Width, Theme.Sf(26f)), StringAlignment.Center);
		}
		using Font f2 = Theme.Reg(11f);
		Theme.Text(g, Text, f2, Theme.Lerp(Theme.Muted, Theme.Fg, Math.Max(HoverT, act)), new RectangleF(0f, r.Y + Theme.Sf(32f), Width, Theme.Sf(18f)), StringAlignment.Center);
	}
}
