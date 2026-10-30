using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MrBeanLauncher.Ui;

public class AboutPage : Page
{
	private readonly Image hero;

	private Bitmap cache;

	public AboutPage(MainForm f, Image hero)
		: base(f)
	{
		this.hero = hero;
		Add(new XButton
		{
			Text = "Папка лаунчера",
			Icon = "\ue838",
			FontPx = 13f
		}, 336, 512, 200, 44).Click += (object s, EventArgs e) =>
		{
			MainForm.OpenFolder(f.Root);
		};
	}

	protected override void Paint2(Graphics g)
	{
		Header(g, "О моде", "MrBean Visuals 1.0 для Minecraft 1.21.11 (Fabric)");
		RectangleF rectangleF = new RectangleF(Theme.S(36f), Theme.S(88f), Theme.S(270f), Theme.S(270f));
		if (hero != null)
		{
			if (cache == null)
			{
				cache = new Bitmap((int)rectangleF.Width, (int)rectangleF.Height);
				using Graphics graphics = Graphics.FromImage(cache);
				using GraphicsPath clip = Theme.Round(new RectangleF(0f, 0f, rectangleF.Width, rectangleF.Height), Theme.S(20f));
				Theme.Hq(graphics);
				graphics.SetClip(clip);
				graphics.DrawImage(hero, 0f, 0f, rectangleF.Width, rectangleF.Height);
			}
			g.DrawImageUnscaled(cache, (int)rectangleF.X, (int)rectangleF.Y);
		}
		string[,] array = new string[4, 2]
		{
			{ "Правый Shift", "открыть меню мода (ЛКМ — вкл/выкл, ПКМ — настройки)" },
			{ "C", "зум, колёсико — сила приближения" },
			{ "HUD редактор", "кнопка в меню — двигай элементы мышкой" },
			{ "Макросы", "Настройки → Управление → MrBean Visuals" }
		};
		float num = Theme.S(92f);
		using (Font f = Theme.Bold(13f))
		{
			using Font f2 = Theme.Reg(13f);
			for (int i = 0; i < array.GetLength(0); i++)
			{
				RectangleF r = new RectangleF(Theme.S(336f), num, Theme.S(130f), Theme.S(30f));
				Theme.FillRound(g, Theme.CardHover, r, Theme.S(8f));
				Theme.StrokeRound(g, Theme.Border, r, Theme.S(8f));
				Theme.Text(g, array[i, 0], f, Theme.Lime, r, StringAlignment.Center);
				Theme.Text(g, array[i, 1], f2, Theme.Fg, new RectangleF(Theme.S(480f), num, Theme.S(520f), Theme.S(30f)));
				num += (float)Theme.S(42f);
			}
		}
		using Font f3 = Theme.Reg(13f);
		Theme.TextWrap(g, "Что внутри: ватермарка, список модулей, клавиши с CPS, броня, эффекты, координаты, таргет-HUD, счётчик тотемов и перлов, комбо, дистанция удара, кулдаун, предупреждения о HP и броне, свой прицел, хитмаркер, авто-спринт, уровень света, день мира, точка смерти, сытость, зум, яркость, без тряски, без покачивания, меньше огня, без дождя, время суток, скрытие скорборда, стабильный FOV, макросы и 8 тем оформления.\n\nНик в лаунчере — офлайн: одиночная игра и серверы без лицензии. Читов нет — только визуал и удобство.", f3, Theme.Muted, new RectangleF(Theme.S(336f), Theme.S(270f), Theme.S(660f), Theme.S(230f)));
	}
}
