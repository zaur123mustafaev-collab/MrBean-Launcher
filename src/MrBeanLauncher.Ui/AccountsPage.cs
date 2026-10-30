using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MrBeanLauncher.Core;

namespace MrBeanLauncher.Ui;

public class AccountsPage : Page
{
	private class AccountList : XControl
	{
		private readonly MainForm f;

		private int hoverRow = -1;

		private int hoverBtn;

		private const int ROW = 74;

		public AccountList(MainForm f)
		{
			this.f = f;
			Cursor = Cursors.Default;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			int num = (e.Y - Theme.S(46f)) / Theme.S(74f);
			int num2 = 0;
			if (num < 0 || num >= f.Accounts.Accounts.Count)
			{
				num = -1;
			}
			else
			{
				float num3 = Theme.S(46f) + num * Theme.S(74f);
				if ((float)e.Y > num3 + (float)Theme.S(40f) && (float)e.Y < num3 + (float)Theme.S(66f))
				{
					if (e.X > Width - Theme.S(250f) && e.X < Width - Theme.S(130f))
					{
						num2 = 1;
					}
					else if (e.X >= Width - Theme.S(124f) && e.X < Width - Theme.S(16f))
					{
						num2 = 2;
					}
				}
			}
			if (num != hoverRow || num2 != hoverBtn)
			{
				hoverRow = num;
				hoverBtn = num2;
				Cursor = ((num2 > 0) ? Cursors.Hand : Cursors.Default);
				Invalidate();
			}
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if (hoverRow < 0 || hoverBtn == 0)
			{
				return;
			}
			Account account = f.Accounts.Accounts[hoverRow];
			if (hoverBtn == 1)
			{
				f.Accounts.Selected = account.Uuid;
				f.Accounts.Save();
				f.AccountsChanged();
				if (account.NeedsLogin)
				{
					f.AccountsPage.SetStatus("Этому аккаунту нужен повторный вход — нажмите «Войти через Microsoft».", error: true);
				}
				else
				{
					f.AccountsPage.SetStatus("Аккаунт выбран: " + account.Name, error: false, success: true);
				}
			}
			else if (MessageBox.Show(f, "Выйти из аккаунта «" + account.Name + "»? Токены будут удалены с этого компьютера.", "MrBean Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				f.Accounts.Remove(account.Uuid);
				f.AccountsChanged();
				f.AccountsPage.SetStatus("Вы вышли из аккаунта " + account.Name, error: false);
			}
			Invalidate();
		}

		protected override void Draw(Graphics g)
		{
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
			Theme.FillRound(g, Theme.A(Theme.Card, 225), r, Theme.Sf(16f));
			Theme.StrokeRound(g, Theme.A(Theme.Border, 200), r, Theme.Sf(16f));
			using (Font font = Theme.Bold(15f))
			{
				Theme.Text(g, "Управление аккаунтами", font, Theme.Fg, new RectangleF(Theme.Sf(18f), Theme.Sf(12f), Width, Theme.Sf(24f)));
			}
			List<Account> accounts = f.Accounts.Accounts;
			if (accounts.Count == 0)
			{
				using (Font font2 = Theme.Icon(40f))
				{
					Theme.Text(g, "\ue77b", font2, Theme.Dim, new RectangleF(0f, (float)Height / 2f - Theme.Sf(60f), Width, Theme.Sf(50f)), StringAlignment.Center);
				}
				using (Font font3 = Theme.Bold(15f))
				{
					Theme.Text(g, "Аккаунтов Microsoft пока нет", font3, Theme.Muted, new RectangleF(0f, (float)Height / 2f, Width, Theme.Sf(24f)), StringAlignment.Center);
				}
				using Font font4 = Theme.Reg(12.5f);
				Theme.Text(g, "Нажмите «Войти через Microsoft» — сейчас играете с офлайн-ником", font4, Theme.Dim, new RectangleF(0f, (float)Height / 2f + Theme.Sf(26f), Width, Theme.Sf(20f)), StringAlignment.Center);
				return;
			}
			for (int i = 0; i < accounts.Count; i++)
			{
				Account account = accounts[i];
				bool flag = account.Uuid == f.Accounts.Selected;
				RectangleF r2 = new RectangleF(Theme.Sf(12f), Theme.Sf(46f) + (float)(i * Theme.S(74f)), (float)Width - Theme.Sf(24f), (float)Theme.S(74f) - Theme.Sf(6f));
				Graphics g2 = g;
				Color c;
				if (flag)
				{
					c = Theme.A(Theme.Green, 26);
				}
				else
				{
					c = ((i == hoverRow) ? Theme.A(Color.White, 10) : Theme.A(Color.White, 4));
				}
				Theme.FillRound(g2, c, r2, Theme.Sf(12f));
				if (flag)
				{
					Theme.StrokeRound(g, Theme.A(Theme.Green, 150), r2, Theme.Sf(12f));
				}
				SkinFaces.DrawHead(g, SkinFaces.Get(f, account.Uuid, account.SkinUrl), new RectangleF(r2.X + Theme.Sf(10f), r2.Y + Theme.Sf(10f), Theme.Sf(48f), Theme.Sf(48f)), account.Name);
				using (Font font5 = Theme.Bold(16f))
				{
					Theme.Text(g, account.Name, font5, Theme.Fg, new RectangleF(r2.X + Theme.Sf(70f), r2.Y + Theme.Sf(8f), Theme.Sf(200f), Theme.Sf(24f)));
				}
				string text;
				if (account.NeedsLogin)
				{
					text = "Нужен вход";
				}
				else
				{
					text = (flag ? "Активен · Microsoft ✓" : "Microsoft ✓");
				}
				using (Font font6 = Theme.Reg(12f))
				{
					Graphics g3 = g;
					string s = text;
					Font font7 = font6;
					Color c2;
					if (account.NeedsLogin)
					{
						c2 = Theme.Yellow;
					}
					else
					{
						c2 = (flag ? Theme.Green : Theme.Muted);
					}
					Theme.Text(g3, s, font7, c2, new RectangleF(r2.X + Theme.Sf(70f), r2.Y + Theme.Sf(32f), Theme.Sf(200f), Theme.Sf(18f)));
				}
				RectangleF r3 = new RectangleF(r2.Right - Theme.Sf(238f), r2.Y + Theme.Sf(36f), Theme.Sf(112f), Theme.Sf(28f));
				RectangleF r4 = new RectangleF(r2.Right - Theme.Sf(116f), r2.Y + Theme.Sf(36f), Theme.Sf(104f), Theme.Sf(28f));
				bool flag2 = i == hoverRow && hoverBtn == 1;
				bool flag3 = i == hoverRow && hoverBtn == 2;
				Theme.FillRound(g, flag2 ? Theme.CardHover : Theme.Card, r3, Theme.Sf(8f));
				Theme.StrokeRound(g, flag2 ? Theme.A(Theme.Green, 160) : Theme.Border, r3, Theme.Sf(8f));
				Theme.FillRound(g, flag3 ? Color.FromArgb(70, 30, 36) : Theme.Card, r4, Theme.Sf(8f));
				Theme.StrokeRound(g, flag3 ? Theme.Red : Theme.Border, r4, Theme.Sf(8f));
				using Font font8 = Theme.Bold(12f);
				Theme.Text(g, flag ? "Выбран" : "Сменить аккаунт", font8, flag ? Theme.Green : Theme.Fg, r3, StringAlignment.Center);
				Theme.Text(g, "Выйти", font8, flag3 ? Theme.Red : Theme.Muted, r4, StringAlignment.Center);
			}
		}
	}

