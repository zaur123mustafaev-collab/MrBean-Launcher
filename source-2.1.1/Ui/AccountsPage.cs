using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MrBeanLauncher.Core;
using static MrBeanLauncher.Ui.Theme;

namespace MrBeanLauncher.Ui
{
    /// <summary>Скачивает и кэширует лица скинов (8×8 + слой шапки), увеличенные без размытия.</summary>
    public static class SkinFaces
    {
        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();
        public static event Action Updated = delegate { };

        public static Image Get(MainForm f, string uuid, string url)
        {
            if (string.IsNullOrEmpty(uuid)) return null;
            Image img;
            if (cache.TryGetValue(uuid, out img)) return img;
            cache[uuid] = null;
            string file = Path.Combine(f.Root, "cache", "skins", uuid + ".png");
            try { if (File.Exists(file)) { cache[uuid] = Face(Image.FromStream(new MemoryStream(File.ReadAllBytes(file)))); return cache[uuid]; } } catch { }
            if (string.IsNullOrEmpty(url)) return null;
            Task.Run(async () =>
            {
                try
                {
                    var bytes = await f.Auth.Download(url.Replace("http://", "https://"), CancellationToken.None);
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllBytes(file, bytes);
                    var face = Face(Image.FromStream(new MemoryStream(bytes)));
                    f.BeginInvoke((Action)(() => { cache[uuid] = face; Updated(); }));
                }
                catch { }
            });
            return null;
        }

        static Image Face(Image skin)
        {
            var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(skin, new Rectangle(0, 0, 64, 64), new Rectangle(8, 8, 8, 8), GraphicsUnit.Pixel);
                g.DrawImage(skin, new Rectangle(0, 0, 64, 64), new Rectangle(40, 8, 8, 8), GraphicsUnit.Pixel);
            }
            return bmp;
        }

