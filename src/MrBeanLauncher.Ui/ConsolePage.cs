using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public class ConsolePage : Page
{
	public enum Kind
	{
		Game,
		Launcher,
		Error
	}

	private readonly RichTextBox box;

	private readonly ConcurrentQueue<Tuple<string, Kind>> queue = new ConcurrentQueue<Tuple<string, Kind>>();

	private readonly Timer flush;

	private readonly string logFile;

	public ConsolePage(MainForm f)
		: base(f)
	{
		logFile = Path.Combine(f.Root, "launcher.log");
		XCard xCard = Add(new XCard(), 36, 84, 964, 488);
		box = new RichTextBox
		{
			BorderStyle = BorderStyle.None,
			BackColor = Theme.Card,
			ForeColor = Theme.Fg,
			ReadOnly = true,
			Font = Theme.Mono(12.5f),
			DetectUrls = false,
			WordWrap = false,
			ScrollBars = RichTextBoxScrollBars.Both,
			Bounds = new Rectangle(Theme.S(16f), Theme.S(14f), Theme.S(932f), Theme.S(460f))
		};
		xCard.Controls.Add(box);
		Add(new XButton
		{
			Text = "Копировать",
			Icon = "\ue8c8",
			FontPx = 12.5f
		}, 520, 18, 130, 40).Click += (object s, EventArgs e) =>
		{
			try
			{
				if (box.TextLength > 0)
				{
					Clipboard.SetText(box.Text);
				}
			}
			catch
			{
			}
		};
		Add(new XButton
		{
			Text = "Очистить",
			Icon = "\ue74d",
			FontPx = 12.5f
		}, 660, 18, 110, 40).Click += (object s, EventArgs e) =>
		{
			box.Clear();
		};
		Add(new XButton
		{
			Text = "latest.log",
			Icon = "\ue8a5",
			FontPx = 12.5f
		}, 780, 18, 110, 40).Click += (object s, EventArgs e) =>
		{
			string text = Path.Combine(F.Inst.GameDir, "logs", "latest.log");
			try
			{
				if (File.Exists(text))
				{
					Process.Start("notepad.exe", "\"" + text + "\"");
				}
			}
			catch
			{
			}
		};
		Add(new XButton
		{
			Style = BtnStyle.Danger,
			Text = "Стоп",
			Icon = "\ue71a",
			FontPx = 12.5f
		}, 900, 18, 100, 40).Click += (object s, EventArgs e) =>
		{
			if (F.GameRunning && MessageBox.Show(this, "Закрыть игру принудительно?", "MrBean Launcher", MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				F.KillGame();
			}
		};
		Append("[Лаунчер] MrBean Launcher 2.0 готов. Здесь будет лог игры в реальном времени.", Kind.Launcher);
		flush = new Timer
		{
			Interval = 100
		};
		flush.Tick += (object s, EventArgs e) =>
		{
			Flush();
		};
		flush.Start();
	}

	public void Append(string line, Kind k)
	{
		queue.Enqueue(Tuple.Create(line, k));
		if (k != Kind.Game)
		{
			try
			{
				File.AppendAllText(logFile, DateTime.Now.ToString("[HH:mm:ss] ") + line + Environment.NewLine);
			}
			catch
			{
			}
		}
	}

	private void Flush()
	{
		if (queue.IsEmpty || box.IsDisposed)
		{
			return;
		}
		int i = 0;
		box.SuspendLayout();
		for (; i < 400; i++)
		{
			if (!queue.TryDequeue(out var result))
			{
				break;
			}
			Color fg = Theme.Fg;
			string item = result.Item1;
			if (result.Item2 != Kind.Launcher)
			{
				if (result.Item2 != Kind.Error && !item.Contains("/ERROR]") && !item.Contains("Exception"))
				{
					if (item.Contains("/WARN]"))
					{
						fg = Theme.Yellow;
					}
					else
					{
						fg = ((!item.Contains("MrBean")) ? Color.FromArgb(190, 196, 212) : Theme.Lime);
					}
				}
				else
				{
					fg = Theme.Red;
				}
			}
			else
			{
				fg = Theme.Green;
			}
			box.SelectionStart = box.TextLength;
			box.SelectionLength = 0;
			box.SelectionColor = fg;
			box.AppendText(item + "\n");
		}
		if (box.TextLength > 2000000)
		{
			box.Select(0, box.TextLength - 1500000);
			box.SelectedText = "";
		}
		box.SelectionStart = box.TextLength;
		box.ScrollToCaret();
		box.ResumeLayout();
	}

	protected override void Paint2(Graphics g)
	{
		Header(g, "Консоль", "Лог игры и лаунчера в реальном времени");
	}
}