	private readonly XButton login;

	private readonly XButton loginCode;

	private readonly XLabel status;

	private readonly AccountList list;

	private readonly XInput offlineNick;

	private readonly XInput clientId;

	private readonly XButton useOffline;

	private readonly XButton help;

	private readonly XCard devCard;

	private readonly XLabel devState;

	private readonly XButton devEdit;

	private bool devEditing;

	private float spin;

	private readonly Timer anim = new Timer
	{
		Interval = 30
	};

	public AccountsPage(MainForm f)
		: base(f)
	{
		AccountsPage accountsPage = this;
		XCard xCard = Add(new XCard
		{
			Title = "Вход через Microsoft",
			Icon = "\ue77b",
			Hint = "Для лицензионных серверов и своего скина",
			Radius = 16
		}, 36, 84, 470, 214);
		xCard.Controls.Add(new XLabel
		{
			Wrap = true,
			Px = 12.5f,
			Color = Theme.Muted,
			Text = "Нажмите кнопку — откроется сайт Microsoft в браузере. Пароль вводите только там: лаунчер его не видит и не хранит. После входа он получит ник, скин и проверку лицензии.",
			Bounds = new Rectangle(Theme.S(20f), Theme.S(56f), Theme.S(430f), Theme.S(54f))
		});
		login = new XButton
		{
			Style = BtnStyle.Primary,
			Text = "Войти через Microsoft",
			Icon = "\ue77b",
			FontPx = 15f,
			Radius = 12f,
			Bounds = new Rectangle(Theme.S(20f), Theme.S(114f), Theme.S(250f), Theme.S(46f))
		};
		login.Click += async (object s, EventArgs e) =>
		{
			await f.AddMicrosoftAccount();
		};
		xCard.Controls.Add(login);
		loginCode = new XButton
		{
			Style = BtnStyle.Secondary,
			Text = "Войти по коду",
			FontPx = 12.5f,
			Radius = 12f,
			Bounds = new Rectangle(Theme.S(280f), Theme.S(114f), Theme.S(170f), Theme.S(46f))
		};
		loginCode.Click += async (object s, EventArgs e) =>
		{
			await f.AddMicrosoftAccountByCode();
		};
		xCard.Controls.Add(loginCode);
		status = new XLabel
		{
			Px = 12.5f,
			Color = Theme.Muted,
			Text = "",
			Bounds = new Rectangle(Theme.S(20f), Theme.S(168f), Theme.S(430f), Theme.S(36f)),
			Wrap = true
		};
		xCard.Controls.Add(status);
		XCard xCard2 = Add(new XCard
		{
			Title = "Офлайн-ник",
			Icon = "\ue77b",
			Hint = "Одиночная игра и серверы без лицензии",
			Radius = 16
		}, 36, 312, 470, 120);
		offlineNick = new XInput
		{
			Icon = "\ue77b",
			Placeholder = "Ник (латиница)",
			Bounds = new Rectangle(Theme.S(20f), Theme.S(60f), Theme.S(270f), Theme.S(44f))
		};
		offlineNick.Text = f.Cfg.Nick;
		offlineNick.Box.MaxLength = 16;
		offlineNick.Box.TextChanged += (object s, EventArgs e) =>
		{
			f.Cfg.Nick = accountsPage.offlineNick.Text.Trim();
			f.SaveCfg();
			f.Home.SyncNick();
		};
		xCard2.Controls.Add(offlineNick);
		useOffline = new XButton
		{
			Text = "Играть офлайн",
			FontPx = 13f,
			Bounds = new Rectangle(Theme.S(300f), Theme.S(60f), Theme.S(150f), Theme.S(44f))
		};
		useOffline.Click += (object s, EventArgs e) =>
		{
			f.Accounts.Selected = null;
			f.Accounts.Save();
			f.AccountsChanged();
		};
		xCard2.Controls.Add(useOffline);
		devCard = Add(new XCard
		{
			Title = "Настройка входа (Client ID)",
			Icon = "\ue943",
			Hint = "Делает один раз автор лаунчера",
			Radius = 16
		}, 36, 446, 470, 126);
		clientId = new XInput
		{
			Icon = "\ue943",
			Placeholder = "00000000-0000-0000-0000-000000000000",
			Bounds = new Rectangle(Theme.S(20f), Theme.S(60f), Theme.S(300f), Theme.S(44f))
		};
		clientId.Text = f.Cfg.ClientId;
		clientId.Box.Font = Theme.Mono(13f);
		clientId.Box.TextChanged += (object s, EventArgs e) =>
		{
			f.Cfg.ClientId = accountsPage.clientId.Text.Trim();
			f.SaveCfg();
			f.Auth.ClientId = f.Cfg.ClientId;
			accountsPage.ApplyDevUi();
		};
		clientId.Box.Leave += (object s, EventArgs e) =>
		{
			accountsPage.devEditing = false;
			accountsPage.ApplyDevUi();
		};
		devCard.Controls.Add(clientId);
		devState = new XLabel
		{
			Wrap = true,
			Px = 12.5f,
			Color = Theme.Muted,
			Bounds = new Rectangle(Theme.S(20f), Theme.S(58f), Theme.S(300f), Theme.S(50f))
		};
		devCard.Controls.Add(devState);
		help = new XButton
		{
			Text = "Как получить?",
			Icon = "\ue946",
			FontPx = 12.5f,
			Bounds = new Rectangle(Theme.S(330f), Theme.S(60f), Theme.S(120f), Theme.S(44f))
		};
		help.Click += (object s, EventArgs e) =>
		{
			accountsPage.ShowHelp();
		};
		devCard.Controls.Add(help);
		devEdit = new XButton
		{
			Text = "Изменить",
			FontPx = 12.5f,
			Bounds = new Rectangle(Theme.S(330f), Theme.S(60f), Theme.S(120f), Theme.S(44f))
		};
		devEdit.Click += (object s, EventArgs e) =>
		{
			accountsPage.devEditing = true;
			accountsPage.ApplyDevUi();
			accountsPage.clientId.Box.Focus();
		};
		devCard.Controls.Add(devEdit);
		accountsPage.ApplyDevUi();
		list = Add(new AccountList(f), 530, 84, 470, 488);
		SkinFaces.Updated += () =>
		{
			accountsPage.list.Invalidate();
			accountsPage.F.Home.Invalidate(invalidateChildren: true);
		};
		anim.Tick += (object s, EventArgs e) =>
		{
			if (accountsPage.F.AuthBusy)
			{
				accountsPage.spin += 0.25f;
				accountsPage.status.Invalidate();
				accountsPage.login.Invalidate();
			}
		};
		anim.Start();
	}

