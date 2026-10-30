using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrBeanLauncher.Core
{
    /// <summary>Ошибка входа с понятным сообщением для игрока.</summary>
    public class AuthException : Exception
    {
        public readonly bool NeedsRelogin;
        public AuthException(string message, bool needsRelogin = false) : base(message) { NeedsRelogin = needsRelogin; }
    }

    public class MinecraftSession
    {
        public string Uuid, Name, AccessToken, Xuid, SkinUrl;
        public DateTime ExpiresUtc;
        public string RefreshToken;   // токен Microsoft (обновляется при каждом входе)
    }

    /// <summary>
    /// Официальный вход Microsoft → Xbox Live → XSTS → Minecraft Services.
    /// Authorization Code Flow + PKCE, системный браузер, приём ответа на localhost.
    /// Пароль игрока лаунчер никогда не видит. Токены не пишутся в логи.
    /// </summary>
    public class MicrosoftAuth
    {
        public string ClientId;
        // адреса можно подменить для тестов
        public string AuthorizeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        public string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        public string XblUrl = "https://user.auth.xboxlive.com/user/authenticate";
        public string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
        public string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
        public string EntitlementsUrl = "https://api.minecraftservices.com/entitlements/mcstore";
        public string ProfileUrl = "https://api.minecraftservices.com/minecraft/profile";
        public const string Scope = "XboxLive.signin offline_access";

        public Action<string> Status = delegate { };
        /// <summary>Как открыть ссылку в браузере (в тестах подменяется).</summary>
        public Action<string> OpenBrowser = url => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

        readonly HttpClient http;

        public MicrosoftAuth(string clientId)
        {
            ClientId = clientId;
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
            http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MrBeanLauncher/2.0");
        }

        // ------------------------------------------------------------------ PKCE
        public static string Base64Url(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }

        public static string NewVerifier()
        {
            var b = new byte[48];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return Base64Url(b);
        }

        public static string Challenge(string verifier)
        {
            using (var sha = SHA256.Create()) return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
        }

        // ------------------------------------------------------------------ вход через браузер
        /// <summary>Полный вход: браузер → код → токены → профиль Minecraft.</summary>
        public async Task<MinecraftSession> LoginInteractive(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ClientId))
                throw new AuthException("Не указан Client ID приложения Azure (Настройки → Аккаунт).");
            string verifier = NewVerifier(), challenge = Challenge(verifier);
            string state = NewVerifier().Substring(0, 24);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string redirect = "http://localhost:" + port;
            try
            {
                string url = AuthorizeUrl + "?client_id=" + Uri.EscapeDataString(ClientId)
                    + "&response_type=code&redirect_uri=" + Uri.EscapeDataString(redirect)
                    + "&scope=" + Uri.EscapeDataString(Scope)
                    + "&code_challenge=" + challenge + "&code_challenge_method=S256"
                    + "&state=" + state + "&prompt=select_account";
                Status("Подключение к Microsoft...");
                OpenBrowser(url);
                var query = await WaitForRedirect(listener, ct);
                string err; query.TryGetValue("error", out err);
                if (err != null)
                {
                    string desc; query.TryGetValue("error_description", out desc);
                    if (err == "access_denied") throw new AuthException("Вход отменён.");
                    throw new AuthException("Microsoft вернул ошибку: " + err + (desc == null ? "" : " — " + desc));
                }
                string gotState; query.TryGetValue("state", out gotState);
                if (gotState != state) throw new AuthException("Ответ не прошёл проверку безопасности (state). Попробуйте снова.");
                string code; query.TryGetValue("code", out code);
                if (string.IsNullOrEmpty(code)) throw new AuthException("Microsoft не вернул код входа.");

                var tok = await PostForm(TokenUrl, new Dictionary<string, string> {
                    {"client_id", ClientId}, {"scope", Scope}, {"code", code}, {"redirect_uri", redirect},
                    {"grant_type", "authorization_code"}, {"code_verifier", verifier}
                }, ct);
                return await FinishWithMsToken(tok, ct);
            }
            finally { listener.Stop(); }
        }

        // ------------------------------------------------------------------ вход по коду (как в официальном лаунчере / на консолях)
        public string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";

        public class DeviceCode { public string UserCode, VerificationUri, Message; public int ExpiresIn, Interval; }

        /// <summary>
        /// Вход по коду: лаунчер показывает короткий код, человек вводит его на microsoft.com/link
        /// и входит там (пароль видит только Microsoft). Лаунчер ждёт подтверждения и сам получает токены.
        /// </summary>
        public async Task<MinecraftSession> LoginDeviceCode(Action<DeviceCode> show, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ClientId))
                throw new AuthException("Не указан Client ID приложения Azure.");
            Status("Подключение к Microsoft...");
            var dc = await PostForm(DeviceCodeUrl, new Dictionary<string, string> { {"client_id", ClientId}, {"scope", Scope} }, ct);
            string deviceCode = dc.Str("device_code");
            var info = new DeviceCode
            {
                UserCode = dc.Str("user_code"),
                VerificationUri = dc.Str("verification_uri") ?? "https://www.microsoft.com/link",
                Message = dc.Str("message"),
                ExpiresIn = dc.Get("expires_in") != null ? (int)dc.Long("expires_in") : 900,
                Interval = dc.Get("interval") != null ? (int)dc.Long("interval") : 5
            };
            if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(info.UserCode))
                throw new AuthException("Microsoft не выдал код входа.");
            show(info);
            Status("Ожидание входа на сайте Microsoft...");

            int interval = Math.Max(1, info.Interval);
            var deadline = DateTime.UtcNow.AddSeconds(info.ExpiresIn);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), ct);
                HttpResponseMessage r;
                try
                {
                    r = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string> {
                        {"client_id", ClientId}, {"grant_type", "urn:ietf:params:oauth:grant-type:device_code"}, {"device_code", deviceCode}
                    }), ct);
                }
                catch (Exception e) when (!(e is OperationCanceledException)) { continue; }   // сеть моргнула — пробуем ещё
                string body = await r.Content.ReadAsStringAsync();
                object j = null;
                try { j = Json.Parse(body); } catch { }
                if (r.IsSuccessStatusCode && j != null) return await FinishWithMsToken(j, ct);
                string err = j == null ? null : j.Str("error");
                if (err == "authorization_pending") continue;
                if (err == "slow_down") { interval += 5; continue; }
                if (err == "authorization_declined") throw new AuthException("Вход отменён на сайте Microsoft.");
                if (err == "expired_token" || err == "code_expired") throw new AuthException("Код устарел. Нажмите «Войти» ещё раз.");
                if (err == "invalid_client" || err == "unauthorized_client")
                    throw new AuthException("Client ID не подходит: в Azure включите «Разрешить потоки общедоступных клиентов».");
                if (err == "bad_verification_code") throw new AuthException("Неверный код. Нажмите «Войти» ещё раз.");
                throw new AuthException("Microsoft отклонил вход: " + (err ?? ((int)r.StatusCode).ToString()));
            }
            throw new AuthException("Код устарел (15 минут). Нажмите «Войти» ещё раз.");
        }

        /// <summary>Ждём один HTTP-запрос от браузера на localhost и отвечаем красивой страницей.</summary>
        async Task<Dictionary<string, string>> WaitForRedirect(TcpListener listener, CancellationToken ct)
        {
            var timeout = Task.Delay(TimeSpan.FromMinutes(5), ct);
            while (true)
            {
                var accept = listener.AcceptTcpClientAsync();
                var done = await Task.WhenAny(accept, timeout);
                if (done == timeout) { ct.ThrowIfCancellationRequested(); throw new AuthException("Время ожидания входа истекло (5 минут)."); }
                using (var client = accept.Result)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 5000;
                    var buf = new byte[8192];
                    int n = await stream.ReadAsync(buf, 0, buf.Length, ct);
                    string req = Encoding.ASCII.GetString(buf, 0, n);
                    string first = req.Split('\n')[0];
                    var parts = first.Split(' ');
                    if (parts.Length < 2 || !parts[0].StartsWith("GET")) continue;
                    string path = parts[1];
                    if (path.StartsWith("/favicon")) { await Respond(stream, 404, ""); continue; }
                    var q = ParseQuery(path);
                    bool ok = q.ContainsKey("code");
                    await Respond(stream, 200, Page(ok));
                    if (q.ContainsKey("code") || q.ContainsKey("error")) return q;
                }
            }
        }

        static string Page(bool ok)
        {
            string title = ok ? "Вход выполнен" : "Вход не выполнен";
            string text = ok ? "Можно закрыть эту вкладку и вернуться в MrBean Launcher." : "Вернитесь в MrBean Launcher и попробуйте снова.";
            string color = ok ? "#2EE59D" : "#FF5A64";
            return "<!doctype html><html><head><meta charset='utf-8'><title>MrBean Launcher</title><style>body{background:#0f1117;color:#eef;font-family:Segoe UI,sans-serif;display:flex;height:100vh;align-items:center;justify-content:center;margin:0}div{background:#181b25;padding:40px 56px;border-radius:18px;text-align:center;border:1px solid #2a2e3c}h1{color:" + color + ";margin:0 0 10px}</style></head><body><div><h1>" + title + "</h1><p>" + text + "</p></div></body></html>";
        }

        static async Task Respond(Stream s, int code, string html)
        {
            var body = Encoding.UTF8.GetBytes(html);
            var head = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + (code == 200 ? " OK" : " Not Found") + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
            await s.WriteAsync(head, 0, head.Length);
            await s.WriteAsync(body, 0, body.Length);
            await s.FlushAsync();
        }

        public static Dictionary<string, string> ParseQuery(string path)
        {
            var d = new Dictionary<string, string>();
            int i = path.IndexOf('?');
            if (i < 0) return d;
            foreach (var kv in path.Substring(i + 1).Split('&'))
            {
                var p = kv.Split(new[] { '=' }, 2);
                if (p.Length == 2) d[Uri.UnescapeDataString(p[0])] = Uri.UnescapeDataString(p[1].Replace('+', ' '));
            }
            return d;
        }

        // ------------------------------------------------------------------ обновление сессии
        /// <summary>Восстановить сессию по сохранённому refresh-токену Microsoft.</summary>
        public async Task<MinecraftSession> Refresh(string refreshToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ClientId)) throw new AuthException("Не указан Client ID приложения Azure.", true);
            Status("Обновление входа...");
            object tok;
            try
            {
                tok = await PostForm(TokenUrl, new Dictionary<string, string> {
                    {"client_id", ClientId}, {"scope", Scope}, {"refresh_token", refreshToken}, {"grant_type", "refresh_token"}
                }, ct);
            }
            catch (AuthException e) { throw new AuthException("Сессия Microsoft истекла. Войдите снова. (" + e.Message + ")", true); }
            return await FinishWithMsToken(tok, ct);
        }

        async Task<MinecraftSession> FinishWithMsToken(object tok, CancellationToken ct)
        {
            string msAccess = tok.Str("access_token"), refresh = tok.Str("refresh_token");
            if (string.IsNullOrEmpty(msAccess)) throw new AuthException("Microsoft не выдал токен доступа.", true);

            // Xbox Live
            Status("Вход в Xbox Live...");
            var xbl = await PostJson(XblUrl, new Dictionary<string, object> {
                {"Properties", new Dictionary<string, object> { {"AuthMethod", "RPS"}, {"SiteName", "user.auth.xboxlive.com"}, {"RpsTicket", "d=" + msAccess} }},
                {"RelyingParty", "http://auth.xboxlive.com"}, {"TokenType", "JWT"}
            }, null, ct, XboxError);
            string xblToken = xbl.Str("Token");
            string uhs = xbl.Obj("DisplayClaims").Arr("xui").Select(x => x.Str("uhs")).FirstOrDefault();

            // XSTS
            var xsts = await PostJson(XstsUrl, new Dictionary<string, object> {
                {"Properties", new Dictionary<string, object> { {"SandboxId", "RETAIL"}, {"UserTokens", new List<object> { xblToken }} }},
                {"RelyingParty", "rp://api.minecraftservices.com/"}, {"TokenType", "JWT"}
            }, null, ct, XboxError);
            string xstsToken = xsts.Str("Token");
            string uhs2 = xsts.Obj("DisplayClaims").Arr("xui").Select(x => x.Str("uhs")).FirstOrDefault() ?? uhs;

            // Minecraft
            Status("Проверка лицензии Minecraft...");
            var mc = await PostJson(McLoginUrl, new Dictionary<string, object> { {"identityToken", "XBL3.0 x=" + uhs2 + ";" + xstsToken} }, null, ct, McError);
            string mcToken = mc.Str("access_token");
            long expires = mc.Long("expires_in");
            if (string.IsNullOrEmpty(mcToken)) throw new AuthException("Сервер Minecraft не выдал токен.");

            var ent = await GetJson(EntitlementsUrl, mcToken, ct, McError);
            bool owns = ent.Arr("items").Any(i => { var n = i.Str("name"); return n == "product_minecraft" || n == "game_minecraft"; });

            Status("Загрузка профиля...");
            object prof;
            try { prof = await GetJson(ProfileUrl, mcToken, ct, McError); }
            catch (AuthException) when (!owns) { throw new AuthException("На этом аккаунте нет лицензии Minecraft: Java Edition."); }
            string id = prof.Str("id"), name = prof.Str("name");
            if (string.IsNullOrEmpty(id)) throw new AuthException(owns ? "У аккаунта ещё нет профиля Minecraft. Создайте его на minecraft.net." : "На этом аккаунте нет лицензии Minecraft: Java Edition.");
            string skin = prof.Arr("skins").Where(s => s.Str("state") == "ACTIVE").Select(s => s.Str("url")).FirstOrDefault();

            Status("Авторизация успешна!");
            return new MinecraftSession
            {
                Uuid = id, Name = name, AccessToken = mcToken, Xuid = "0", SkinUrl = skin,
                ExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expires - 60)), RefreshToken = refresh
            };
        }

        // ------------------------------------------------------------------ сообщения об ошибках
        static string XboxError(int status, string body)
        {
            try
            {
                var j = Json.Parse(body);
                long x = j.Long("XErr");
                if (x == 2148916233) return "У этого аккаунта Microsoft нет профиля Xbox. Зайдите на xbox.com и создайте его.";
                if (x == 2148916235) return "Xbox Live недоступен в вашей стране.";
                if (x == 2148916236 || x == 2148916237) return "Аккаунт требует подтверждения взрослого (Южная Корея).";
                if (x == 2148916238) return "Это детский аккаунт — его нужно добавить в семью Microsoft взрослым.";
            }
            catch { }
            return "Xbox Live отклонил вход (код " + status + ").";
        }

        static string McError(int status, string body)
        {
            if (status == 403 && body.IndexOf("Invalid app registration", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Microsoft и Xbox пройдены, но Mojang ещё не одобрил приложение MrBean Launcher. Нужна заявка на aka.ms/mce-reviewappid (Client ID " + Settings.BuiltInClientId + "). Пока можно играть с офлайн-ником.";
            if (status == 404) return "Профиль Minecraft не найден — на аккаунте нет лицензии Java Edition.";
            if (status == 429) return "Слишком много попыток входа. Подождите пару минут.";
            if (status == 401) return "Сервер Minecraft не принял вход. Попробуйте снова.";
            return "Сервер Minecraft вернул ошибку " + status + ".";
        }

        // ------------------------------------------------------------------ HTTP
        async Task<object> PostForm(string url, Dictionary<string, string> form, CancellationToken ct)
        {
            HttpResponseMessage r;
            try { r = await http.PostAsync(url, new FormUrlEncodedContent(form), ct); }
            catch (Exception e) when (!(e is OperationCanceledException)) { throw new AuthException("Нет связи с Microsoft: " + e.Message); }
            string body = await r.Content.ReadAsStringAsync();
            object j = null;
            try { j = Json.Parse(body); } catch { }
            if (!r.IsSuccessStatusCode)
            {
                string err = j == null ? null : j.Str("error");
                if (err == "invalid_grant") throw new AuthException("Сессия Microsoft истекла или отозвана. Войдите снова.", true);
                if (err == "invalid_client" || err == "unauthorized_client") throw new AuthException("Client ID не подходит для входа (проверьте регистрацию приложения в Azure).");
                throw new AuthException("Microsoft отклонил вход: " + (err ?? ((int)r.StatusCode).ToString()));
            }
            return j;
        }

        async Task<object> PostJson(string url, object payload, string bearer, CancellationToken ct, Func<int, string, string> errorText)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(Json.Write(payload), Encoding.UTF8, "application/json") };
            req.Headers.Accept.ParseAdd("application/json");
            if (bearer != null) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
            return await Send(req, ct, errorText);
        }

        async Task<object> GetJson(string url, string bearer, CancellationToken ct, Func<int, string, string> errorText)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
            return await Send(req, ct, errorText);
        }

        async Task<object> Send(HttpRequestMessage req, CancellationToken ct, Func<int, string, string> errorText)
        {
            HttpResponseMessage r;
            try { r = await http.SendAsync(req, ct); }
            catch (Exception e) when (!(e is OperationCanceledException)) { throw new AuthException("Нет связи с сервером: " + e.Message); }
            string body = await r.Content.ReadAsStringAsync();
            if (!r.IsSuccessStatusCode) throw new AuthException(errorText((int)r.StatusCode, body));
            try { return Json.Parse(body); } catch { throw new AuthException("Сервер вернул непонятный ответ."); }
        }

        public async Task<byte[]> Download(string url, CancellationToken ct)
        {
            return await http.GetByteArrayAsync(url);
        }
    }
}
