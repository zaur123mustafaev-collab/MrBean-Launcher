using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MrBeanLauncher.Core;

namespace MrBeanLauncher.Ui;

public class HomePage : Page
{
	private class AccountChip : XControl
	{
		private readonly MainForm f;

		public AccountChip(MainForm f)
		{
			this.f = f;
		}

		protected override void Draw(Graphics g)
		{
			Account current = f.Accounts.Current;
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
			Theme.FillRound(g, Theme.Lerp(Theme.Card, Theme.CardHover, HoverT), r, Theme.Sf(10f));
			Theme.StrokeRound(g, Theme.Lerp(Theme.A(Theme.Green, 120), Theme.Green, HoverT), r, Theme.Sf(10f));
			if (current == null)
			{
				return;
			}
			SkinFaces.DrawHead(g, SkinFaces.Get(f, current.Uuid, current.SkinUrl), new RectangleF(Theme.Sf(8f), Theme.Sf(7f), (float)Height - Theme.Sf(14f), (float)Height - Theme.Sf(14f)), current.Name);
			using (Font font = Theme.Bold(14f))
			{
				Theme.Text(g, current.Name, font, Theme.Fg, new RectangleF((float)Height + Theme.Sf(2f), Theme.Sf(5f), (float)(Width - Height) - Theme.Sf(30f), Theme.Sf(20f)));
			}
			using (Font font2 = Theme.Reg(11.5f))
			{
				Theme.Text(g, current.NeedsLogin ? "Нужен вход" : "Microsoft ✓ лицензия", font2, current.NeedsLogin ? Theme.Yellow : Theme.Green, new RectangleF((float)Height + Theme.Sf(2f), Theme.Sf(24f), Width, Theme.Sf(16f)));
			}
			using Font font3 = Theme.Icon(12f);
			Theme.Text(g, "\ue76c", font3, Theme.Muted, new RectangleF((float)Width - Theme.Sf(26f), 0f, Theme.Sf(20f), Height), StringAlignment.Center);
		}
	}

	private class Tile : XControl
	{
		public string Icon;

		public string Title;

		public string Desc;

		protected override void Draw(Graphics g)
		{
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
			Theme.FillRound(g, Theme.A(Theme.Lerp(Theme.Card, Theme.CardHover, HoverT), 225), r, Theme.Sf(14f));
			Theme.StrokeRound(g, Theme.Lerp(Theme.A(Theme.Border, 200), Theme.A(Theme.Green, 150), HoverT), r, Theme.Sf(14f));
			RectangleF rectangleF = new RectangleF(Theme.Sf(14f), ((float)Height - Theme.Sf(40f)) / 2f, Theme.Sf(40f), Theme.Sf(40f));
			using (LinearGradientBrush b = new LinearGradientBrush(rectangleF, Theme.A(Theme.Green, 60 + (int)(40f * HoverT)), Theme.A(Theme.Yellow, 40), 45f))
			{
				Theme.FillRound(g, b, rectangleF, Theme.Sf(10f));
			}
			using (Font f = Theme.Icon(18f))
			{
				Theme.Text(g, Icon, f, Theme.Lime, rectangleF, StringAlignment.Center);
			}
			using (Font f2 = Theme.Bold(14f))
			{
				Theme.Text(g, Title, f2, Theme.Fg, new RectangleF(Theme.Sf(66f), Theme.Sf(16f), (float)Width - Theme.Sf(70f), Theme.Sf(22f)));
			}
			using Font f3 = Theme.Reg(12f);
			Theme.TextWrap(g, Desc, f3, Theme.Muted, new RectangleF(Theme.Sf(66f), Theme.Sf(38f), (float)Width - Theme.Sf(72f), Theme.Sf(36f)));
		}
	}

	private class ProgressLine : Control
	{
		public float Value = -1f;

		private float shimmer;

		private readonly Timer t = new Timer
		{
			Interval = 30
		};

