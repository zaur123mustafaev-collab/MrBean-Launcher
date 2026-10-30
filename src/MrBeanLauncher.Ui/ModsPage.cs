using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class ModsPage : Page
{
	private class ModList : XControl
	{
		private class Mod
		{
			public string Path;

			public string Name;

			public long Size;

			public bool On;

			public bool Builtin;
		}

		private readonly ModsPage page;

		private List<Mod> mods = new List<Mod>();

		private float scroll;

		private float scrollT;

		private int hoverRow = -1;

		private int hoverPart;

		private const int ROW = 66;

		private float MaxScroll => Math.Max(0, mods.Count * Theme.S(66f) - Height);

		public ModList(ModsPage p)
		{
			page = p;
			Cursor = Cursors.Default;
		}

		public void Load(string dir)
		{
			mods.Clear();
			if (Directory.Exists(dir))
			{
				string[] files = Directory.GetFiles(dir);
				foreach (string text in files)
				{
					bool flag = text.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
					bool flag2 = text.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
					if (flag || flag2)
					{
						string fileName = Path.GetFileName(text);
						fileName = fileName.Substring(0, fileName.Length - (flag ? 4 : 13));
						mods.Add(new Mod
						{
							Path = text,
							Name = fileName,
							Size = new FileInfo(text).Length,
							On = flag,
							Builtin = (fileName.StartsWith("MrBeanVisuals") || fileName.StartsWith("fabric-api"))
						});
					}
				}
			}
			mods = (from m in mods
				orderby m.Builtin descending, m.Name
				select m).ToList();
			Invalidate();
		}

		public string Summary()
		{
			if (mods.Count != 0)
			{
				return $"Всего: {mods.Count}  •  включено: {Enumerable.Count(mods, (Mod m) => m.On)}  •  {(double)mods.Sum((Mod m) => m.Size) / 1048576.0:0.0} МБ";
			}
			return "Модов пока нет — они появятся после первого запуска";
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			scrollT = Math.Max(0f, Math.Min(MaxScroll, scrollT - (float)e.Delta * 0.6f));
		}

		public override void Step()
		{
			base.Step();
			float num = scroll + (scrollT - scroll) * 0.25f;
			if (Math.Abs(num - scrollT) < 0.5f)
			{
				num = scrollT;
			}
			if (num != scroll)
			{
				scroll = num;
				Invalidate();
			}
		}

		private void Hit(int x, int y, out int row, out int part)
		{
			row = (int)(((float)y + scroll) / (float)Theme.S(66f));
			part = 0;
			if (row < 0 || row >= mods.Count)
			{
				row = -1;
			}
			else if (x > Width - Theme.S(130f) && x < Width - Theme.S(70f))
			{
				part = 1;
			}
			else if (x >= Width - Theme.S(62f) && x < Width - Theme.S(14f))
			{
				part = 2;
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			Hit(e.X, e.Y, out var row, out var part);
			if (row != hoverRow || part != hoverPart)
			{
				hoverRow = row;
				hoverPart = part;
				Cursor = ((part > 0) ? Cursors.Hand : Cursors.Default);
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			hoverRow = -1;
			Invalidate();
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			Hit(e.X, e.Y, out var row, out var part);
			if (row < 0)
			{
				return;
			}
			Mod mod = mods[row];
			try
			{
				switch (part)
				{
				case 1:
				{
					string destFileName = (mod.On ? (mod.Path + ".disabled") : mod.Path.Substring(0, mod.Path.Length - ".disabled".Length));
					File.Move(mod.Path, destFileName);
					if (mod.Name.StartsWith("MrBeanVisuals"))
					{
						page.F.Cfg.WithVisuals = !mod.On;
						page.F.SaveCfg();
						page.F.Home.Profile.Selected = (mod.On ? 1 : 0);
						page.F.Home.Profile.Invalidate();
					}
					page.Reload();
					break;
				}
				case 2:
					if (MessageBox.Show(page, "Удалить мод «" + mod.Name + "»?", "MrBean Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					{
						File.Delete(mod.Path);
						page.Reload();
					}
					break;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(page, "Не получилось: " + ex.Message + "\nЗакрой игру и попробуй снова.", "MrBean Launcher");
			}
		}

		protected override void Draw(Graphics g)
		{
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
			Theme.FillRound(g, Theme.A(Theme.Card, 225), r, Theme.Sf(16f));
			Theme.StrokeRound(g, Theme.A(Theme.Border, 200), r, Theme.Sf(16f));
			if (mods.Count == 0)
			{
				using (Font f = Theme.Icon(40f))
				{
					Theme.Text(g, "\ue74c", f, Theme.Dim, new RectangleF(0f, (float)Height / 2f - Theme.Sf(60f), Width, Theme.Sf(50f)), StringAlignment.Center);
				}
				using (Font f2 = Theme.Bold(16f))
				{
					Theme.Text(g, "Здесь появятся твои моды", f2, Theme.Muted, new RectangleF(0f, (float)Height / 2f, Width, Theme.Sf(24f)), StringAlignment.Center);
				}
				using Font f3 = Theme.Reg(13f);
				Theme.Text(g, "Нажми «Играть» один раз — лаунчер поставит Fabric API и MrBean Visuals", f3, Theme.Dim, new RectangleF(0f, (float)Height / 2f + Theme.Sf(26f), Width, Theme.Sf(20f)), StringAlignment.Center);
				return;
			}
			GraphicsState gstate = g.Save();
			using (GraphicsPath clip = Theme.Round(r, Theme.Sf(16f)))
			{
				g.SetClip(clip);
			}
			for (int i = 0; i < mods.Count; i++)
			{
				float num = (float)(i * Theme.S(66f)) - scroll;
				if (num + (float)Theme.S(66f) < 0f || num > (float)Height)
				{
					continue;
				}
				Mod mod = mods[i];
				RectangleF r2 = new RectangleF(Theme.Sf(8f), num + Theme.Sf(6f), (float)Width - Theme.Sf(16f), (float)Theme.S(66f) - Theme.Sf(8f));
				if (i == hoverRow)
				{
					Theme.FillRound(g, Theme.A(Color.White, 10), r2, Theme.Sf(12f));
				}
				RectangleF r3 = new RectangleF(r2.X + Theme.Sf(10f), r2.Y + (r2.Height - Theme.Sf(40f)) / 2f, Theme.Sf(40f), Theme.Sf(40f));
				if (mod.Name.StartsWith("MrBeanVisuals"))
				{
					using Brush b = Theme.Accent(r3, 45f);
					Theme.FillRound(g, b, r3, Theme.Sf(10f));
				}
				else
				{
					Theme.FillRound(g, Color.FromArgb(255, 36 + (mod.Name.GetHashCode() & 0x1F), 40, 56), r3, Theme.Sf(10f));
				}
				using (Font f4 = Theme.Black(18f))
				{
					Theme.Text(g, mod.Name.Substring(0, 1).ToUpper(), f4, mod.Name.StartsWith("MrBeanVisuals") ? Color.FromArgb(14, 18, 10) : Theme.Fg, r3, StringAlignment.Center);
				}
				using (Font f5 = Theme.Bold(15f))
				{
					Theme.Text(g, mod.Name, f5, mod.On ? Theme.Fg : Theme.Dim, new RectangleF(r3.Right + Theme.Sf(14f), r2.Y + Theme.Sf(10f), r2.Width - Theme.Sf(280f), Theme.Sf(22f)));
				}
				string s = (((double)mod.Size / 1024.0 < 1024.0) ? (((double)mod.Size / 1024.0).ToString("0") + " КБ") : (((double)mod.Size / 1048576.0).ToString("0.0") + " МБ")) + (mod.On ? "" : "  •  выключен");
				using (Font f6 = Theme.Reg(12f))
				{
					Theme.Text(g, s, f6, Theme.Muted, new RectangleF(r3.Right + Theme.Sf(14f), r2.Y + Theme.Sf(32f), Theme.Sf(300f), Theme.Sf(18f)));
				}
				if (mod.Builtin)
				{
					string s2 = (mod.Name.StartsWith("MrBean") ? "ВСТРОЕН" : "НУЖЕН");
					using Font font = Theme.Bold(10f);
					SizeF sizeF = g.MeasureString(s2, font);
					RectangleF r4 = new RectangleF(r2.Right - Theme.Sf(140f) - sizeF.Width - Theme.Sf(14f), r2.Y + (r2.Height - Theme.Sf(22f)) / 2f, sizeF.Width + Theme.Sf(14f), Theme.Sf(22f));
					Theme.FillRound(g, Theme.A(Theme.Green, 30), r4, Theme.Sf(11f));
					Theme.Text(g, s2, font, Theme.Lime, r4, StringAlignment.Center);
				}
				RectangleF rectangleF = new RectangleF(r2.Right - Theme.Sf(116f), r2.Y + (r2.Height - Theme.Sf(24f)) / 2f, Theme.Sf(44f), Theme.Sf(24f));
				Theme.FillRound(g, mod.On ? Theme.Green : Color.FromArgb(46, 50, 66), rectangleF, Theme.Sf(12f));
				if (mod.On)
				{
					using LinearGradientBrush b2 = new LinearGradientBrush(rectangleF, Theme.Green, Theme.Yellow, 0f);
					Theme.FillRound(g, b2, rectangleF, Theme.Sf(12f));
				}
				float num2 = Theme.Sf(18f);
				using (SolidBrush brush = new SolidBrush(Color.White))
				{
					g.FillEllipse(brush, mod.On ? (rectangleF.Right - num2 - Theme.Sf(3f)) : (rectangleF.X + Theme.Sf(3f)), rectangleF.Y + Theme.Sf(3f), num2, num2);
				}
				RectangleF r5 = new RectangleF(r2.Right - Theme.Sf(52f), r2.Y + (r2.Height - Theme.Sf(36f)) / 2f, Theme.Sf(36f), Theme.Sf(36f));
				bool flag = i == hoverRow && hoverPart == 2;
				if (flag)
				{
					Theme.FillRound(g, Theme.A(Theme.Red, 40), r5, Theme.Sf(9f));
				}
				using Font f7 = Theme.Icon(15f);
				Theme.Text(g, "\ue74d", f7, flag ? Theme.Red : Theme.Muted, r5, StringAlignment.Center);
			}
			g.Restore(gstate);
			if (MaxScroll > 0f)
			{
				float num3 = (float)(Height * Height) / (float)(mods.Count * Theme.S(66f));
				float num4 = ((float)Height - num3) * scroll / MaxScroll;
				Theme.FillRound(g, Theme.A(Color.White, 40), new RectangleF((float)Width - Theme.Sf(6f), num4 + Theme.Sf(4f), Theme.Sf(3f), num3 - Theme.Sf(8f)), Theme.Sf(2f));
			}
		}
	}

	private readonly ModList list;

	private readonly XLabel count;

	public ModsPage(MainForm f)
		: base(f)
	{
		Add(new XButton
		{
			Style = BtnStyle.Primary,
			Text = "Добавить мод",
			Icon = "\ue710",
			FontPx = 13f,
			Radius = 10f
		}, 610, 14, 170, 42).Click += (object s, EventArgs e) =>
		{
			AddMods();
		};
		Add(new XButton
		{
			Text = "Папка",
			Icon = "\ue838",
			FontPx = 13f
		}, 790, 14, 100, 42).Click += (object s, EventArgs e) =>
		{
			MainForm.OpenFolder(F.Inst.ModsDir);
		};
		Add(new XButton
		{
			Text = "Modrinth",
			Icon = "\ue774",
			FontPx = 13f
		}, 900, 14, 100, 42).Click += (object s, EventArgs e) =>
		{
			try
			{
				Process.Start("https://modrinth.com/mods?g=categories:fabric&v=1.21.11");
			}
			catch
			{
			}
		};
		list = Add(new ModList(this), 36, 92, 964, 470);
		count = Add(new XLabel
		{
			Px = 12f,
			Color = Theme.Muted
		}, 38, 570, 900, 20);
		AllowDrop = true;
		list.AllowDrop = true;
		DragEnter += OnDragEnter;
		list.DragEnter += OnDragEnter;
		DragDrop += OnDrop;
		list.DragDrop += OnDrop;
	}

	private void OnDragEnter(object s, DragEventArgs e)
	{
		e.Effect = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
	}

	private void OnDrop(object s, DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
		{
			Import(files);
		}
	}

	private void AddMods()
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Моды Fabric (*.jar)|*.jar",
			Multiselect = true,
			Title = "Выбери моды для 1.21.11 (Fabric)"
		};
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			Import(openFileDialog.FileNames);
		}
	}

	private void Import(IEnumerable<string> files)
	{
		Directory.CreateDirectory(F.Inst.ModsDir);
		int num = 0;
		foreach (string file in files)
		{
			if (file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					File.Copy(file, Path.Combine(F.Inst.ModsDir, Path.GetFileName(file)), overwrite: true);
					num++;
				}
				catch
				{
				}
			}
		}
		Reload();
		F.Toast((num > 0) ? ("Добавлено модов: " + num) : "Нужны файлы .jar");
	}

	public void Reload()
	{
		list.Load(F.Inst.ModsDir);
		count.Text = list.Summary();
	}

	protected override void Paint2(Graphics g)
	{
		Header(g, "Моды", "Перетащи .jar сюда или нажми «Добавить». Только Fabric 1.21.11.");
	}
}
