using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MrBeanLauncher.Ui;

public class XButton : XControl
{
	public BtnStyle Style = BtnStyle.Secondary;

	public string Icon;

	public string Sub;

	public float Radius = 10f;

	public float FontPx = 14f;

	public float Progress = -1f;

	protected override void Draw(Graphics g)
	{
		RectangleF rectangleF = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
		float num = Theme.Sf(Radius);
		float num2 = PressT * Theme.Sf(1.5f);
		rectangleF.Inflate(0f - num2, 0f - num2);
		bool enabled = Enabled;
		switch (Style)
		{
		case BtnStyle.Primary:
		{
			if (enabled)
			{
				for (int num3 = 3; num3 >= 1; num3--)
				{
					RectangleF r = rectangleF;
					r.Inflate(Theme.Sf(num3 * 2), Theme.Sf(num3 * 2));
					Theme.FillRound(g, Theme.A(Theme.Green, (int)((10f + 14f * HoverT) / (float)num3)), r, num + Theme.Sf(num3 * 2));
				}
			}
			using (LinearGradientBrush b = new LinearGradientBrush(rectangleF, enabled ? Theme.Lerp(Theme.Green, Theme.Lime, HoverT * 0.4f) : Color.FromArgb(60, 70, 60), enabled ? Theme.Lerp(Theme.Yellow, Theme.Lime, HoverT * 0.3f) : Color.FromArgb(70, 70, 60), 0f))
			{
				Theme.FillRound(g, b, rectangleF, num);
			}
			if (Progress >= 0f)
			{
				Theme.FillRound(g, Theme.A(Color.Black, 120), rectangleF, num);
				RectangleF r2 = rectangleF;
				r2.Width = Math.Max(num * 2f, rectangleF.Width * Math.Min(1f, Progress));
				using LinearGradientBrush b2 = new LinearGradientBrush(rectangleF, Theme.Green, Theme.Yellow, 0f);
				Theme.FillRound(g, b2, r2, num);
			}
			using (LinearGradientBrush b3 = new LinearGradientBrush(rectangleF, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
			{
				RectangleF r3 = rectangleF;
				r3.Height /= 2f;
				Theme.FillRound(g, b3, r3, num);
			}
			break;
		}
		case BtnStyle.Secondary:
			Theme.FillRound(g, Theme.Lerp(Theme.Card, Theme.CardHover, HoverT), rectangleF, num);
			Theme.StrokeRound(g, Theme.Lerp(Theme.Border, Theme.A(Theme.Green, 160), HoverT), rectangleF, num);
			break;
		case BtnStyle.Ghost:
			if (HoverT > 0f)
			{
				Theme.FillRound(g, Theme.A(Color.White, (int)(18f * HoverT)), rectangleF, num);
			}
			break;
		case BtnStyle.Danger:
			Theme.FillRound(g, Theme.Lerp(Theme.Card, Color.FromArgb(70, 30, 36), HoverT), rectangleF, num);
			Theme.StrokeRound(g, Theme.Lerp(Theme.Border, Theme.Red, HoverT), rectangleF, num);
			break;
		}
		Color c;
		if (Style != BtnStyle.Primary)
		{
			c = ((Style == BtnStyle.Danger) ? Theme.Lerp(Theme.Muted, Theme.Red, HoverT) : Theme.Lerp(Theme.Fg, Color.White, HoverT));
		}
		else
		{
			c = ((Progress >= 0f) ? Color.White : Color.FromArgb(14, 18, 10));
		}
		if (!enabled && Style != BtnStyle.Primary)
		{
			c = Theme.Dim;
		}
		using Font font = ((Style == BtnStyle.Primary) ? Theme.Black(FontPx) : Theme.Bold(FontPx));
		using Font font2 = Theme.Icon(FontPx + 2f);
		float num4 = 0f;
		if (!string.IsNullOrEmpty(Icon) && !string.IsNullOrEmpty(Text))
		{
			num4 = g.MeasureString(Icon, font2).Width;
		}
		SizeF sizeF = g.MeasureString(Text ?? "", font);
		float num5 = num4 + ((num4 > 0f) ? Theme.Sf(4f) : 0f) + sizeF.Width;
		float num6 = rectangleF.X + (rectangleF.Width - num5) / 2f;
		float num7 = rectangleF.Y + rectangleF.Height / 2f - ((Sub != null) ? Theme.Sf(9f) : 0f);
		if (!string.IsNullOrEmpty(Icon) && string.IsNullOrEmpty(Text))
		{
			Theme.Text(g, Icon, font2, c, rectangleF, StringAlignment.Center);
			return;
		}
		if (num4 > 0f)
		{
			Theme.Text(g, Icon, font2, c, new RectangleF(num6, num7 - Theme.Sf(12f), num4, Theme.Sf(24f)));
		}
		Theme.Text(g, Text, font, c, new RectangleF(num6 + num4 + ((num4 > 0f) ? Theme.Sf(4f) : 0f), num7 - sizeF.Height / 2f, sizeF.Width + 4f, sizeF.Height));
		if (Sub != null)
		{
			using (Font f = Theme.Reg(11.5f))
			{
				Theme.Text(g, Sub, f, Theme.A(c, 180), new RectangleF(rectangleF.X, num7 + Theme.Sf(13f), rectangleF.Width, Theme.Sf(16f)), StringAlignment.Center);
				return;
			}
		}
	}
}