		public ProgressLine()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Color.Transparent;
			t.Tick += (object s, EventArgs e) =>
			{
				if (Value >= 0f)
				{
					shimmer = (shimmer + 0.02f) % 1.4f;
					Invalidate();
				}
			};
			t.Start();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Hq(graphics);
			RectangleF rectangleF = new RectangleF(0f, 0f, Width, Height);
			Theme.FillRound(graphics, Color.FromArgb(36, 40, 54), rectangleF, (float)Height / 2f);
			if (Value < 0f)
			{
				return;
			}
			RectangleF r = rectangleF;
			r.Width = Math.Max(Height, (float)Width * Math.Min(1f, Value));
			using (LinearGradientBrush b = new LinearGradientBrush(rectangleF, Theme.Green, Theme.Yellow, 0f))
			{
				Theme.FillRound(graphics, b, r, (float)Height / 2f);
			}
			float num = (float)Width * (shimmer - 0.2f);
			RectangleF rect = new RectangleF(num, 0f, (float)Width * 0.2f, Height);
			if (!(rect.Right > 0f) || !(rect.X < r.Right))
			{
				return;
			}
			using LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new RectangleF(rect.X - 1f, 0f, rect.Width + 2f, Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 255, 255), 0f);
			linearGradientBrush.SetBlendTriangularShape(0.5f);
			GraphicsState gstate = graphics.Save();
			using (GraphicsPath clip = Theme.Round(r, (float)Height / 2f))
			{
				graphics.SetClip(clip);
				graphics.FillRectangle(linearGradientBrush, rect);
			}
			graphics.Restore(gstate);
		}
	}

	private readonly Image hero;

	public readonly XInput Nick;

	private readonly AccountChip chip;

	private readonly XLabel nickL;

	public readonly XSegmented Profile;

	private readonly XButton play;

	private readonly XLabel status;

	private readonly XLabel speedL;

	private readonly ProgressLine bar;

	private float glow;

	private Bitmap heroCache;

	private readonly Timer pulse;

	public HomePage(MainForm f, Image hero)
		: base(f)
	{
		HomePage homePage = this;
		this.hero = hero;
		XCard xCard = Add(new XCard
		{
			Radius = 20
		}, 36, 476, 964, 140);
		nickL = new XLabel
		{
			Text = "НИК В ИГРЕ",
			Px = 11f,
			IsBold = true,
			Color = Theme.Muted,
			Bounds = new Rectangle(Theme.S(22f), Theme.S(16f), Theme.S(200f), Theme.S(16f))
		};
		xCard.Controls.Add(nickL);
		Nick = new XInput
		{
			Icon = "\ue77b",
			Placeholder = "Например, MrBean",
			Bounds = new Rectangle(Theme.S(20f), Theme.S(36f), Theme.S(250f), Theme.S(46f))
		};
		Nick.Text = f.Cfg.Nick;
		Nick.Box.MaxLength = 16;
		xCard.Controls.Add(Nick);
		chip = new AccountChip(f)
		{
			Bounds = new Rectangle(Theme.S(20f), Theme.S(36f), Theme.S(250f), Theme.S(46f)),
			Visible = false
		};
		chip.Click += (object s, EventArgs e) =>
		{
			f.ShowPage(1);
		};
		xCard.Controls.Add(chip);
		XLabel value = new XLabel
		{
			Text = "СБОРКА",
			Px = 11f,
			IsBold = true,
			Color = Theme.Muted,
			Bounds = new Rectangle(Theme.S(292f), Theme.S(16f), Theme.S(200f), Theme.S(16f))
		};
		xCard.Controls.Add(value);
		Profile = new XSegmented
		{
			Items = new string[2] { "MrBean Visuals", "Чистый Fabric" },
			Selected = ((!f.Cfg.WithVisuals) ? 1 : 0),
			Bounds = new Rectangle(Theme.S(290f), Theme.S(36f), Theme.S(320f), Theme.S(46f))
		};
		Profile.Changed += (object s, EventArgs e) =>
		{
			f.Cfg.WithVisuals = homePage.Profile.Selected == 0;
			f.SaveCfg();
		};
		xCard.Controls.Add(Profile);
		play = new XButton
		{
			Style = BtnStyle.Primary,
			Text = "ИГРАТЬ",
			Sub = "1.21.11 • Fabric",
			FontPx = 24f,
			Radius = 16f,
			Bounds = new Rectangle(Theme.S(636f), Theme.S(18f), Theme.S(306f), Theme.S(70f))
		};
		play.Click += (object s, EventArgs e) =>
		{
			f.PlayClicked();
		};
		xCard.Controls.Add(play);
		bar = new ProgressLine
		{
			Bounds = new Rectangle(Theme.S(22f), Theme.S(104f), Theme.S(600f), Theme.S(6f))
		};
		xCard.Controls.Add(bar);
		status = new XLabel
		{
			Text = "Готов к запуску",
			Px = 13f,
			Color = Theme.Muted,
			Bounds = new Rectangle(Theme.S(20f), Theme.S(112f), Theme.S(640f), Theme.S(22f))
		};
		xCard.Controls.Add(status);
		speedL = new XLabel
		{
			Text = "",
			Px = 13f,
			IsBold = true,
			Color = Theme.Green,
			Align = StringAlignment.Far,
			Bounds = new Rectangle(Theme.S(636f), Theme.S(106f), Theme.S(306f), Theme.S(24f))
		};
		xCard.Controls.Add(speedL);
		UpdateRamHint();
		AddTile(36, 236, "\ue7c1", "PvP HUD", "Таргет-HUD, тотемы, комбо, кулдаун");
		AddTile(282, 236, "\ue8be", "PvE помощники", "Свет, день мира, точка смерти");
		AddTile(36, 326, "\ue7b3", "Визуалы", "Зум, яркость, прицел, без тряски");
		AddTile(282, 326, "\ue765", "Макросы", "6 клавиш с командами");
		pulse = new Timer
		{
			Interval = 30
		};
		pulse.Tick += (object s, EventArgs e) =>
		{
			homePage.glow += 0.04f;
			homePage.Invalidate(new Rectangle(Theme.S(560f), Theme.S(0f), Theme.S(480f), Theme.S(470f)));
		};
		pulse.Start();
	}

	private void AddTile(int x, int y, string icon, string title, string text)
	{
		Add(new Tile
		{
			Icon = icon,
			Title = title,
			Desc = text
		}, x, y, 232, 78);
	}

	public void UpdateRamHint()
	{
		speedL.Text = "";
		speedL.Color = Theme.Muted;
		speedL.Text = "ОЗУ: " + F.Cfg.RamMb / 1024 + " ГБ";
	}

	protected override void Paint2(Graphics g)
	{
		RectangleF r = new RectangleF(Theme.S(36f), Theme.S(8f), Theme.S(330f), Theme.S(28f));
		Theme.FillRound(g, Theme.A(Theme.Green, 28), r, Theme.S(14f));
		Theme.StrokeRound(g, Theme.A(Theme.Green, 90), r, Theme.S(14f));
		using (SolidBrush brush = new SolidBrush(Theme.Green))
		{
			g.FillEllipse(brush, r.X + (float)Theme.S(12f), r.Y + (float)Theme.S(10f), Theme.S(8f), Theme.S(8f));
		}
		using (Font f = Theme.Bold(12f))
		{
			Theme.Text(g, "Minecraft 1.21.11  •  Fabric  •  Java 21", f, Theme.Lime, new RectangleF(r.X + (float)Theme.S(26f), r.Y, r.Width, r.Height));
		}
		using (Font font = Theme.Black(56f))
		{
			Theme.Text(g, "MrBean", font, Color.White, new RectangleF(Theme.S(30f), Theme.S(40f), Theme.S(520f), Theme.S(70f)));
			RectangleF rectangleF = new RectangleF(Theme.S(30f), Theme.S(100f), Theme.S(520f), Theme.S(70f));
			using GraphicsPath graphicsPath = new GraphicsPath();
			using StringFormat format = new StringFormat();
			graphicsPath.AddString("Visuals", font.FontFamily, (int)font.Style, font.Size, new PointF(rectangleF.X, rectangleF.Y + (float)Theme.S(2f)), format);
			using LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(rectangleF.X, rectangleF.Y, Theme.S(280f), Theme.S(70f)), Theme.Green, Theme.Yellow, 0f);
			g.FillPath(brush2, graphicsPath);
		}
		using (Font f2 = Theme.Reg(15f))
		{
			Theme.TextWrap(g, "Красивые визуалы, HUD для PvP и PvE и макросы.\nОдна кнопка — и Minecraft со всеми модами готов.", f2, Theme.Muted, new RectangleF(Theme.S(36f), Theme.S(174f), Theme.S(500f), Theme.S(50f)));
		}
		float num = Theme.S(36f);
		float num2 = Theme.S(420f);
		num = Key(g, "Правый Shift", num, num2);
		num = Hint(g, "меню мода", num, num2);
		num = Key(g, "C", num + (float)Theme.S(14f), num2);
		num = Hint(g, "зум", num, num2);
		if (hero == null)
		{
			return;
		}
		RectangleF rectangleF2 = new RectangleF(Theme.S(560f), Theme.S(4f), Theme.S(440f), Theme.S(440f));
		float num3 = (float)(Math.Sin(glow) * 0.5 + 0.5);
		for (int num4 = 5; num4 >= 1; num4--)
		{
			RectangleF r2 = rectangleF2;
			r2.Inflate(Theme.S(num4 * 3), Theme.S(num4 * 3));
			Theme.FillRound(g, Theme.A(Theme.Lerp(Theme.Green, Theme.Yellow, num3), (int)((12f + 10f * num3) / (float)num4 * 2f)), r2, Theme.S(22 + num4 * 3));
		}
		if (heroCache == null)
		{
			heroCache = new Bitmap((int)rectangleF2.Width, (int)rectangleF2.Height);
			using Graphics graphics = Graphics.FromImage(heroCache);
			using GraphicsPath clip = Theme.Round(new RectangleF(0f, 0f, rectangleF2.Width, rectangleF2.Height), Theme.S(22f));
			Theme.Hq(graphics);
			graphics.SetClip(clip);
			graphics.DrawImage(hero, 0f, 0f, rectangleF2.Width, rectangleF2.Height);
		}
		g.DrawImageUnscaled(heroCache, (int)rectangleF2.X, (int)rectangleF2.Y);
		using GraphicsPath path = Theme.Round(rectangleF2, Theme.S(22f));
		using Pen pen = new Pen(Theme.A(Theme.Lime, 160), Theme.S(2f));
		g.DrawPath(pen, path);
	}

	private float Key(Graphics g, string k, float x, float y)
	{
		using Font font = Theme.Bold(12f);
		RectangleF r = new RectangleF(x, y, g.MeasureString(k, font).Width + (float)Theme.S(16f), Theme.S(28f));
		Theme.FillRound(g, Theme.CardHover, r, Theme.S(7f));
		Theme.FillRound(g, Theme.Card, new RectangleF(r.X, r.Y, r.Width, r.Height - (float)Theme.S(3f)), Theme.S(7f));
		Theme.StrokeRound(g, Theme.Border, r, Theme.S(7f));
		Theme.Text(g, k, font, Theme.Fg, new RectangleF(r.X, r.Y, r.Width, r.Height - (float)Theme.S(3f)), StringAlignment.Center);
		return r.Right + (float)Theme.S(8f);
	}

	private float Hint(Graphics g, string t, float x, float y)
	{
		using Font font = Theme.Reg(13f);
		SizeF sizeF = g.MeasureString(t, font);
		Theme.Text(g, t, font, Theme.Muted, new RectangleF(x, y, sizeF.Width + 4f, Theme.S(28f)));
		return x + sizeF.Width;
	}

	public void RefreshAccount()
	{
		bool flag = F.Accounts.Current != null;
		chip.Visible = flag;
		Nick.Visible = !flag;
		nickL.Text = (flag ? "АККАУНТ" : "НИК В ИГРЕ (ОФЛАЙН)");
		nickL.Invalidate();
		chip.Invalidate();
	}

	public void SyncNick()
	{
		if (Nick.Text != F.Cfg.Nick)
		{
			Nick.Text = F.Cfg.Nick;
		}
	}

	public void SetStatus(string s, bool error)
	{
		status.Text = s;
		status.Color = (error ? Theme.Red : Theme.Muted);
		status.Invalidate();
	}

	public void SetBusy(bool b)
	{
		Nick.Enabled = !b;
		Profile.Enabled = !b;
		chip.Enabled = !b;
		XButton xButton = play;
		string text;
		if (b)
		{
			text = "ОТМЕНА";
		}
		else
		{
			text = (F.GameRunning ? "В ИГРЕ" : "ИГРАТЬ");
		}
		xButton.Text = text;
		play.Sub = (b ? "идёт загрузка..." : "1.21.11 • Fabric");
		if (!b)
		{
			play.Progress = -1f;
			bar.Value = -1f;
			UpdateRamHint();
		}
		play.Invalidate();
	}

	public void SetRunning(bool r)
	{
		play.Text = (r ? "В ИГРЕ" : "ИГРАТЬ");
		play.Sub = (r ? "открыть консоль" : "1.21.11 • Fabric");
		play.Invalidate();
	}

	public void SetProgress(double p, string text, double bytesPerSec)
	{
		play.Progress = (float)p;
		play.Invalidate();
		bar.Value = (float)p;
		bar.Invalidate();
		if (!string.IsNullOrEmpty(text))
		{
			SetStatus(text, error: false);
		}
		speedL.Color = Theme.Green;
		speedL.Text = (int)(p * 100.0) + "%" + ((bytesPerSec > 1024.0) ? ("  •  " + (bytesPerSec / 1048576.0).ToString("0.0") + " МБ/с") : "");
	}
}