	public void SetStatus(string text, bool error, bool success = false)
	{
		status.Text = text;
		ref Color color = ref status.Color;
		Color color2;
		if (error)
		{
			color2 = Theme.Red;
		}
		else
		{
			color2 = (success ? Theme.Green : Theme.Muted);
		}
		color = color2;
		status.Invalidate();
	}

	public void SetBusy(bool b)
	{
		login.Enabled = !b;
		loginCode.Enabled = !b;
		login.Text = (b ? "Ожидание входа..." : "Войти через Microsoft");
		login.Invalidate();
		loginCode.Invalidate();
	}

	public void Reload()
	{
		list.Invalidate();
	}

	/// <summary>
	/// Client ID — настройка одного человека: автора лаунчера. Игрокам поле не нужно,
	/// поэтому после сохранения карточка сворачивается до одной строчки.
	/// </summary>
	public void ApplyDevUi()
	{
		bool flag = MicrosoftAuth.IsValidClientId(F.Cfg.ClientId);
		bool editing = devEditing || !flag;
		clientId.Visible = editing;
		help.Visible = editing;
		devState.Visible = flag && !devEditing;
		devEdit.Visible = flag && !devEditing;
		if (devState.Visible)
		{
			string id = F.Cfg.ClientId;
			devState.Text = "Готово: " + id.Substring(0, 8) + "… сохранён в launcher.json.\nИгрокам вводить его не нужно.";
			devState.Color = Theme.Green;
		}
		devCard.Hint = (flag ? "Настроено — вход работает" : "Игрокам не нужно: заполняет один раз автор лаунчера");
		devCard.Invalidate();
	}

