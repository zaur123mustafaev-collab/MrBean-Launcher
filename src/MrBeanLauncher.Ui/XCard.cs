using System.Drawing;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class XCard : Panel
{
	public string Title;

	public string Icon;

	public string Hint;

	public int Radius = 16;

	public XCard()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Hq(graphics);
		RectangleF r = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
		Theme.FillRound(graphics, Theme.A(Theme.Card, 235), r, Theme.Sf(Radius));
		Theme.StrokeRound(graphics, Theme.A(Theme.Border, 200), r, Theme.Sf(Radius));
		if (Title == null)
		{
			return;
		}
		float num = Theme.Sf(18f);
		if (Icon != null)
		{
			RectangleF r2 = new RectangleF(Theme.Sf(16f), Theme.Sf(14f), Theme.Sf(30f), Theme.Sf(30f));
			Theme.FillRound(graphics, Theme.A(Theme.Green, 30), r2, Theme.Sf(8f));
			using (Font f = Theme.Icon(15f))
			{
				Theme.Text(graphics, Icon, f, Theme.Green, r2, StringAlignment.Center);
			}
			num = Theme.Sf(56f);
		}
		using (Font f2 = Theme.Bold(15f))
		{
			Theme.Text(graphics, Title, f2, Theme.Fg, new RectangleF(num, Theme.Sf(12f), (float)Width - num - Theme.Sf(10f), Theme.Sf(20f)));
		}
		if (Hint != null)
		{
			using (Font f3 = Theme.Reg(12f))
			{
				Theme.Text(graphics, Hint, f3, Theme.Muted, new RectangleF(num, Theme.Sf(30f), (float)Width - num - Theme.Sf(10f), Theme.Sf(18f)));
			}
		}
	}
}
