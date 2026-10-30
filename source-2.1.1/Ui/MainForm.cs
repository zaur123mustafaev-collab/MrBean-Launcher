using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MrBeanLauncher.Core;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    public partial class MainForm : Form
    {
        public const int W = 1120, H = 680, SIDE = 84, TITLE = 44;

        public readonly string Root;
        public readonly Settings Cfg;
        public readonly Installer Inst;
        public readonly int TotalRam;

        readonly Image hero, bg, face;
        readonly ContentHost host;
        readonly Sidebar side;
        readonly List<NavItem> nav = new List<NavItem>();
        readonly List<Control> pages = new List<Control>();
        public HomePage Home; public ModsPage Mods; public SettingsPage SettingsP; public ConsolePage Console; public AboutPage About;
        public AccountsPage AccountsPage;
        public readonly AccountStore Accounts;
        public readonly MicrosoftAuth Auth;
        public bool AuthBusy;
        public const int PageHome = 0, PageAccounts = 1, PageMods = 2, PageSettings = 3, PageConsole = 4, PageAbout = 5;
        readonly NotifyIcon tray;

        public bool Busy, GameRunning;
        CancellationTokenSource cts;
        Process game;

        public MainForm(string root)
        {
            Root = root;
            Cfg = Settings.Load(root);
            TotalRam = Native.TotalRamMb();
            Inst = new Installer(root);
            Inst.BundledModStream = () => typeof(MainForm).Assembly.GetManifestResourceStream("MrBeanLauncher.Assets.MrBeanVisuals.jar");

            hero = Res("hero.jpg"); bg = Res("bg.jpg"); face = Res("face.png");
            Accounts = new AccountStore(root);
            Accounts.Load();
            Auth = new MicrosoftAuth(Cfg.ClientId);
            Auth.Status = st => { try { BeginInvoke((Action)(() => { AccountsPage.SetStatus(st, false, st.StartsWith("Авторизация успешна")); Home.SetStatus(st, false); })); } catch { } };

            Text = "MrBean Launcher";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(W), S(H));
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Opacity = 0;

            side = new Sidebar(this, face) { Bounds = new Rectangle(0, 0, S(SIDE), S(H)) };
            Controls.Add(side);
            host = new ContentHost(bg) { Bounds = new Rectangle(S(SIDE), 0, S(W - SIDE), S(H)) };
            Controls.Add(host);

            var tb = new TitleBar(this) { Bounds = new Rectangle(0, 0, S(W - SIDE), S(TITLE)) };
            host.Controls.Add(tb);

            Home = new HomePage(this, hero); Mods = new ModsPage(this); SettingsP = new SettingsPage(this);
            AccountsPage = new AccountsPage(this);
            Console = new ConsolePage(this); About = new AboutPage(this, hero);
            pages.AddRange(new Control[] { Home, AccountsPage, Mods, SettingsP, Console, About });
            foreach (var p in pages) { p.Bounds = new Rectangle(0, S(TITLE), S(W - SIDE), S(H - TITLE)); p.Visible = false; host.Controls.Add(p); }

            string[] names = { "Главная", "Аккаунты", "Моды", "Настройки", "Консоль", "О моде" };
            string[] icons = { IcHome, IcPerson, IcMods, IcSettings, IcConsole, IcInfo };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var n = new NavItem { Text = names[i], Icon = icons[i], Location = new Point(S(6), S(92 + i * 70)) };
                n.Click += (s, e) => ShowPage(idx);
                nav.Add(n); side.Controls.Add(n);
            }
            var folder = new NavItem { Text = "Папка", Icon = IcFolder, Location = new Point(S(6), S(H - 80)) };
            folder.Click += (s, e) => OpenFolder(Inst.GameDir);
            side.Controls.Add(folder);
            ShowPage(0);

            tray = new NotifyIcon { Icon = Icon, Text = "MrBean Launcher", Visible = false };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть лаунчер", null, (s, e) => RestoreFromTray());
            menu.Items.Add("Консоль игры", null, (s, e) => { RestoreFromTray(); ShowPage(PageConsole); });
            menu.Items.Add("Выход", null, (s, e) => { tray.Visible = false; Application.Exit(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => RestoreFromTray();

            var fade = new System.Windows.Forms.Timer { Interval = 15 };
            fade.Tick += (s, e) => { Opacity = Math.Min(1, Opacity + 0.08); if (Opacity >= 1) fade.Stop(); };
            Shown += (s, e) => { fade.Start(); Native.RoundCorners(Handle); Home.RefreshAccount(); RestoreSessionSilently(); };
            FormClosing += (s, e) => { SaveCfg(); tray.Visible = false; };
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; /* CS_DROPSHADOW */ return cp; }
        }

        public void ShowPage(int i)
        {
            for (int k = 0; k < pages.Count; k++) pages[k].Visible = k == i;
            for (int k = 0; k < nav.Count; k++) nav[k].Active = k == i;
            if (pages[i] == Mods) Mods.Reload();
        }

        public void SaveCfg() { Cfg.Save(Root); }

        public static void OpenFolder(string p)
        {
            try { Directory.CreateDirectory(p); Process.Start("explorer.exe", "\"" + p + "\""); } catch { }
        }

        public void Toast(string msg, bool error = false) { Home.SetStatus(msg, error); }

        void RestoreFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); tray.Visible = false; }

        // ------------------------------------------------------------------ установка и запуск
        public async void PlayClicked()
        {
            if (Busy) { cts?.Cancel(); return; }
            if (GameRunning) { ShowPage(PageConsole); return; }
            if (Accounts.Current == null)
            {
                string nick = Home.Nick.Text.Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(nick, "^[A-Za-z0-9_]{3,16}$"))
                {
                    Home.SetStatus("Ник: 3–16 символов — латиница, цифры и _", true);
                    Home.Nick.Focus();
                    return;
                }
                Cfg.Nick = nick;
            }
            Cfg.WithVisuals = Home.Profile.Selected == 0;
            SaveCfg();
            await RunInstall(true);
        }

        public async Task RunInstall(bool launch, bool forceJava = false)
        {
            if (Busy) return;
            Busy = true; Home.SetBusy(true);
            cts = new CancellationTokenSource();
            Inst.Parallel = Cfg.Parallel;
            Inst.WithVisuals = Cfg.WithVisuals; Inst.OptimizationMods = Cfg.OptMods;
            Inst.RussianLang = Cfg.RussianLang;
            Inst.ForceJavaCheck = forceJava;
            double lastPct = 0; string lastText = "";
            long lastBytes = 0; var sw = Stopwatch.StartNew(); double speed = 0;
            var ui = new System.Windows.Forms.Timer { Interval = 50 };
            ui.Tick += (s, e) =>
            {
                double secs = sw.Elapsed.TotalSeconds;
                if (secs >= 0.5)
                {
                    long b = Inst.BytesDownloaded;
                    speed = speed * 0.5 + (b - lastBytes) / secs * 0.5; lastBytes = b; sw.Restart();
                }
                Home.SetProgress(lastPct / 100.0, lastText, speed);
            };
            Inst.Progress = (p, t) => { lastPct = p; lastText = t; };
            Inst.Log = l => Console.Append("[Лаунчер] " + l, ConsolePage.Kind.Launcher);
            ui.Start();
            GameInfo gi = null;
            try
            {
                gi = await Task.Run(() => Inst.Install(cts.Token));
            }
            catch (OperationCanceledException) { Home.SetStatus("Загрузка отменена", true); }
            catch (Exception ex)
            {
                Console.Append("[Ошибка] " + ex, ConsolePage.Kind.Error);
                Home.SetStatus("Ошибка: " + ex.Message, true);
            }
            finally
            {
                ui.Stop(); Busy = false; Home.SetBusy(false);
            }
            if (gi == null) return;
            if (!launch) { Home.SetStatus("Все файлы на месте ✔", false); return; }
            GameLauncher.LaunchAccount acc = null;
            if (Accounts.Current != null)
            {
                try { acc = await EnsureSession(); }
                catch (AuthException ex)
                {
                    Home.SetStatus(ex.Message, true);
                    AccountsPage.SetStatus(ex.Message, true);
                    if (MessageBox.Show(this, ex.Message + "\n\nИграть с офлайн-ником «" + Cfg.Nick + "»?", "MrBean Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                }
            }
            Launch(gi, acc);
        }

        string SwitchFile { get { return Path.Combine(Root, "switch_request.json"); } }

        /// <summary>Список аккаунтов для меню в игре — только ники, uuid и скины, без токенов.</summary>
        void WriteAccountsForGame(GameLauncher.LaunchAccount acc)
        {
            try
            {
                string dir = Path.Combine(Inst.GameDir, "config", "mrbeanvisuals");
                Directory.CreateDirectory(dir);
                var list = new List<object>();
                foreach (var a in Accounts.Accounts)
                    list.Add(new Dictionary<string, object> { {"name", a.Name}, {"uuid", a.Uuid}, {"skin", a.SkinUrl ?? ""}, {"needsLogin", a.NeedsLogin} });
                var d = new Dictionary<string, object> {
                    {"request", SwitchFile}, {"current", acc != null ? acc.Uuid : null}, {"offlineNick", Cfg.Nick}, {"accounts", list}
                };
                File.WriteAllText(Path.Combine(dir, "launcher_accounts.json"), Json.Write(d, 2), new UTF8Encoding(false));
                if (File.Exists(SwitchFile)) File.Delete(SwitchFile);
            }
            catch (Exception ex) { Console.Append("[Лаунчер] Не удалось передать список аккаунтов в игру: " + ex.Message, ConsolePage.Kind.Error); }
        }

        /// <summary>Игра попросила сменить аккаунт — выполняем и перезапускаем.</summary>
        async Task<bool> HandleSwitchRequest()
        {
            if (!File.Exists(SwitchFile)) return false;
            object r = null;
            try { r = Json.Parse(File.ReadAllText(SwitchFile, Encoding.UTF8)); } catch { }
            try { File.Delete(SwitchFile); } catch { }
            if (r == null) return false;
            string action = r.Str("action");
            if (action == "select")
            {
                string uuid = r.Str("uuid");
                if (Accounts.Accounts.All(a => a.Uuid != uuid)) return false;
                Accounts.Selected = uuid; Accounts.Save(); AccountsChanged();
                Console.Append("[Лаунчер] Смена аккаунта из игры: " + Accounts.Current.Name, ConsolePage.Kind.Launcher);
            }
            else if (action == "offline")
            {
                string nick = r.Str("nick");
                if (nick == null || !System.Text.RegularExpressions.Regex.IsMatch(nick, "^[A-Za-z0-9_]{3,16}$")) return false;
                Cfg.Nick = nick; SaveCfg(); Accounts.Selected = null; Accounts.Save(); AccountsChanged(); Home.SyncNick();
                Console.Append("[Лаунчер] Игра перезапускается с офлайн-ником " + nick, ConsolePage.Kind.Launcher);
            }
            else if (action == "add")
            {
                ShowPage(PageAccounts);
                int before = Accounts.Accounts.Count;
                await AddMicrosoftAccount();
                if (Accounts.Accounts.Count == before && Accounts.Current == null) return false;
            }
            else return false;
            Home.SetStatus("Перезапускаю игру с новым аккаунтом...", false);
            await RunInstall(true);
            return true;
        }

        void Launch(GameInfo gi, GameLauncher.LaunchAccount acc)
        {
            WriteAccountsForGame(acc);
            try
            {
                Console.Append("[Лаунчер] Запускаю Minecraft " + Installer.MC + " (Fabric " + gi.LoaderVersion + "), " + (acc != null ? "аккаунт Microsoft " + acc.Name : "офлайн-ник " + Cfg.Nick) + ", память " + Cfg.RamMb / 1024 + " ГБ", ConsolePage.Kind.Launcher);
                game = GameLauncher.Start(Inst, gi, Cfg, line => Console.Append(line, ConsolePage.Kind.Game), acc);
                GameRunning = true;
                Home.SetRunning(true);
                Home.SetStatus("Minecraft запускается... Меню мода в игре — правый Shift", false);
                game.Exited += (s, e) => BeginInvoke((Action)(() => OnGameExit()));
                if (Cfg.ShowConsoleOnLaunch) ShowPage(PageConsole);
                if (Cfg.HideOnLaunch)
                {
                    var t = new System.Windows.Forms.Timer { Interval = 2500 };
                    t.Tick += (s, e) => { t.Stop(); if (GameRunning) { tray.Visible = true; Hide(); tray.ShowBalloonTip(2000, "MrBean Launcher", "Игра запущена. Лаунчер свернулся в трей.", ToolTipIcon.Info); } };
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                Home.SetStatus("Не удалось запустить: " + ex.Message, true);
                Console.Append("[Ошибка] " + ex, ConsolePage.Kind.Error);
            }
        }

        void OnGameExit()
        {
            GameRunning = false;
            Home.SetRunning(false);
            int code = 0; try { code = game.ExitCode; } catch { }
            Console.Append("[Лаунчер] Игра закрыта, код " + code, ConsolePage.Kind.Launcher);
            if (!Visible) RestoreFromTray();
            if (File.Exists(SwitchFile)) { var _ = HandleSwitchRequest(); return; }
            if (code != 0)
            {
                Home.SetStatus("Игра вылетела (код " + code + "). Смотри вкладку «Консоль»", true);
                ShowPage(PageConsole);
            }
            else Home.SetStatus("Игра закрыта. Ещё каточку?", false);
        }

        public void KillGame() { try { if (game != null && !game.HasExited) game.Kill(); } catch { } }

        // ------------------------------------------------------------------ аккаунты Microsoft
        public void AccountsChanged() { Home.RefreshAccount(); AccountsPage.Reload(); }

        public async Task AddMicrosoftAccount()
        {
            if (AuthBusy) return;
            if (string.IsNullOrWhiteSpace(Cfg.ClientId))
            {
                ShowPage(PageAccounts);
                AccountsPage.SetStatus("Вход через Microsoft ещё не настроен: один раз вставьте Client ID в карточку ниже. Пока можно играть с офлайн-ником.", true);
                AccountsPage.ShowHelp();
                return;
            }
            AuthBusy = true; AccountsPage.SetBusy(true);
            Auth.ClientId = Cfg.ClientId;
            var cts = new CancellationTokenSource();
            var dlg = new DeviceCodeDialog();
            dlg.Cancelled += () => cts.Cancel();
            dlg.FormClosed += (s, e) => { if (AuthBusy) cts.Cancel(); };
            dlg.Show(this);
            var oldStatus = Auth.Status;
            Auth.Status = st => { oldStatus(st); try { BeginInvoke((Action)(() => { if (!dlg.IsDisposed && !st.StartsWith("Авторизация")) dlg.SetState(st); })); } catch { } };
            try
            {
                var s = await Auth.LoginDeviceCode(dc => BeginInvoke((Action)(() => { if (!dlg.IsDisposed) dlg.ShowCode(dc); })), cts.Token);
                var a = Accounts.Upsert(s);
                AccountsChanged();
                AccountsPage.SetStatus("Вы успешно вошли в MrBean Visuals!", false, true);
                Home.SetStatus("Вы успешно вошли в MrBean Visuals! Аккаунт: " + a.Name, false);
                Console.Append("[Лаунчер] Вход через Microsoft: " + a.Name, ConsolePage.Kind.Launcher);
                Activate();
                if (!dlg.IsDisposed) dlg.Success(a.Name);
            }
            catch (OperationCanceledException)
            {
                AccountsPage.SetStatus("Вход отменён.", false);
            }
            catch (AuthException ex)
            {
                AccountsPage.SetStatus("Не удалось войти. Попробуйте снова. " + ex.Message, true);
                Console.Append("[Лаунчер] Вход не удался: " + ex.Message, ConsolePage.Kind.Error);
                if (!dlg.IsDisposed) dlg.SetState(ex.Message, true);
            }
            catch (Exception ex)
            {
                AccountsPage.SetStatus("Не удалось войти. Попробуйте снова. (" + ex.Message + ")", true);
                if (!dlg.IsDisposed) dlg.SetState("Не удалось войти. Попробуйте снова.", true);
            }
            finally { Auth.Status = oldStatus; AuthBusy = false; AccountsPage.SetBusy(false); }
        }

        /// <summary>Действующая сессия для запуска: если токен Minecraft скоро истечёт — обновляем.</summary>
        public async Task<GameLauncher.LaunchAccount> EnsureSession()
        {
            var a = Accounts.Current;
            if (a == null) return null;
            if (a.NeedsLogin || string.IsNullOrEmpty(a.RefreshToken)) throw new AuthException("Аккаунту «" + a.Name + "» нужен повторный вход.", true);
            if (string.IsNullOrEmpty(a.McToken) || a.McExpiresUtc < DateTime.UtcNow.AddMinutes(10))
            {
                Auth.ClientId = Cfg.ClientId;
                try
                {
                    var s = await Auth.Refresh(a.RefreshToken, CancellationToken.None);
                    if (s.Uuid != a.Uuid) throw new AuthException("Microsoft вернул другой профиль. Войдите снова.", true);
                    a = Accounts.Upsert(s);
                }
                catch (AuthException ex)
                {
                    if (ex.NeedsRelogin) { a.NeedsLogin = true; a.Status = "Нужен вход"; Accounts.Save(); AccountsChanged(); }
                    throw;
                }
            }
            return new GameLauncher.LaunchAccount { Name = a.Name, Uuid = a.Uuid, AccessToken = a.McToken, ClientId = Cfg.ClientId, Xuid = "0" };
        }

        async void RestoreSessionSilently()
        {
            var a = Accounts.Current;
            if (a == null || a.NeedsLogin || string.IsNullOrWhiteSpace(Cfg.ClientId)) return;
            if (!string.IsNullOrEmpty(a.McToken) && a.McExpiresUtc > DateTime.UtcNow.AddMinutes(30)) { a.Status = "Активен"; return; }
            try { await EnsureSession(); Home.SetStatus("Сессия восстановлена: " + a.Name, false); }
            catch (AuthException ex) { Home.SetStatus(ex.Message, true); }
            catch { }
            AccountsChanged();
        }
    }

    // ====================================================================== каркас
    public class ContentHost : Panel
    {
        readonly Image bg; Bitmap cache;
        public ContentHost(Image bg)
        {
            this.bg = bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); cache?.Dispose(); cache = null; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (cache == null && Width > 0 && Height > 0)
            {
                cache = new Bitmap(Width, Height);
                using (var g = Graphics.FromImage(cache))
                {
                    Hq(g);
                    g.Clear(Theme.Bg);
                    if (bg != null)
                    {
                        float sc = Math.Max(Width / (float)bg.Width, Height / (float)bg.Height);
                        float w = bg.Width * sc, h = bg.Height * sc;
                        g.DrawImage(bg, (Width - w) / 2, (Height - h) / 2 - h * 0.08f, w, h);
                    }
                    var r = new Rectangle(0, 0, Width, Height);
                    using (var b = new LinearGradientBrush(r, Color.FromArgb(235, 12, 13, 18), Color.FromArgb(150, 12, 13, 18), 0f)) g.FillRectangle(b, r);
                    using (var b = new LinearGradientBrush(r, Color.FromArgb(0, 12, 13, 18), Color.FromArgb(245, 12, 13, 18), 90f)) g.FillRectangle(b, r);
                    // мягкое зелёное пятно
                    using (var p = new GraphicsPath())
                    {
                        p.AddEllipse(Width * 0.45f, -Height * 0.3f, Width * 0.8f, Height * 0.9f);
                        using (var pb = new PathGradientBrush(p) { CenterColor = Color.FromArgb(40, 124, 230, 60), SurroundColors = new[] { Color.FromArgb(0, 124, 230, 60) } })
                            g.FillPath(pb, p);
                    }
                }
            }
            if (cache != null) e.Graphics.DrawImage(cache, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel);
        }
    }

    public class Sidebar : Panel
    {
        readonly MainForm f; readonly Image face;
        public Sidebar(MainForm f, Image face)
        {
            this.f = f; this.face = face;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Native.DragWindow(f.Handle); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Hq(g);
            g.Clear(Side);
            using (var p = new Pen(Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
            var r = new RectangleF(S(18), S(20), S(48), S(48));
            // кольцо-градиент вокруг аватара
            var ring = r; ring.Inflate(S(3), S(3));
            using (var b = Accent(ring, 45)) g.FillEllipse(b, ring);
            if (face != null)
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(r);
                    var st = g.Save(); g.SetClip(path); g.DrawImage(face, r); g.Restore(st);
                }
        }
    }

    public class TitleBar : Control
    {
        readonly MainForm f;
        public TitleBar(MainForm f)
        {
            this.f = f;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            var close = new XButton { Style = BtnStyle.Ghost, Icon = IcClose, FontPx = 11, Size = new Size(S(46), S(32)), Radius = 8 };
            var min = new XButton { Style = BtnStyle.Ghost, Icon = IcMin, FontPx = 11, Size = new Size(S(46), S(32)), Radius = 8 };
            close.Click += (s, e) => f.Close();
            min.Click += (s, e) => f.WindowState = FormWindowState.Minimized;
            Controls.Add(close); Controls.Add(min);
            Layout += (s, e) => { close.Location = new Point(Width - S(54), S(6)); min.Location = new Point(Width - S(102), S(6)); };
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Native.DragWindow(f.Handle); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Hq(g);
            using (var fb = Bold(13)) Theme.Text(g, "MrBean Launcher", fb, Theme.Fg, new RectangleF(S(28), 0, S(200), Height));
            using (var fu = Reg(12)) Theme.Text(g, "v2.1.1  •  Minecraft " + Installer.MC, fu, Muted, new RectangleF(S(150), 0, S(300), Height));
        }
    }

    /// <summary>Прозрачная страница.</summary>
    public class Page : Panel
    {
        protected readonly MainForm F;
        public Page(MainForm f)
        {
            F = f;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected void Header(Graphics g, string title, string sub)
        {
            using (var f = Black(30)) Theme.Text(g, title, f, Theme.Fg, new RectangleF(S(36), S(4), S(600), S(42)));
            using (var f = Reg(14)) Theme.Text(g, sub, f, Muted, new RectangleF(S(38), S(46), S(700), S(22)));
        }
        protected T Add<T>(T c, int x, int y, int w, int h) where T : Control { c.Bounds = new Rectangle(S(x), S(y), S(w), S(h)); Controls.Add(c); return c; }
        protected override void OnPaint(PaintEventArgs e) { Hq(e.Graphics); Paint2(e.Graphics); }
        protected virtual void Paint2(Graphics g) { }
    }
}
