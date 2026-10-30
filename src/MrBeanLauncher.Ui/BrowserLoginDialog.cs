using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MrBeanLauncher.Core;

namespace MrBeanLauncher.Ui;

/// <summary>
/// Окно, которое показывает лаунчер, пока игрок входит в браузере (OAuth + PKCE).
/// Пароль вводится только на сайте Microsoft — лаунчер его не видит.
/// </summary>
public class BrowserLoginDialog : Form
{
	private string state = "Открываю браузер Microsoft...";

	private bool error;

	private string url = "";

	private readonly string redirectHint;

	private readonly XButton reopen;

	private readonly XButton byCode;

	private readonly XButton cancel;

	private readonly Timer pulse = new Timer
	{
		Interval = 40
	};

	private float t;

	/// <summary>Игрок нажал «Отмена» или закрыл окно.</summary>
	public event Action Cancelled = () =>
	{
	};

	/// <summary>Игрок попросил запасной вход по коду.</summary>
	public event Action CodeRequested = () =>
	{
	};

	public bool WantsCode { get; private set; }

	public BrowserLoginDialog(string redirectUri)
	{
		redirectHint = redirectUri;
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterParent;
		ShowInTaskbar = false;
		BackColor = Theme.Bg;
		Size = new Size(Theme.S(480f), Theme.S(392f));
		DoubleBuffered = true;
		Text = "Вход через Microsoft";
		reopen = new XButton
		{
			Style = BtnStyle.Primary,
			Text = "Открыть браузер снова",
			Icon = "\ue774",
			FontPx = 14f,
			Radius = 12f,
			Bounds = new Rectangle(Theme.S(40f), Theme.S(256f), Theme.S(400f), Theme.S(46f)),
			Enabled = false
		};
		reopen.Click += (object s, EventArgs e) => OpenSite();
		Controls.Add(reopen);
		byCode = new XButton
		{
			Style = BtnStyle.Secondary,
			Text = "Войти по коду",
			FontPx = 12.5f,
			Radius = 10f,
			Bounds = new Rectangle(Theme.S(40f), Theme.S(314f), Theme.S(196f), Theme.S(40f))
		};
		byCode.Click += (object s, EventArgs e) =>
		{
			WantsCode = true;
			CodeRequested();
			Close();
		};
		Controls.Add(byCode);
		cancel = new XButton
		{
			Style = BtnStyle.Ghost,
			Text = "Отмена",
			FontPx = 12.5f,
			Radius = 10f,
			Bounds = new Rectangle(Theme.S(244f), Theme.S(314f), Theme.S(196f), Theme.S(40f))
		};
		cancel.Click += (object s, EventArgs e) =>
		{
			Cancelled();
			Close();
		};
		Controls.Add(cancel);
		pulse.Tick += (object s, EventArgs e) =>
		{
			t += 0.08f;
			Invalidate(new Rectangle(0, Theme.S(150f), Width, Theme.S(90f)));
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

	/// <summary>Лаунчер сообщил адрес страницы входа — теперь можно открыть её заново.</summary>
	public void SetTarget(string u)
	{
		url = u;
		reopen.Enabled = true;
		Invalidate();
	}

	public void SetState(string s, bool err = false)
	{
		state = s;
		error = err;
		Invalidate();
	}

	private void OpenSite()
	{
		if (string.IsNullOrEmpty(url))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(url)
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
		using (Font f2 = Theme.Reg(12.5f))
		{
			Theme.TextWrap(graphics, "Открыли сайт Microsoft в браузере. Введите там пароль и подтвердите вход — лаунчер пароль не видит и не хранит.",
				f2, Theme.Muted, new RectangleF(Theme.Sf(52f), Theme.Sf(64f), (float)Width - Theme.Sf(104f), Theme.Sf(56f)));
		}
		RectangleF r = new RectangleF(Theme.Sf(40f), Theme.Sf(134f), (float)Width - Theme.Sf(80f), Theme.Sf(88f));
		Theme.FillRound(graphics, Theme.Card, r, Theme.Sf(16f));
		Theme.StrokeRound(graphics, error ? Theme.Red : Theme.Border, r, Theme.Sf(16f), 1f);
		using (Font icon = Theme.Icon(26f))
		{
			Theme.Text(graphics, "\ue774", icon, error ? Theme.Red : Theme.Lime,
				new RectangleF(r.X + Theme.Sf(18f), r.Y, Theme.Sf(40f), r.Height), StringAlignment.Center);
		}
		int alpha = (int)(140.0 + 115.0 * Math.Sin(t * 3f));
		using (Font f3 = Theme.Bold(13.5f))
		{
			Theme.TextWrap(graphics, state, f3, error ? Theme.Red : Theme.Fg,
				new RectangleF(r.X + Theme.Sf(66f), r.Y + Theme.Sf(14f), r.Width - Theme.Sf(90f), Theme.Sf(46f)));
		}
		if (!error)
		{
			using SolidBrush b = new SolidBrush(Theme.A(Theme.Lime, alpha));
			graphics.FillEllipse(b, r.X + Theme.Sf(66f), r.Bottom - Theme.Sf(28f), Theme.Sf(8f), Theme.Sf(8f));
			using Font f4 = Theme.Reg(11.5f);
			Theme.Text(graphics, "Ждём ответ от браузера...", f4, Theme.Muted,
				new RectangleF(r.X + Theme.Sf(82f), r.Bottom - Theme.Sf(34f), r.Width - Theme.Sf(90f), Theme.Sf(20f)));
		}
		else
		{
			using Font f5 = Theme.Reg(11.5f);
			Theme.Text(graphics, "Можно попробовать снова или войти по коду", f5, Theme.Muted,
				new RectangleF(r.X + Theme.Sf(66f), r.Bottom - Theme.Sf(34f), r.Width - Theme.Sf(90f), Theme.Sf(20f)));
		}
		using Font f6 = Theme.Reg(11f);
		Theme.Text(graphics, "Если браузер спросит адрес возврата — он такой: " + redirectHint, f6, Theme.Dim,
			new RectangleF(Theme.Sf(40f), Theme.Sf(228f), (float)Width - Theme.Sf(80f), Theme.Sf(22f)), StringAlignment.Center);
	}
}
