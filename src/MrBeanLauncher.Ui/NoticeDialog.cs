using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MrBeanLauncher.Core;

namespace MrBeanLauncher.Ui;

/// <summary>
/// Аккуратное окно-объяснение: заголовок, что произошло и по шагам, что делать.
/// Стиль тот же, что у окна входа через Microsoft.
/// </summary>
public class NoticeDialog : Form
{
	private class Seg
	{
		public string Text;

		public float Y;

		public float H;

		public float Px;

		public bool Bold;

		public bool Badge;

		public int Number;

		public Color Color;
	}

	private readonly OAuthError info;

	private readonly List<Seg> segs = new List<Seg>();

	private readonly float contentW;

	private bool isError = true;

	public NoticeDialog(OAuthError info, bool isError = true)
		: this(info, isError, null)
	{
	}

	public NoticeDialog(OAuthError info, bool isError, string extra)
	{
		this.info = info;
		this.isError = isError;
		FormBorderStyle = FormBorderStyle.None;
		AutoScaleMode = AutoScaleMode.None;
		StartPosition = FormStartPosition.CenterParent;
		ShowInTaskbar = false;
		BackColor = Theme.Bg;
		DoubleBuffered = true;
		Text = info.Title ?? "MrBean Launcher";
		contentW = Theme.Sf(480f);
		using Bitmap bmp = new Bitmap(1, 1);
		using Graphics g = Graphics.FromImage(bmp);
		Theme.Hq(g);
		float y = Theme.Sf(30f);
		string titleText = info.Title ?? "Готово";
		float titleH;
		using (Font f = Theme.Bold(19f))
		{
			titleH = g.MeasureString(titleText, f, (int)contentW - Theme.S(56)).Height;
		}
		segs.Add(new Seg
		{
			Text = titleText,
			Y = y,
			H = titleH,
			Px = 19f,
			Bold = true,
			Badge = true,
			Color = Theme.Fg
		});
		y += Math.Max(titleH, Theme.Sf(44f)) + Theme.Sf(16f);
		string detail = ((info.Detail ?? "") + ((extra == null) ? "" : ("\n\n" + extra))).Trim();
		if (detail.Length != 0)
		{
			float h;
			using (Font f2 = Theme.Reg(13f))
			{
				h = g.MeasureString(detail, f2, (int)contentW).Height;
			}
			segs.Add(new Seg
			{
				Text = detail,
				Y = y,
				H = h,
				Px = 13f,
				Color = Theme.Muted
			});
			y += h + Theme.Sf(14f);
		}
		if (info.Fix != null && info.Fix.Count > 0)
		{
			float hh;
			using (Font f3 = Theme.Bold(13f))
			{
				hh = g.MeasureString("Что сделать:", f3, (int)contentW).Height;
			}
			segs.Add(new Seg
			{
				Text = "Что сделать:",
				Y = y,
				H = hh,
				Px = 13f,
				Bold = true,
				Color = Theme.Lime
			});
			y += hh + Theme.Sf(8f);
			float indent = Theme.Sf(30f);
			using (Font f4 = Theme.Reg(12.5f))
			{
				for (int i = 0; i < info.Fix.Count; i++)
				{
					Seg seg = new Seg
					{
						Text = info.Fix[i],
						Y = y,
						H = g.MeasureString(info.Fix[i], f4, (int)(contentW - indent)).Height,
						Px = 12.5f,
						Number = i + 1,
						Color = Theme.Fg
					};
					segs.Add(seg);
					y += seg.H + Theme.Sf(6f);
				}
			}
			y += Theme.Sf(6f);
		}
		BuildButtons(y);
	}

