using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MrBeanLauncher.Core;

namespace MrBeanLauncher.Ui;

public class MainForm : Form
{
	public const int W = 1120;

	public const int H = 680;

	public const int SIDE = 84;

	public const int TITLE = 44;

	public readonly string Root;

	public readonly Settings Cfg;

	public readonly Installer Inst;

	public readonly int TotalRam;

	private readonly Image hero;

	private readonly Image bg;

	private readonly Image face;

	private readonly ContentHost host;

	private readonly Sidebar side;

	private readonly List<NavItem> nav = new List<NavItem>();

	private readonly List<Control> pages = new List<Control>();

	public HomePage Home;

	public ModsPage Mods;

	public SettingsPage SettingsP;

	public ConsolePage Console;

	public AboutPage About;

	public AccountsPage AccountsPage;

	public readonly AccountStore Accounts;

	public readonly MicrosoftAuth Auth;

	public bool AuthBusy;

	public const int PageHome = 0;

	public const int PageAccounts = 1;

	public const int PageMods = 2;

	public const int PageSettings = 3;

	public const int PageConsole = 4;

	public const int PageAbout = 5;

	private readonly NotifyIcon tray;

	public bool Busy;

	public bool GameRunning;

	private CancellationTokenSource cts;

	private Process game;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ClassStyle |= 131072;
			return createParams;
		}
	}

	private string SwitchFile => Path.Combine(Root, "switch_request.json");

	public MainForm(string root)
	{
		Root = root;
		Cfg = Settings.Load(root);
		TotalRam = Native.TotalRamMb();
		Inst = new Installer(root);
		Inst.BundledModStream = () => typeof(MainForm).Assembly.GetManifestResourceStream("MrBeanLauncher.Assets.MrBeanVisuals.jar");
		hero = Theme.Res("hero.jpg");
		bg = Theme.Res("bg.jpg");
		face = Theme.Res("face.png");
		Accounts = new AccountStore(root);
		Accounts.Load();
		Auth = new MicrosoftAuth(Cfg.ClientId);
		Auth.Status = (string st) =>
		{
			try
			{
				BeginInvoke((Action)(() =>
				{
					AccountsPage.SetStatus(st, error: false, st.StartsWith("Авторизация успешна"));
					Home.SetStatus(st, error: false);
				}));
			}
			catch
			{
			}
		};
		Text = "MrBean Launcher";
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterScreen;
		ClientSize = new Size(Theme.S(1120f), Theme.S(680f));
		BackColor = Theme.Bg;
		DoubleBuffered = true;
		try
		{
			Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		Opacity = 0.0;
		side = new Sidebar(this, face)
		{
			Bounds = new Rectangle(0, 0, Theme.S(84f), Theme.S(680f))
		};
		Controls.Add(side);
		host = new ContentHost(bg)
		{
			Bounds = new Rectangle(Theme.S(84f), 0, Theme.S(1036f), Theme.S(680f))
		};
		Controls.Add(host);
		TitleBar value = new TitleBar(this)
		{
			Bounds = new Rectangle(0, 0, Theme.S(1036f), Theme.S(44f))
		};
		host.Controls.Add(value);
		Home = new HomePage(this, hero);
		Mods = new ModsPage(this);
		SettingsP = new SettingsPage(this);
		AccountsPage = new AccountsPage(this);
		Console = new ConsolePage(this);
		About = new AboutPage(this, hero);
		pages.AddRange(new Control[6] { Home, AccountsPage, Mods, SettingsP, Console, About });
		foreach (Control page in pages)
		{
			page.Bounds = new Rectangle(0, Theme.S(44f), Theme.S(1036f), Theme.S(636f));
			page.Visible = false;
			host.Controls.Add(page);
		}
		string[] array = new string[6] { "Главная", "Аккаунты", "Моды", "Настройки", "Консоль", "О моде" };
		string[] array2 = new string[6] { "\ue80f", "\ue77b", "\ue74c", "\ue713", "\ue756", "\ue946" };
		for (int num = 0; num < array.Length; num++)
		{
			int idx = num;
			NavItem navItem = new NavItem
			{
				Text = array[num],
				Icon = array2[num],
				Location = new Point(Theme.S(6f), Theme.S(92 + num * 70))
			};
			navItem.Click += (object s, EventArgs e) =>
			{
				ShowPage(idx);
			};
			nav.Add(navItem);
			side.Controls.Add(navItem);
		}
		NavItem navItem2 = new NavItem
		{
			Text = "Папка",
			Icon = "\ue838",
			Location = new Point(Theme.S(6f), Theme.S(600f))
		};
		navItem2.Click += (object s, EventArgs e) =>
		{
			OpenFolder(Inst.GameDir);
		};
		side.Controls.Add(navItem2);
		ShowPage(0);
		tray = new NotifyIcon
		{
			Icon = Icon,
			Text = "MrBean Launcher",
			Visible = false
		};
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip
		{
			Items = 
			{
				{
					"Открыть лаунчер",
					(Image)null,
					(EventHandler)((object s, EventArgs e) =>
					{
						RestoreFromTray();
					})
				},
				{
					"Консоль игры",
					(Image)null,
					(EventHandler)((object s, EventArgs e) =>
					{
						RestoreFromTray();
						ShowPage(4);
					})
				},
				{
					"Выход",
					(Image)null,
					(EventHandler)((object s, EventArgs e) =>
					{
						tray.Visible = false;
						Application.Exit();
					})
				}
			}
		};
		tray.ContextMenuStrip = contextMenuStrip;
		tray.DoubleClick += (object s, EventArgs e) =>
		{
			RestoreFromTray();
		};
		System.Windows.Forms.Timer fade = new System.Windows.Forms.Timer
		{
			Interval = 15
		};
		fade.Tick += (object s, EventArgs e) =>
		{
			Opacity = Math.Min(1.0, Opacity + 0.08);
			if (Opacity >= 1.0)
			{
				fade.Stop();
			}
		};
		Shown += (object s, EventArgs e) =>
		{
			fade.Start();
			Native.RoundCorners(Handle);
			Home.RefreshAccount();
			RestoreSessionSilently();
		};
		FormClosing += (object s, FormClosingEventArgs e) =>
		{
			SaveCfg();
			tray.Visible = false;
		};
	}

	public void ShowPage(int i)
	{
		for (int j = 0; j < pages.Count; j++)
		{
			pages[j].Visible = j == i;
		}
		for (int k = 0; k < nav.Count; k++)
		{
			nav[k].Active = k == i;
		}
		if (pages[i] == Mods)
		{
			Mods.Reload();
		}
	}

	public void SaveCfg()
	{
		Cfg.Save(Root);
	}

	public static void OpenFolder(string p)
	{
		try
		{
			Directory.CreateDirectory(p);
			Process.Start("explorer.exe", "\"" + p + "\"");
		}
		catch
		{
		}
	}

	public void Toast(string msg, bool error = false)
	{
		Home.SetStatus(msg, error);
	}

	private void RestoreFromTray()
	{
		Show();
		WindowState = FormWindowState.Normal;
		Activate();
		tray.Visible = false;
	}

	public async void PlayClicked()
	{
		if (Busy)
		{
			cts?.Cancel();
			return;
		}
		if (GameRunning)
		{
			ShowPage(4);
			return;
		}
		if (Accounts.Current == null)
		{
			string text = Home.Nick.Text.Trim();
			if (!Regex.IsMatch(text, "^[A-Za-z0-9_]{3,16}$"))
			{
				Home.SetStatus("Ник: 3–16 символов — латиница, цифры и _", error: true);
				Home.Nick.Focus();
				return;
			}
			Cfg.Nick = text;
		}
		Cfg.WithVisuals = Home.Profile.Selected == 0;
		SaveCfg();
		await RunInstall(launch: true);
	}

	public async Task RunInstall(bool launch, bool forceJava = false)
	{
		if (Busy)
		{
			return;
		}
		Busy = true;
		Home.SetBusy(b: true);
		cts = new CancellationTokenSource();
		Inst.Parallel = Cfg.Parallel;
		Inst.WithVisuals = Cfg.WithVisuals;
		Inst.OptimizationMods = Cfg.OptMods;
		Inst.RussianLang = Cfg.RussianLang;
		Inst.ForceJavaCheck = forceJava;
		double lastPct = 0.0;
		string lastText = "";
		long lastBytes = 0L;
		Stopwatch sw = Stopwatch.StartNew();
		double speed = 0.0;
		System.Windows.Forms.Timer ui = new System.Windows.Forms.Timer
		{
			Interval = 50
		};
		ui.Tick += (object s, EventArgs e) =>
		{
			double totalSeconds = sw.Elapsed.TotalSeconds;
			if (totalSeconds >= 0.5)
			{
				long bytesDownloaded = Inst.BytesDownloaded;
				speed = speed * 0.5 + (double)(bytesDownloaded - lastBytes) / totalSeconds * 0.5;
				lastBytes = bytesDownloaded;
				sw.Restart();
			}
			Home.SetProgress(lastPct / 100.0, lastText, speed);
		};
		Inst.Progress = (double p, string t) =>
		{
			lastPct = p;
			lastText = t;
		};
		Inst.Log = (string l) =>
		{
			Console.Append("[Лаунчер] " + l, ConsolePage.Kind.Launcher);
		};
		ui.Start();
		GameInfo gi = null;
		try
		{
			gi = await Task.Run(() => Inst.Install(cts.Token));
		}
		catch (OperationCanceledException)
		{
			Home.SetStatus("Загрузка отменена", error: true);
		}
		catch (Exception ex2)
		{
			Console.Append("[Ошибка] " + ex2, ConsolePage.Kind.Error);
			Home.SetStatus("Ошибка: " + ex2.Message, error: true);
		}
		finally
		{
			ui.Stop();
			Busy = false;
			Home.SetBusy(b: false);
		}
		if (gi == null)
		{
			return;
		}
		if (!launch)
		{
			Home.SetStatus("Все файлы на месте ✔", error: false);
			return;
		}
		GameLauncher.LaunchAccount acc = null;
		if (Accounts.Current != null)
		{
			try
			{
				acc = await EnsureSession();
			}
			catch (AuthException ex3)
			{
				string short2 = ex3.Info?.Short ?? ex3.Message;
				Home.SetStatus(short2, error: true);
				AccountsPage.SetStatus(short2, error: true);
				if (ex3.Info != null)
				{
					ShowNotice(ex3.Info);
				}
				if (MessageBox.Show(this, "Играть с офлайн-ником «" + Cfg.Nick + "»?", "MrBean Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
				{
					return;
				}
			}
		}
		Launch(gi, acc);
	}

	private void WriteAccountsForGame(GameLauncher.LaunchAccount acc)
	{
		try
		{
			string text = Path.Combine(Inst.GameDir, "config", "mrbeanvisuals");
			Directory.CreateDirectory(text);
			List<object> list = new List<object>();
			foreach (Account account in Accounts.Accounts)
			{
				list.Add(new Dictionary<string, object>
				{
					{ "name", account.Name },
					{ "uuid", account.Uuid },
					{
						"skin",
						account.SkinUrl ?? ""
					},
					{ "needsLogin", account.NeedsLogin }
				});
			}
			Dictionary<string, object> o = new Dictionary<string, object>
			{
				{ "request", SwitchFile },
				{
					"current",
					acc?.Uuid
				},
				{ "offlineNick", Cfg.Nick },
				{ "accounts", list }
			};
			File.WriteAllText(Path.Combine(text, "launcher_accounts.json"), Json.Write(o, 2), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(SwitchFile))
			{
				File.Delete(SwitchFile);
			}
		}
		catch (Exception ex)
		{
			Console.Append("[Лаунчер] Не удалось передать список аккаунтов в игру: " + ex.Message, ConsolePage.Kind.Error);
		}
	}

	private async Task<bool> HandleSwitchRequest()
	{
		if (!File.Exists(SwitchFile))
		{
			return false;
		}
		object obj = null;
		try
		{
			obj = Json.Parse(File.ReadAllText(SwitchFile, Encoding.UTF8));
		}
		catch
		{
		}
		try
		{
			File.Delete(SwitchFile);
		}
		catch
		{
		}
		if (obj == null)
		{
			return false;
		}
		switch (obj.Str("action"))
		{
		case "select":
		{
			string uuid = obj.Str("uuid");
			if (Accounts.Accounts.All((Account a) => a.Uuid != uuid))
			{
				return false;
			}
			Accounts.Selected = uuid;
			Accounts.Save();
			AccountsChanged();
			Console.Append("[Лаунчер] Смена аккаунта из игры: " + Accounts.Current.Name, ConsolePage.Kind.Launcher);
			break;
		}
		case "offline":
		{
			string text = obj.Str("nick");
			if (text == null || !Regex.IsMatch(text, "^[A-Za-z0-9_]{3,16}$"))
			{
				return false;
			}
			Cfg.Nick = text;
			SaveCfg();
			Accounts.Selected = null;
			Accounts.Save();
			AccountsChanged();
			Home.SyncNick();
			Console.Append("[Лаунчер] Игра перезапускается с офлайн-ником " + text, ConsolePage.Kind.Launcher);
			break;
		}
		case "add":
		{
			ShowPage(1);
			int before = Accounts.Accounts.Count;
			await AddMicrosoftAccount();
			if (Accounts.Accounts.Count == before && Accounts.Current == null)
			{
				return false;
			}
			break;
		}
		default:
			return false;
		}
		Home.SetStatus("Перезапускаю игру с новым аккаунтом...", error: false);
		await RunInstall(launch: true);
		return true;
	}

	private void Launch(GameInfo gi, GameLauncher.LaunchAccount acc)
	{
		WriteAccountsForGame(acc);
		try
		{
			Console.Append("[Лаунчер] Запускаю Minecraft 1.21.11 (Fabric " + gi.LoaderVersion + "), " + ((acc != null) ? ("аккаунт Microsoft " + acc.Name) : ("офлайн-ник " + Cfg.Nick)) + ", память " + Cfg.RamMb / 1024 + " ГБ", ConsolePage.Kind.Launcher);
			game = GameLauncher.Start(Inst, gi, Cfg, (string line) =>
			{
				Console.Append(line, ConsolePage.Kind.Game);
			}, acc);
			GameRunning = true;
			Home.SetRunning(r: true);
			Home.SetStatus("Minecraft запускается... Меню мода в игре — правый Shift", error: false);
			game.Exited += (object s, EventArgs e) =>
			{
				BeginInvoke((Action)(() =>
				{
					OnGameExit();
				}));
			};
			if (Cfg.ShowConsoleOnLaunch)
			{
				ShowPage(4);
			}
			if (!Cfg.HideOnLaunch)
			{
				return;
			}
			System.Windows.Forms.Timer t = new System.Windows.Forms.Timer
			{
				Interval = 2500
			};
			t.Tick += (object s, EventArgs e) =>
			{
				t.Stop();
				if (GameRunning)
				{
					tray.Visible = true;
					Hide();
					tray.ShowBalloonTip(2000, "MrBean Launcher", "Игра запущена. Лаунчер свернулся в трей.", ToolTipIcon.Info);
				}
			};
			t.Start();
		}
		catch (Exception ex)
		{
			Home.SetStatus("Не удалось запустить: " + ex.Message, error: true);
			Console.Append("[Ошибка] " + ex, ConsolePage.Kind.Error);
		}
	}

	private void OnGameExit()
	{
		GameRunning = false;
		Home.SetRunning(r: false);
		int num = 0;
		try
		{
			num = game.ExitCode;
		}
		catch
		{
		}
		Console.Append("[Лаунчер] Игра закрыта, код " + num, ConsolePage.Kind.Launcher);
		if (!Visible)
		{
			RestoreFromTray();
		}
		if (File.Exists(SwitchFile))
		{
			HandleSwitchRequest();
		}
		else if (num != 0)
		{
			Home.SetStatus("Игра вылетела (код " + num + "). Смотри вкладку «Консоль»", error: true);
			ShowPage(4);
		}
		else
		{
			Home.SetStatus("Игра закрыта. Ещё каточку?", error: false);
		}
	}

	public void KillGame()
	{
		try
		{
			if (game != null && !game.HasExited)
			{
				game.Kill();
			}
		}
		catch
		{
		}
	}

	public void AccountsChanged()
	{
		Home.RefreshAccount();
		AccountsPage.Reload();
	}

	public async Task AddMicrosoftAccount()
	{
		await AddMicrosoftAccount(useDeviceCode: false);
	}

	public async Task AddMicrosoftAccountByCode()
	{
		await AddMicrosoftAccount(useDeviceCode: true);
	}

	private async Task AddMicrosoftAccount(bool useDeviceCode)
	{
		if (AuthBusy)
		{
			return;
		}
		Auth.ClientId = (Cfg.ClientId ?? "").Trim();
		if (!MicrosoftAuth.IsValidClientId(Auth.ClientId))
		{
			ShowPage(1);
			AccountsPage.SetStatus("Вход через Microsoft ещё не настроен — это делает один раз автор лаунчера. Пока можно играть с офлайн-ником.", error: false);
			ShowNotice(MicrosoftAuth.NotConfigured(), isError: false);
			return;
		}
		AuthBusy = true;
		AccountsPage.SetBusy(b: true);
		try
		{
			MinecraftSession session;
			if (useDeviceCode)
			{
				session = await LoginByCode();
			}
			else
			{
				try
				{
					session = await LoginByBrowser();
				}
				catch (AuthException ex) when (ex.Info != null && ex.Info.SuggestCode)
				{
					AccountsPage.SetStatus("Пробую вход по коду...", error: false);
					session = await LoginByCode();
				}
			}
			Account account = Accounts.Upsert(session);
			AccountsChanged();
			AccountsPage.SetStatus("Вы успешно вошли в MrBean Visuals!", error: false, success: true);
			Home.SetStatus("Вы успешно вошли в MrBean Visuals! Аккаунт: " + account.Name, error: false);
			Console.Append("[Лаунчер] Вход через Microsoft: " + account.Name, ConsolePage.Kind.Launcher);
			Activate();
		}
		catch (OperationCanceledException)
		{
			AccountsPage.SetStatus("Вход отменён.", error: false);
		}
		catch (AuthException ex2)
		{
			ShowAuthError(ex2);
		}
		catch (Exception ex3)
		{
			AccountsPage.SetStatus("Не удалось войти. Попробуйте снова. (" + ex3.Message + ")", error: true);
			Console.Append("[Лаунчер] Вход не удался: " + ex3.Message, ConsolePage.Kind.Error);
		}
		finally
		{
			AuthBusy = false;
			AccountsPage.SetBusy(b: false);
		}
	}

	/// <summary>Основной вход: страница Microsoft в системном браузере, OAuth 2.0 + PKCE.</summary>
	private async Task<MinecraftSession> LoginByBrowser()
	{
		CancellationTokenSource cts = new CancellationTokenSource();
		BrowserLoginDialog browserLoginDialog = new BrowserLoginDialog(Auth.RedirectUri);
		browserLoginDialog.Cancelled += () =>
		{
			cts.Cancel();
		};
		browserLoginDialog.CodeRequested += () =>
		{
			cts.Cancel();
		};
		browserLoginDialog.FormClosed += (object obj, FormClosedEventArgs e) =>
		{
			if (AuthBusy)
			{
				cts.Cancel();
			}
		};
		browserLoginDialog.Show(this);
		Action<string> oldStatus = Auth.Status;
		Action<string> oldBrowser = Auth.BrowserUrl;
		Auth.Status = delegate(string st)
		{
			oldStatus(st);
			OnUi(() =>
			{
				if (!browserLoginDialog.IsDisposed && !st.StartsWith("Авторизация"))
				{
					browserLoginDialog.SetState(st);
				}
			});
		};
		Auth.BrowserUrl = (string u) => OnUi(() =>
		{
			if (!browserLoginDialog.IsDisposed)
			{
				browserLoginDialog.SetTarget(u);
			}
		});
		try
		{
			return await Auth.LoginInteractive(cts.Token);
		}
		catch (OperationCanceledException)
		{
			if (browserLoginDialog.WantsCode)
			{
				throw new AuthException(new OAuthError
				{
					Short = "Входим по коду",
					Title = "Выбран вход по коду",
					Detail = "Браузерный вход отменён по вашей просьбе. Дальше — код на microsoft.com/link.",
					SuggestCode = true
				});
			}
			throw;
		}
		finally
		{
			Auth.Status = oldStatus;
			Auth.BrowserUrl = oldBrowser;
			if (!browserLoginDialog.IsDisposed)
			{
				browserLoginDialog.Close();
			}
			browserLoginDialog.Dispose();
		}
	}

	/// <summary>Запасной вход: код на microsoft.com/link — работает без локального порта.</summary>
	private async Task<MinecraftSession> LoginByCode()
	{
		CancellationTokenSource cts = new CancellationTokenSource();
		DeviceCodeDialog deviceCodeDialog = new DeviceCodeDialog();
		deviceCodeDialog.Cancelled += () =>
		{
			cts.Cancel();
		};
		deviceCodeDialog.FormClosed += (object obj, FormClosedEventArgs e) =>
		{
			if (AuthBusy)
			{
				cts.Cancel();
			}
		};
		deviceCodeDialog.Show(this);
		Action<string> oldStatus = Auth.Status;
		Auth.Status = delegate(string st)
		{
			oldStatus(st);
			OnUi(() =>
			{
				if (!deviceCodeDialog.IsDisposed && !st.StartsWith("Авторизация"))
				{
					deviceCodeDialog.SetState(st);
				}
			});
		};
		bool ok = false;
		try
		{
			MinecraftSession s = await Auth.LoginDeviceCode((MicrosoftAuth.DeviceCode dc) => OnUi(() =>
			{
				if (!deviceCodeDialog.IsDisposed)
				{
					deviceCodeDialog.ShowCode(dc);
				}
			}), cts.Token);
			ok = true;
			if (!deviceCodeDialog.IsDisposed)
			{
				deviceCodeDialog.Success(s.Name);
			}
			return s;
		}
		finally
		{
			Auth.Status = oldStatus;
			if (!ok)
			{
				if (!deviceCodeDialog.IsDisposed)
				{
					deviceCodeDialog.Close();
				}
				deviceCodeDialog.Dispose();
			}
		}
	}

	private void OnUi(Action a)
	{
		try
		{
			BeginInvoke(a);
		}
		catch
		{
		}
	}

	private void ShowAuthError(AuthException ex)
	{
		OAuthError oAuthError = ex.Info ?? new OAuthError
		{
			Short = ex.Message,
			Title = "Не удалось войти в Microsoft",
			Detail = ex.Message + "\n\nПопробуйте ещё раз. Если браузерный вход не проходит — выберите «Войти по коду»."
		};
		AccountsPage.SetStatus(oAuthError.Short, error: true);
		Home.SetStatus(oAuthError.Short, error: true);
		Console.Append("[Лаунчер] Вход не удался: " + oAuthError.Title, ConsolePage.Kind.Error);
		ShowNotice(oAuthError);
	}

	public void ShowNotice(OAuthError info, bool isError = true)
	{
		using NoticeDialog noticeDialog = new NoticeDialog(info, isError);
		noticeDialog.ShowDialog(this);
		Activate();
	}

	public async Task<GameLauncher.LaunchAccount> EnsureSession()
	{
		Account a = Accounts.Current;
		if (a == null)
		{
			return null;
		}
		if (a.NeedsLogin || string.IsNullOrEmpty(a.RefreshToken))
		{
			throw new AuthException("Аккаунту «" + a.Name + "» нужен повторный вход.", needsRelogin: true);
		}
		if (string.IsNullOrEmpty(a.McToken) || a.McExpiresUtc < DateTime.UtcNow.AddMinutes(10.0))
		{
			Auth.ClientId = (Cfg.ClientId ?? "").Trim();
			try
			{
				MinecraftSession minecraftSession = await Auth.Refresh(a.RefreshToken, CancellationToken.None);
				if (minecraftSession.Uuid != a.Uuid)
				{
					throw new AuthException("Microsoft вернул другой профиль. Войдите снова.", needsRelogin: true);
				}
				a = Accounts.Upsert(minecraftSession);
			}
			catch (AuthException ex)
			{
				if (ex.NeedsRelogin)
				{
					a.NeedsLogin = true;
					a.Status = "Нужен вход";
					Accounts.Save();
					AccountsChanged();
				}
				throw;
			}
		}
		return new GameLauncher.LaunchAccount
		{
			Name = a.Name,
			Uuid = a.Uuid,
			AccessToken = a.McToken,
			ClientId = Cfg.ClientId,
			Xuid = "0"
		};
	}

	private async void RestoreSessionSilently()
	{
		Account a = Accounts.Current;
		if (a == null || a.NeedsLogin || !MicrosoftAuth.IsValidClientId(Cfg.ClientId))
		{
			return;
		}
		if (!string.IsNullOrEmpty(a.McToken) && a.McExpiresUtc > DateTime.UtcNow.AddMinutes(30.0))
		{
			a.Status = "Активен";
			return;
		}
		try
		{
			await EnsureSession();
			Home.SetStatus("Сессия восстановлена: " + a.Name, error: false);
		}
		catch (AuthException ex)
		{
			Home.SetStatus(ex.Info?.Short ?? ex.Message, error: true);
		}
		catch
		{
		}
		AccountsChanged();
	}
}
