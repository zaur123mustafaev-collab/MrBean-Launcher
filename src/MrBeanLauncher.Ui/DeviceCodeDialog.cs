using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MrBeanLauncher.Core;

namespace MrBeanLauncher.Ui;

public class DeviceCodeDialog : Form
{
	private string code = "";

	private string url = "https://www.microsoft.com/link";

	private string state = "Подключение к Microsoft...";

	private bool error;

	private bool done;

	private readonly XButton open;

	private readonly XButton cancel;

	private readonly Timer pulse = new Timer
	{
		Interval = 40
	};

	private float t;

	public event Action Cancelled = () =>
	{
	};

	public DeviceCodeDialog()
	{
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterParent;
		ShowInTaskbar = false;
		BackColor = Theme.Bg;
		Size = new Size(Theme.S(480f), Theme.S(380f));
		DoubleBuffered = true;
		Text = "Вход через Microsoft";
		open = new XButton
		{
			Style = BtnStyle.Primary,
			Text = "Скопировать код и открыть сайт",
			Icon = "\ue774",
			FontPx = 14f,
			Radius = 12f,
			Bounds = new Rectangle(Theme.S(40f), Theme.S(250f), Theme.S(400f), Theme.S(46f)),
			Enabled = false
		};
		open.Click += (object s, EventArgs e) =>
		{
			OpenSite();
		};
		Controls.Add(open);
		cancel = new XButton
		{
			Style = BtnStyle.Ghost,
			Text = "Отмена",
			FontPx = 13f,
			Radius = 10f,
			Bounds = new Rectangle(Theme.S(170f), Theme.S(310f), Theme.S(140f), Theme.S(38f))
		};
		cancel.Click += (object s, EventArgs e) =>
		{
			if (!done)
			{
				Cancelled();
			}
			Close();
		};
		Controls.Add(cancel);
		pulse.Tick += (object s, EventArgs e) =>
		{
			t += 0.08f;
			Invalidate(new Rectangle(0, Theme.S(205f), Width, Theme.S(40f)));
		};
		pulse.Start();
		Shown += (object s, EventArgs e) =>
		{
			Native.RoundCorners(Handle);
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			pulse.Stop();
		};
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.Button == MouseButtons.Left)
		{
			Native.DragWindow(Handle);
		}
	}

	public void ShowCode(MicrosoftAuth.DeviceCode dc)
	{
		code = dc.UserCode;
		url = dc.VerificationUri;
		state = "Ожидание входа на сайте Microsoft...";
		open.Enabled = true;
		Invalidate();
		OpenSite();
	}

	public void SetState(string s, bool err = false)
	{
		state = s;
		error = err;
		Invalidate();
	}

	public void Success(string name)
	{
		done = true;
		error = false;
		state = "Вы успешно вошли в MrBean Visuals! (" + name + ")";
		open.Visible = false;
		cancel.Text = "Готово";
		Invalidate();
		Timer close = new Timer
		{
			Interval = 1800
		};
		close.Tick += (object s, EventArgs e) =>
		{
			close.Stop();
			if (!IsDisposed)
			{
				Close();
			}
		};
		close.Start();
	}

	private void OpenSite()
	{
		try
		{
			Clipboard.SetText(code);
		}
		catch
		{
		}
		string fileName = url + (url.Contains("?") ? "&" : "?") + "otc=" + Uri.EscapeDataString(code);
		try
		{
			Process.Start(new ProcessStartInfo(fileName)
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Theme.Hq(graphics);
		Theme.StrokeRound(graphics, Theme.Border, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.Sf(14f));
		using (Font f = Theme.Black(22f))
		{
			Theme.Text(graphics, "Вход через Microsoft", f, Theme.Fg, new RectangleF(0f, Theme.Sf(28f), Width, Theme.Sf(30f)), StringAlignment.Center);
		}
		using (Font f2 = Theme.Reg(13f))
		{
			Theme.Text(graphics, "Введите код на сайте " + url.Replace("https://", "").Replace("www.", ""), f2, Theme.Muted, new RectangleF(0f, Theme.Sf(66f), Width, Theme.Sf(20f)), StringAlignment.Center);
		}
		RectangleF r = new RectangleF(Theme.Sf(60f), Theme.Sf(100f), (float)Width - Theme.Sf(120f), Theme.Sf(84f));
		Theme.FillRound(graphics, Theme.Card, r, Theme.Sf(16f));
		Theme.StrokeRound(graphics, done ? Theme.Green : Theme.Border, r, Theme.Sf(16f), (!done) ? 1 : 2);
		if (code.Length > 0)
		{
			using Font f3 = Theme.Mono(40f);
			Theme.Text(graphics, code, f3, done ? Theme.Green : Theme.Lime, r, StringAlignment.Center);
		}
		else
		{
			using Font f4 = Theme.Reg(14f);
			Theme.Text(graphics, "Получаю код...", f4, Theme.Dim, r, StringAlignment.Center);
		}
		if (code.Length > 0 && !done)
		{
			using Font f5 = Theme.Reg(11f);
			Theme.Text(graphics, "код уже скопирован — просто вставьте (Ctrl+V)", f5, Theme.Dim, new RectangleF(0f, Theme.Sf(188f), Width, Theme.Sf(16f)), StringAlignment.Center);
		}
		Color c;
		if (error)
		{
			c = Theme.Red;
		}
		else
		{
			c = (done ? Theme.Green : Theme.Muted);
		}
		if (!error && !done)
		{
			int a = (int)(140.0 + 115.0 * Math.Sin(t * 3f));
			using SolidBrush brush = new SolidBrush(Theme.A(Theme.Lime, a));
			graphics.FillEllipse(brush, Theme.Sf(40f), Theme.Sf(219f), Theme.Sf(8f), Theme.Sf(8f));
		}
		using Font f6 = Theme.Bold(13f);
		Theme.Text(graphics, state, f6, c, new RectangleF(Theme.Sf(54f), Theme.Sf(210f), (float)Width - Theme.Sf(94f), Theme.Sf(26f)), StringAlignment.Center);
	}
}