	private void BuildButtons(float y)
	{
		if (!string.IsNullOrEmpty(info.LinkText) && !string.IsNullOrEmpty(info.LinkUrl))
		{
			XButton link = new XButton
			{
				Style = BtnStyle.Primary,
				Text = info.LinkText,
				Icon = "\ue774",
				FontPx = 13.5f,
				Radius = 12f,
				Bounds = new Rectangle(Theme.S(40f), Theme.S(y), Theme.S(480f), Theme.S(44f))
			};
			link.Click += (object s, EventArgs e) => OpenLink();
			Controls.Add(link);
			y += Theme.S(44f) + Theme.S(10f);
		}
		XButton copy = new XButton
		{
			Style = BtnStyle.Ghost,
			Text = "Скопировать",
			Icon = "\ue8c8",
			FontPx = 12.5f,
			Radius = 10f,
			Bounds = new Rectangle(Theme.S(40f), Theme.S(y), Theme.S(190f), Theme.S(38f))
		};
		copy.Click += (object s, EventArgs e) => CopyText();
		Controls.Add(copy);
		XButton close = new XButton
		{
			Style = BtnStyle.Secondary,
			Text = "Понятно",
			FontPx = 13f,
			Radius = 10f,
			Bounds = new Rectangle(Theme.S(340f), Theme.S(y), Theme.S(180f), Theme.S(38f))
		};
		close.Click += (object s, EventArgs e) => Close();
		Controls.Add(close);
		ClientSize = new Size(Theme.S(560f), Theme.S(y) + Theme.S(66f));
	}

	public void OpenLink()
	{
		try
		{
			Process.Start(new ProcessStartInfo(info.LinkUrl)
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private void CopyText()
	{
		try
		{
			Clipboard.SetText(FullText());
		}
		catch
		{
		}
	}

	public string FullText()
	{
		string text = (info.Title ?? "") + "\n" + (info.Detail ?? "");
		if (info.Fix != null && info.Fix.Count > 0)
		{
			text += "\n\nЧто сделать:";
			for (int i = 0; i < info.Fix.Count; i++)
			{
				text = text + "\n" + (i + 1) + ". " + info.Fix[i];
			}
		}
		if (!string.IsNullOrEmpty(info.LinkUrl))
		{
			text += "\n\n" + info.LinkUrl;
		}
		return text.Trim();
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.Button == MouseButtons.Left)
		{
			Native.DragWindow(Handle);
		}
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		Native.RoundCorners(Handle);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		Theme.Hq(g);
		Color accent = isError ? Theme.Red : Theme.Green;
		Theme.StrokeRound(g, Theme.Border, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.Sf(14f));
		foreach (Seg seg in segs)
		{
			float x = Theme.Sf(40f);
			float w = contentW;
			if (seg.Badge)
			{
				DrawBadge(g, accent, seg);
				x += Theme.Sf(56f);
				w -= Theme.Sf(56f);
			}
			if (seg.Number > 0)
			{
				using Font nf = Theme.Bold(12.5f);
				Theme.Text(g, seg.Number + ".", nf, Theme.Lime, new RectangleF(x, seg.Y, Theme.Sf(24f), seg.H));
				x += Theme.Sf(30f);
				w -= Theme.Sf(30f);
			}
			using Font font = FontFor(seg);
			Theme.TextWrap(g, seg.Text, font, seg.Color, new RectangleF(x, seg.Y, w, seg.H));
		}
	}

	private static Font FontFor(Seg seg)
	{
		if (!seg.Bold)
		{
			return Theme.Reg(seg.Px);
		}
		return (seg.Px > 15f) ? Theme.Black(seg.Px) : Theme.Bold(seg.Px);
	}

	private static void DrawBadge(Graphics g, Color accent, Seg title)
	{
		RectangleF r = new RectangleF(Theme.Sf(40f), title.Y - Theme.Sf(4f), Theme.Sf(44f), Theme.Sf(44f));
		Theme.FillRound(g, Theme.A(accent, 28), r, Theme.Sf(14f));
		Theme.StrokeRound(g, Theme.A(accent, 170), r, Theme.Sf(14f), 1f);
		using Font font = Theme.Icon(20f);
		Theme.Text(g, accent == Theme.Red ? "\ue7ba" : "\ue946", font, accent, r, StringAlignment.Center);
	}
}
