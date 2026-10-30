using System;
using System.Drawing;
using System.IO;
using MrBeanLauncher.Core;

namespace MrBeanLauncher.Ui;

public class SettingsPage : Page
{
	public SettingsPage(MainForm f)
		: base(f)
	{
		Settings c = f.Cfg;
		XCard xCard = Add(new XCard
		{
			Title = "Оперативная память",
			Icon = "\ue964",
			Hint = "В ПК " + Math.Round((double)f.TotalRam / 1024.0) + " ГБ  •  для игры рекомендуем 4–6 ГБ"
		}, 36, 84, 470, 128);
		int num = Math.Max(2, Math.Min(32, (f.TotalRam - 1536) / 1024));
		XSlider ram = new XSlider
		{
			Min = 1,
			Max = num,
			Value = Math.Max(1, Math.Min(num, c.RamMb / 1024)),
			Format = (int v) => v + " ГБ"
		};
		ram.Bounds = new Rectangle(Theme.S(12f), Theme.S(62f), Theme.S(446f), Theme.S(44f));
		ram.Changed += (object s, EventArgs e) =>
		{
			c.RamMb = ram.Value * 1024;
			f.SaveCfg();
			f.Home.UpdateRamHint();
		};
		xCard.Controls.Add(ram);
		XCard xCard2 = Add(new XCard
		{
			Title = "Окно игры",
			Icon = "\ue7f4",
			Hint = "Размер окна при запуске"
		}, 36, 228, 470, 132);
		XInput wI = new XInput
		{
			Placeholder = "1280",
			Bounds = new Rectangle(Theme.S(18f), Theme.S(64f), Theme.S(110f), Theme.S(44f))
		};
		wI.Text = c.Width.ToString();
		XInput hI = new XInput
		{
			Placeholder = "720",
			Bounds = new Rectangle(Theme.S(160f), Theme.S(64f), Theme.S(110f), Theme.S(44f))
		};
		hI.Text = c.Height.ToString();
		xCard2.Controls.Add(wI);
		xCard2.Controls.Add(hI);
		xCard2.Controls.Add(new XLabel
		{
			Text = "×",
			Px = 18f,
			Color = Theme.Muted,
			Align = StringAlignment.Center,
			Bounds = new Rectangle(Theme.S(128f), Theme.S(64f), Theme.S(32f), Theme.S(44f))
		});
		EventHandler value = (object s, EventArgs e) =>
		{
			if (int.TryParse(wI.Text, out var result) && result >= 640 && result <= 7680)
			{
				c.Width = result;
			}
			if (int.TryParse(hI.Text, out var result2) && result2 >= 480 && result2 <= 4320)
			{
				c.Height = result2;
			}
			f.SaveCfg();
		};
		wI.Box.TextChanged += value;
		hI.Box.TextChanged += value;
		XToggle fs = new XToggle
		{
			Checked = c.Fullscreen,
			Location = new Point(Theme.S(408f), Theme.S(74f))
		};
		fs.Changed += (object s, EventArgs e) =>
		{
			c.Fullscreen = fs.Checked;
			f.SaveCfg();
		};
		xCard2.Controls.Add(fs);
		xCard2.Controls.Add(new XLabel
		{
			Text = "Полный экран",
			Px = 13f,
			Color = Theme.Fg,
			Align = StringAlignment.Far,
			Bounds = new Rectangle(Theme.S(286f), Theme.S(64f), Theme.S(114f), Theme.S(44f))
		});
		XCard xCard3 = Add(new XCard
		{
			Title = "Java и производительность",
			Icon = "\ue943",
			Hint = "Сборщик мусора и свои аргументы JVM"
		}, 36, 376, 470, 196);
		XSegmented gc = new XSegmented
		{
			Items = new string[3] { "G1 (реком.)", "ZGC", "Parallel" },
			Bounds = new Rectangle(Theme.S(18f), Theme.S(62f), Theme.S(434f), Theme.S(40f))
		};
		gc.Selected = (c.GcPreset.StartsWith("ZGC") ? 1 : (c.GcPreset.StartsWith("Parallel") ? 2 : 0));
		gc.Changed += (object s, EventArgs e) =>
		{
			c.GcPreset = (new string[3] { "G1 (рекомендуется)", "ZGC", "Parallel" })[gc.Selected];
			f.SaveCfg();
		};
		xCard3.Controls.Add(gc);
		XInput args = new XInput
		{
			Icon = "\ue943",
			Placeholder = "Доп. аргументы, напр. -XX:+UseStringDeduplication",
			Bounds = new Rectangle(Theme.S(18f), Theme.S(116f), Theme.S(434f), Theme.S(44f))
		};
		args.Text = c.JavaArgs;
		args.Box.Font = Theme.Mono(13f);
		args.Box.TextChanged += (object s, EventArgs e) =>
		{
			c.JavaArgs = args.Text;
			f.SaveCfg();
		};
		xCard3.Controls.Add(args);
		xCard3.Controls.Add(new XLabel
		{
			Text = "Java 21 скачивается автоматически (официальная от Mojang)",
			Px = 11.5f,
			Color = Theme.Dim,
			Bounds = new Rectangle(Theme.S(20f), Theme.S(166f), Theme.S(430f), Theme.S(18f))
		});
		XCard xCard4 = Add(new XCard
		{
			Title = "Лаунчер",
			Icon = "\ue713",
			Hint = "Поведение и загрузка"
		}, 530, 84, 470, 268);
		AddToggleRow(xCard4, 56, "Сворачивать в трей во время игры", c.HideOnLaunch, (bool v) =>
		{
			c.HideOnLaunch = v;
		});
		AddToggleRow(xCard4, 94, "Открывать консоль при запуске", c.ShowConsoleOnLaunch, (bool v) =>
		{
			c.ShowConsoleOnLaunch = v;
		});
		AddToggleRow(xCard4, 132, "Русский язык при первом запуске", c.RussianLang, (bool v) =>
		{
			c.RussianLang = v;
		});
		AddToggleRow(xCard4, 170, "Моды оптимизации (Sodium, Lithium…) с Modrinth", c.OptMods, (bool v) =>
		{
			c.OptMods = v;
			if (!v)
			{
				int num2 = new OptMods(f.Inst.ModsDir, f.Inst.CacheDir).RemoveOurs();
				f.Console.Append("[Лаунчер] Моды оптимизации выключены, убрано файлов: " + num2, ConsolePage.Kind.Launcher);
			}
		});
		xCard4.Controls.Add(new XLabel
		{
			Text = "Потоков загрузки",
			Px = 13f,
			Color = Theme.Fg,
			Bounds = new Rectangle(Theme.S(20f), Theme.S(216f), Theme.S(150f), Theme.S(40f))
		});
		XSlider par = new XSlider
		{
			Min = 4,
			Max = 48,
			Value = c.Parallel,
			Format = (int v) => v.ToString(),
			Bounds = new Rectangle(Theme.S(170f), Theme.S(210f), Theme.S(286f), Theme.S(44f))
		};
		par.Changed += (object s, EventArgs e) =>
		{
			c.Parallel = par.Value;
			f.SaveCfg();
		};
		xCard4.Controls.Add(par);
		XCard xCard5 = Add(new XCard
		{
			Title = "Файлы и папки",
			Icon = "\ue838",
			Hint = "Всё лежит в " + f.Root
		}, 530, 366, 470, 206);
		string g = f.Inst.GameDir;
		AddBtn(xCard5, 0, 0, "Папка игры", "\ue838", () =>
		{
			MainForm.OpenFolder(g);
		});
		AddBtn(xCard5, 1, 0, "Моды", "\ue74c", () =>
		{
			MainForm.OpenFolder(f.Inst.ModsDir);
		});
		AddBtn(xCard5, 2, 0, "Скриншоты", "\ue722", () =>
		{
			MainForm.OpenFolder(Path.Combine(g, "screenshots"));
		});
		AddBtn(xCard5, 0, 1, "Ресурспаки", "\ueb9f", () =>
		{
			MainForm.OpenFolder(Path.Combine(g, "resourcepacks"));
		});
		AddBtn(xCard5, 1, 1, "Миры", "\ue774", () =>
		{
			MainForm.OpenFolder(Path.Combine(g, "saves"));
		});
		AddBtn(xCard5, 2, 1, "Логи", "\ue8a5", () =>
		{
			MainForm.OpenFolder(Path.Combine(g, "logs"));
		});
		XButton xButton = new XButton
		{
			Text = "Проверить файлы",
			Icon = "\ue72c",
			FontPx = 12.5f,
			Bounds = new Rectangle(Theme.S(18f), Theme.S(152f), Theme.S(210f), Theme.S(38f))
		};
		xButton.Click += async (object s, EventArgs e) =>
		{
			f.ShowPage(0);
			await f.RunInstall(launch: false);
		};
		xCard5.Controls.Add(xButton);
		XButton xButton2 = new XButton
		{
			Text = "Переустановить Java",
			Icon = "\ue72c",
			FontPx = 12.5f,
			Bounds = new Rectangle(Theme.S(240f), Theme.S(152f), Theme.S(212f), Theme.S(38f))
		};
		xButton2.Click += async (object s, EventArgs e) =>
		{
			f.ShowPage(0);
			await f.RunInstall(launch: false, forceJava: true);
		};
		xCard5.Controls.Add(xButton2);
	}

	private void AddToggleRow(XCard card, int y, string text, bool value, Action<bool> set)
	{
		card.Controls.Add(new XLabel
		{
			Text = text,
			Px = 13f,
			Color = Theme.Fg,
			Bounds = new Rectangle(Theme.S(20f), Theme.S(y), Theme.S(380f), Theme.S(36f))
		});
		XToggle t = new XToggle
		{
			Checked = value,
			Location = new Point(Theme.S(408f), Theme.S(y + 6))
		};
		t.Changed += (object s, EventArgs e) =>
		{
			set(t.Checked);
			F.SaveCfg();
		};
		card.Controls.Add(t);
	}

	private void AddBtn(XCard card, int col, int row, string text, string icon, Action a)
	{
		XButton xButton = new XButton
		{
			Text = text,
			Icon = icon,
			FontPx = 12.5f,
			Bounds = new Rectangle(Theme.S(18 + col * 146), Theme.S(56 + row * 46), Theme.S(138f), Theme.S(38f))
		};
		xButton.Click += (object s, EventArgs e) =>
		{
			a();
		};
		card.Controls.Add(xButton);
	}

	protected override void Paint2(Graphics g)
	{
		Header(g, "Настройки", "Всё сохраняется автоматически");
	}
}