	public void ShowHelp()
	{
		OAuthError oAuthError = new OAuthError
		{
			Short = "Client ID настраивает один раз автор лаунчера",
			Title = "Как включить вход через Microsoft",
			Detail = "Игрокам регистрировать ничего не нужно — они просто нажимают «Войти через Microsoft». Настройку делает один человек: у лаунчера должен быть собственный Client ID бесплатного приложения в Azure. Без него вход технически невозможен, а чужой Client ID использовать нельзя.",
			LinkText = "Открыть регистрацию приложений в Azure",
			LinkUrl = OAuthErrors.AzurePortal
		};
		oAuthError.Fix.Add("portal.azure.com → «Регистрация приложений» → «Новая регистрация», имя любое (например, MrBean Launcher).");
		oAuthError.Fix.Add("«Поддерживаемые типы учётных записей» → «Только личные учётные записи Microsoft».");
		oAuthError.Fix.Add("«URI перенаправления» → «Мобильные и настольные приложения» → http://localhost");
		oAuthError.Fix.Add("«Проверка подлинности» → «Разрешить потоки общедоступных клиентов» = Да. Секретный ключ (client secret) не создавать.");
		oAuthError.Fix.Add("Скопируйте «Идентификатор приложения (клиента)» и вставьте в поле Client ID — один раз, он сохранится в launcher.json.");
		oAuthError.Fix.Add("Отправьте этот Client ID на одобрение Mojang: aka.ms/mce-reviewappid. Пока Mojang не ответит, Minecraft будет отклонять вход.");
		oAuthError.Fix.Add("Лаунчер принимает ответ на адрес " + F.Auth.RedirectUri + " — порт менять не нужно, в Azure достаточно http://localhost.");
		F.ShowNotice(oAuthError, isError: false);
	}

	protected override void Paint2(Graphics g)
	{
		Header(g, "Аккаунты", "Вход через Microsoft, смена аккаунта и офлайн-ник");
	}
}