        public static void DrawHead(Graphics g, Image face, RectangleF r, string name)
        {
            using (var path = Round(r, r.Width * 0.22f))
            {
                var st = g.Save();
                g.SetClip(path);
                if (face != null)
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(face, r);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                }
                else
                {
                    using (var b = Accent(r, 45)) g.FillRectangle(b, r);
                    using (var f = Black(r.Height * 0.5f / Theme.Scale))
                        Theme.Text(g, string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper(), f, Color.FromArgb(14, 18, 10), r, StringAlignment.Center);
                }
                g.Restore(st);
            }
        }
    }

    /// <summary>Управление аккаунтами: вход через Microsoft, смена, выход, офлайн-ник.</summary>
    public class AccountsPage : Page
    {
        readonly XButton login;
        readonly XLabel status;
        readonly AccountList list;
        readonly XInput offlineNick, clientId;
        readonly XButton useOffline, help;
        float spin;
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer { Interval = 30 };

        public AccountsPage(MainForm f) : base(f)
        {
            // карточка входа
            var card = Add(new XCard { Title = "Вход через Microsoft", Icon = IcPerson, Hint = "Для лицензионных серверов и своего скина", Radius = 16 }, 36, 84, 470, 214);
            card.Controls.Add(new XLabel { Wrap = true, Px = 12.5f, Color = Muted,
                Text = "Нажмите кнопку — лаунчер покажет код. Введите его на microsoft.com/link и войдите в свой аккаунт. Microsoft напишет «можно закрыть окно», и всё готово.",
                Bounds = new Rectangle(S(20), S(56), S(430), S(54)) });
            login = new XButton { Style = BtnStyle.Primary, Text = "Войти через Microsoft", Icon = IcPerson, FontPx = 15, Radius = 12, Bounds = new Rectangle(S(20), S(114), S(250), S(46)) };
            login.Click += async (s, e) => await f.AddMicrosoftAccount();
            card.Controls.Add(login);
            status = new XLabel { Px = 12.5f, Color = Muted, Text = "", Bounds = new Rectangle(S(20), S(168), S(430), S(36)), Wrap = true };
            card.Controls.Add(status);

            // офлайн
            var off = Add(new XCard { Title = "Офлайн-ник", Icon = IcPerson, Hint = "Одиночная игра и серверы без лицензии", Radius = 16 }, 36, 312, 470, 120);
            offlineNick = new XInput { Icon = IcPerson, Placeholder = "Ник (латиница)", Bounds = new Rectangle(S(20), S(60), S(270), S(44)) };
            offlineNick.Text = f.Cfg.Nick; offlineNick.Box.MaxLength = 16;
            offlineNick.Box.TextChanged += (s, e) => { f.Cfg.Nick = offlineNick.Text.Trim(); f.SaveCfg(); f.Home.SyncNick(); };
            off.Controls.Add(offlineNick);
            useOffline = new XButton { Text = "Играть офлайн", FontPx = 13, Bounds = new Rectangle(S(300), S(60), S(150), S(44)) };
            useOffline.Click += (s, e) => { f.Accounts.Selected = null; f.Accounts.Save(); f.AccountsChanged(); };
            off.Controls.Add(useOffline);

            // Client ID
            var cid = Add(new XCard { Title = "Приложение Azure (Client ID)", Icon = IcCode, Hint = "Уже вшит в лаунчер — менять не нужно", Radius = 16 }, 36, 446, 470, 126);
            clientId = new XInput { Icon = IcCode, Placeholder = "00000000-0000-0000-0000-000000000000", Bounds = new Rectangle(S(20), S(60), S(300), S(44)) };
            clientId.Text = f.Cfg.ClientId; clientId.Box.Font = Mono(13);
            clientId.Box.TextChanged += (s, e) => { f.Cfg.ClientId = clientId.Text.Trim(); f.SaveCfg(); f.Auth.ClientId = f.Cfg.ClientId; Invalidate(); };
            cid.Controls.Add(clientId);
            help = new XButton { Text = "Как получить?", Icon = IcInfo, FontPx = 12.5f, Bounds = new Rectangle(S(330), S(60), S(120), S(44)) };
            help.Click += (s, e) => ShowHelp();
            cid.Controls.Add(help);

            // список аккаунтов
            list = Add(new AccountList(f), 530, 84, 470, 488);

            SkinFaces.Updated += () => { list.Invalidate(); F.Home.Invalidate(true); };
            anim.Tick += (s, e) => { if (F.AuthBusy) { spin += 0.25f; status.Invalidate(); login.Invalidate(); } };
            anim.Start();
        }

        public void SetStatus(string text, bool error, bool success = false)
        {
            status.Text = text;
            status.Color = error ? Red : success ? Green : Muted;
            status.Invalidate();
        }

        public void SetBusy(bool b)
        {
            login.Enabled = !b;
            login.Text = b ? "Ожидание входа..." : "Войти через Microsoft";
            login.Invalidate();
        }

        public void Reload() { list.Invalidate(); }

        public void ShowHelp()
        {
            string t = "Как включить вход через Microsoft (один раз):\n\n" +
                "1. Зайдите на portal.azure.com → «Регистрация приложений» → «Новая регистрация».\n" +
                "2. Тип учётных записей: «Личные учётные записи Майкрософт».\n" +
                "3. URI перенаправления можно не указывать — вход идёт по коду (microsoft.com/link).\n" +
                "4. «Проверка подлинности» → «Разрешить потоки общедоступных клиентов» = Да (обязательно для входа по коду).\n" +
                "5. Скопируйте «Идентификатор приложения (клиента)» и вставьте его в поле Client ID.\n" +
                "6. Отправьте Client ID на одобрение Mojang: aka.ms/mce-reviewappid\n" +
                "   Пока Mojang не одобрит заявку, вход будет заканчиваться ошибкой «приложение не одобрено».\n\n" +
                "Секретный ключ (client secret) не нужен и вводить его нельзя — вход безопасный.\n\nОткрыть страницу заявки Mojang?";
            if (MessageBox.Show(this, t, "MrBean Launcher — вход через Microsoft", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                try { Process.Start(new ProcessStartInfo("https://aka.ms/mce-reviewappid") { UseShellExecute = true }); } catch { }
        }

        protected override void Paint2(Graphics g) { Header(g, "Аккаунты", "Вход через Microsoft, смена аккаунта и офлайн-ник"); }

        // ------------------------------------------------------------------ список
        class AccountList : XControl
        {
            readonly MainForm f;
            int hoverRow = -1, hoverBtn;
            const int ROW = 74;
            public AccountList(MainForm f) { this.f = f; Cursor = Cursors.Default; }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int row = (e.Y - S(46)) / S(ROW), btn = 0;
                if (row < 0 || row >= f.Accounts.Accounts.Count) row = -1;
                else
                {
                    float ry = S(46) + row * S(ROW);
                    if (e.Y > ry + S(40) && e.Y < ry + S(66))
                    {
                        if (e.X > Width - S(250) && e.X < Width - S(130)) btn = 1;
                        else if (e.X >= Width - S(124) && e.X < Width - S(16)) btn = 2;
                    }
                }
                if (row != hoverRow || btn != hoverBtn) { hoverRow = row; hoverBtn = btn; Cursor = btn > 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (hoverRow < 0 || hoverBtn == 0) return;
                var a = f.Accounts.Accounts[hoverRow];
                if (hoverBtn == 1)
                {
                    f.Accounts.Selected = a.Uuid; f.Accounts.Save(); f.AccountsChanged();
                    if (a.NeedsLogin) f.AccountsPage.SetStatus("Этому аккаунту нужен повторный вход — нажмите «Войти через Microsoft».", true);
                    else f.AccountsPage.SetStatus("Аккаунт выбран: " + a.Name, false, true);
                }
                else if (MessageBox.Show(f, "Выйти из аккаунта «" + a.Name + "»? Токены будут удалены с этого компьютера.", "MrBean Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    f.Accounts.Remove(a.Uuid); f.AccountsChanged();
                    f.AccountsPage.SetStatus("Вы вышли из аккаунта " + a.Name, false);
                }
                Invalidate();
            }

            protected override void Draw(Graphics g)
            {
                var full = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                FillRound(g, A(Card, 225), full, Sf(16));
                StrokeRound(g, A(Border, 200), full, Sf(16));
                using (var fb = Bold(15)) Theme.Text(g, "Управление аккаунтами", fb, Fg, new RectangleF(Sf(18), Sf(12), Width, Sf(24)));
                var accs = f.Accounts.Accounts;
                if (accs.Count == 0)
                {
                    using (var fi = Theme.Icon(40)) Theme.Text(g, IcPerson, fi, Dim, new RectangleF(0, Height / 2f - Sf(60), Width, Sf(50)), StringAlignment.Center);
                    using (var fb = Bold(15)) Theme.Text(g, "Аккаунтов Microsoft пока нет", fb, Muted, new RectangleF(0, Height / 2f, Width, Sf(24)), StringAlignment.Center);
                    using (var fu = Reg(12.5f)) Theme.Text(g, "Нажмите «Войти через Microsoft» — сейчас играете с офлайн-ником", fu, Dim, new RectangleF(0, Height / 2f + Sf(26), Width, Sf(20)), StringAlignment.Center);
                    return;
                }
                for (int i = 0; i < accs.Count; i++)
                {
                    var a = accs[i];
                    bool sel = a.Uuid == f.Accounts.Selected;
                    var r = new RectangleF(Sf(12), Sf(46) + i * S(ROW), Width - Sf(24), S(ROW) - Sf(6));
                    FillRound(g, sel ? A(Green, 26) : (i == hoverRow ? A(Color.White, 10) : A(Color.White, 4)), r, Sf(12));
                    if (sel) StrokeRound(g, A(Green, 150), r, Sf(12));
                    SkinFaces.DrawHead(g, SkinFaces.Get(f, a.Uuid, a.SkinUrl), new RectangleF(r.X + Sf(10), r.Y + Sf(10), Sf(48), Sf(48)), a.Name);
                    using (var fb = Bold(16)) Theme.Text(g, a.Name, fb, Fg, new RectangleF(r.X + Sf(70), r.Y + Sf(8), Sf(200), Sf(24)));
                    string st = a.NeedsLogin ? "Нужен вход" : sel ? "Активен · Microsoft ✓" : "Microsoft ✓";
                    using (var fu = Reg(12)) Theme.Text(g, st, fu, a.NeedsLogin ? Yellow : sel ? Green : Muted, new RectangleF(r.X + Sf(70), r.Y + Sf(32), Sf(200), Sf(18)));
                    // кнопки
                    var b1 = new RectangleF(r.Right - Sf(238), r.Y + Sf(36), Sf(112), Sf(28));
                    var b2 = new RectangleF(r.Right - Sf(116), r.Y + Sf(36), Sf(104), Sf(28));
                    bool h1 = i == hoverRow && hoverBtn == 1, h2 = i == hoverRow && hoverBtn == 2;
                    FillRound(g, h1 ? CardHover : Card, b1, Sf(8)); StrokeRound(g, h1 ? A(Green, 160) : Border, b1, Sf(8));
                    FillRound(g, h2 ? Color.FromArgb(70, 30, 36) : Card, b2, Sf(8)); StrokeRound(g, h2 ? Red : Border, b2, Sf(8));
                    using (var f12 = Bold(12))
                    {
                        Theme.Text(g, sel ? "Выбран" : "Сменить аккаунт", f12, sel ? Green : Fg, b1, StringAlignment.Center);
                        Theme.Text(g, "Выйти", f12, h2 ? Red : Muted, b2, StringAlignment.Center);
                    }
                }
            }
        }
    }
}
