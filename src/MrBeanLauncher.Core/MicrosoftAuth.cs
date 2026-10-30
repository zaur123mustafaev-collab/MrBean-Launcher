using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrBeanLauncher.Core;

public class MicrosoftAuth
{
	public class DeviceCode
	{
		public string UserCode;

		public string VerificationUri;

		public string Message;

		public int ExpiresIn;

		public int Interval;
	}

	public string ClientId;

	public string AuthorizeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";

	public string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

	public string XblUrl = "https://user.auth.xboxlive.com/user/authenticate";

	public string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";

	public string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";

	public string EntitlementsUrl = "https://api.minecraftservices.com/entitlements/mcstore";

	public string ProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

	public const string Scope = "XboxLive.signin offline_access";

	/// <summary>Порт для приёма ответа Microsoft (PKCE). В Azure должен быть добавлен http://localhost.</summary>
	public int LoopbackPort = 48120;

	public string RedirectUri => "http://localhost:" + LoopbackPort;

	public Action<string> Status = delegate
	{
	};

	/// <summary>Сообщает лаунчеру адрес страницы входа — чтобы показать кнопку «открыть браузер снова».</summary>
	public Action<string> BrowserUrl = delegate
	{
	};

	public Action<string> OpenBrowser = (string url) =>
	{
		Process.Start(new ProcessStartInfo(url)
		{
			UseShellExecute = true
		});
	};

	private readonly HttpClient http;

	public string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";

	public MicrosoftAuth(string clientId)
	{
		ClientId = clientId;
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		}
		catch
		{
		}
		http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30.0)
		};
		http.DefaultRequestHeaders.UserAgent.ParseAdd("MrBeanLauncher/2.1");
	}

	public static bool IsValidClientId(string id)
	{
		return Guid.TryParse((id ?? "").Trim(), out var _);
	}

	public bool HasClientId => IsValidClientId(ClientId);

	private void RequireClientId()
	{
		string trimmed = (ClientId ?? "").Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			throw new AuthException(NotConfigured());
		}
		if (!IsValidClientId(trimmed))
		{
			OAuthError e = NotConfigured();
			e.Title = "Client ID записан неверно";
			e.Detail = "Идентификатор приложения выглядит не как адрес приложения Microsoft. Нужен формат 8-4-4-4-12, например 1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d.";
			e.Short = "Client ID неполный или с опечаткой";
			throw new AuthException(e);
		}
	}

	/// <summary>Текст для случая, когда вход ещё не настроен (это делает один раз автор лаунчера, не игроки).</summary>
	public static OAuthError NotConfigured()
	{
		OAuthError e = new OAuthError
		{
			Short = "Вход через Microsoft ещё не подключён",
			Title = "Вход через Microsoft нужно один раз настроить",
			Detail = "Игрокам ничего регистрировать не нужно — настройку делает один раз автор лаунчера: у Microsoft должен быть свой Client ID (бесплатная регистрация приложения в Azure). До этого можно играть с офлайн-ником: одиночная игра и серверы без лицензии работают.",
			LinkText = "Показать, как получить Client ID",
			LinkUrl = OAuthErrors.AzurePortal
		};
		e.Fix.Add("Откройте portal.azure.com и войдите любым Microsoft-аккаунтом.");
		e.Fix.Add("«Регистрация приложений» → «Новая регистрация», имя любое (например, MrBean Launcher).");
		e.Fix.Add("«Поддерживаемые типы учётных записей» → «Только личные учётные записи Microsoft».");
		e.Fix.Add("«URI перенаправления» → платформа «Мобильные и настольные приложения» → http://localhost");
		e.Fix.Add("«Проверка подлинности» → «Разрешить потоки общедоступных клиентов» = Да.");
		e.Fix.Add("Скопируйте «Идентификатор приложения (клиента)» в лаунчер: Аккаунты → Client ID. Это один раз.");
		e.Fix.Add("Отправьте этот Client ID на одобрение Mojang: aka.ms/mce-reviewappid — без одобрения Minecraft не примет вход.");
		return e;
	}

	public static string Base64Url(byte[] b)
	{
		return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}

	public static string NewVerifier()
	{
		byte[] array = new byte[48];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		return Base64Url(array);
	}

	public static string Challenge(string verifier)
	{
		using SHA256 sHA = SHA256.Create();
		return Base64Url(sHA.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
	}

	/// <summary>
	/// Основной вход: OAuth 2.0 authorization code + PKCE (S256) через системный браузер.
	/// Пароль лаунчер не видит и не хранит — всё вводит игрок на сайте Microsoft.
	/// </summary>
	public async Task<MinecraftSession> LoginInteractive(CancellationToken ct)
	{
		RequireClientId();
		string verifier = NewVerifier();
		string challenge = Challenge(verifier);
		string state = NewVerifier();
		using LoopbackServer srv = new LoopbackServer(LoopbackPort);
		string url = AuthorizeUrl + "?client_id=" + Uri.EscapeDataString(ClientId.Trim())
			+ "&response_type=code"
			+ "&redirect_uri=" + Uri.EscapeDataString(srv.RedirectUri)
			+ "&scope=" + Uri.EscapeDataString(Scope)
			+ "&code_challenge=" + challenge
			+ "&code_challenge_method=S256"
			+ "&state=" + Uri.EscapeDataString(state)
			+ "&prompt=select_account";
		Status("Открываю браузер Microsoft...");
		BrowserUrl(url);
		try
		{
			OpenBrowser(url);
		}
		catch (Exception ex)
		{
			OAuthError e = new OAuthError
			{
				Short = "Браузер не открылся",
				Title = "Не удалось открыть браузер",
				Detail = "Windows не дала лаунчеру открыть страницу входа: " + ex.Message + "\n\nСкопируйте адрес вручную или войдите по коду.",
				LinkText = "Открыть страницу входа",
				LinkUrl = url,
				SuggestCode = true
			};
			throw new AuthException(e);
		}
		Status("Ждём входа в браузере...");
		Dictionary<string, string> q = await srv.WaitForQueryAsync(TimeSpan.FromMinutes(10.0), ct);
		ct.ThrowIfCancellationRequested();
		string err = q.GetOrEmpty("error");
		if (!string.IsNullOrEmpty(err))
		{
			throw new AuthException(OAuthErrors.Describe(err, q.GetOrEmpty("error_description"), 0));
		}
		if (q.GetOrEmpty("state") != state)
		{
			throw new AuthException("Ответ Microsoft не совпал с запросом (state). Начните вход заново.");
		}
		string code = q.GetOrEmpty("code");
		if (string.IsNullOrEmpty(code))
		{
			throw new AuthException("Microsoft не прислал код входа. Начните вход заново.");
		}
		Status("Получаем токен Microsoft...");
		object tok = await PostForm(TokenUrl, new Dictionary<string, string>
		{
			{ "client_id", ClientId.Trim() },
			{ "scope", Scope },
			{ "code", code },
			{ "redirect_uri", srv.RedirectUri },
			{ "grant_type", "authorization_code" },
			{ "code_verifier", verifier }
		}, ct);
		return await FinishWithMsToken(tok, ct);
	}

	/// <summary>
	/// Запасной вход: код на microsoft.com/link. Нужен, если локальный порт занят
	/// или браузер не доносит ответ до лаунчера (антивирь, прокси, удалённый рабочий стол).
	/// </summary>
	public async Task<MinecraftSession> LoginDeviceCode(Action<DeviceCode> show, CancellationToken ct)
	{
		RequireClientId();
		Status("Подключение к Microsoft...");
		object o = await PostForm(DeviceCodeUrl, new Dictionary<string, string>
		{
			{ "client_id", ClientId.Trim() },
			{ "scope", Scope }
		}, ct);
		string deviceCode = o.Str("device_code");
		DeviceCode dc = new DeviceCode
		{
			UserCode = o.Str("user_code"),
			VerificationUri = (o.Str("verification_uri") ?? "https://www.microsoft.com/link"),
			Message = o.Str("message"),
			ExpiresIn = (int)((o.Get("expires_in") != null) ? o.Long("expires_in") : 900),
			Interval = (int)((o.Get("interval") != null) ? o.Long("interval") : 5)
		};
		if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(dc.UserCode))
		{
			throw new AuthException("Microsoft не выдал код входа. Попробуйте ещё раз.");
		}
		show(dc);
		Status("Ожидание входа на сайте Microsoft...");
		int interval = Math.Max(1, dc.Interval);
		DateTime deadline = DateTime.UtcNow.AddSeconds(dc.ExpiresIn);
		while (DateTime.UtcNow < deadline)
		{
			await Task.Delay(TimeSpan.FromSeconds(interval), ct);
			HttpResponseMessage r;
			try
			{
				r = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
				{
					{ "client_id", ClientId.Trim() },
					{ "grant_type", "urn:ietf:params:oauth:grant-type:device_code" },
					{ "device_code", deviceCode }
				}), ct);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				continue;
			}
			string s = await r.Content.ReadAsStringAsync();
			object obj = null;
			try
			{
				obj = Json.Parse(s);
			}
			catch
			{
			}
			if (r.IsSuccessStatusCode && obj != null)
			{
				return await FinishWithMsToken(obj, ct);
			}
			string error = obj?.Str("error");
			if (error == "authorization_pending")
			{
				continue;
			}
			if (error == "slow_down")
			{
				interval += 5;
				continue;
			}
			if (string.IsNullOrEmpty(error) && !r.IsSuccessStatusCode)
			{
				throw new AuthException("Microsoft отклонил вход (HTTP " + (int)r.StatusCode + ").");
			}
			throw new AuthException(OAuthErrors.Describe(error, obj?.Str("error_description"), (int)r.StatusCode));
		}
		throw new AuthException("Код устарел (15 минут). Нажмите «Войти» ещё раз.");
	}

	public async Task<MinecraftSession> Refresh(string refreshToken, CancellationToken ct)
	{
		RequireClientId();
		Status("Обновление входа...");
		object tok;
		try
		{
			tok = await PostForm(TokenUrl, new Dictionary<string, string>
			{
				{ "client_id", ClientId.Trim() },
				{ "scope", Scope },
				{ "refresh_token", refreshToken },
				{ "grant_type", "refresh_token" }
			}, ct);
		}
		catch (AuthException ex)
		{
			throw new AuthException(new OAuthError
			{
				Short = "Нужно войти заново",
				Title = "Сессия Microsoft больше не действует",
				Detail = ex.Message + "\n\n" + (ex.Info?.Detail ?? ""),
				Fix = ex.Info?.Fix ?? new List<string> { "Нажмите «Войти через Microsoft» и подтвердите вход в браузере." },
				LinkText = ex.Info?.LinkText,
				LinkUrl = ex.Info?.LinkUrl,
				NeedsRelogin = true
			});
		}
		return await FinishWithMsToken(tok, ct);
	}

	private async Task<MinecraftSession> FinishWithMsToken(object tok, CancellationToken ct)
	{
		string msToken = tok.Str("access_token");
		string refresh = tok.Str("refresh_token");
		if (string.IsNullOrEmpty(msToken))
		{
			throw new AuthException(new OAuthError
			{
				Short = "Microsoft не выдал токен",
				Title = "Microsoft не дал доступ",
				Detail = "Удалось войти в аккаунт, но токен Xbox Live не выдан. Обычно помогает повторный вход.",
				Fix = { "Нажмите «Войти через Microsoft» ещё раз." },
				NeedsRelogin = true
			});
		}
		Status("Вход в Xbox Live...");
		object xbl = await PostJson(XblUrl, new Dictionary<string, object>
		{
			{
				"Properties",
				new Dictionary<string, object>
				{
					{ "AuthMethod", "RPS" },
					{ "SiteName", "user.auth.xboxlive.com" },
					{
						"RpsTicket",
						"d=" + msToken
					}
				}
			},
			{ "RelyingParty", "http://auth.xboxlive.com" },
			{ "TokenType", "JWT" }
		}, null, ct, XboxError);
		string userToken = xbl.Str("Token");
		string uhs = (from x in xbl.Obj("DisplayClaims").Arr("xui")
			select x.Str("uhs")).FirstOrDefault();
		Status("Получаем доступ к Minecraft...");
		object xsts = await PostJson(XstsUrl, new Dictionary<string, object>
		{
			{
				"Properties",
				new Dictionary<string, object>
				{
					{ "SandboxId", "RETAIL" },
					{
						"UserTokens",
						new List<object> { userToken }
					}
				}
			},
			{ "RelyingParty", "rp://api.minecraftservices.com/" },
			{ "TokenType", "JWT" }
		}, null, ct, XboxError);
		string xstsToken = xsts.Str("Token");
		string xstsUhs = (from x in xsts.Obj("DisplayClaims").Arr("xui")
			select x.Str("uhs")).FirstOrDefault() ?? uhs;
		Status("Проверка лицензии Minecraft...");
		object mc = await PostJson(McLoginUrl, new Dictionary<string, object>
		{
			{
				"identityToken",
				"XBL3.0 x=" + xstsUhs + ";" + xstsToken
			}
		}, null, ct, McError);
		string mcToken = mc.Str("access_token");
		long expires = mc.Long("expires_in");
		if (string.IsNullOrEmpty(mcToken))
		{
			throw new AuthException("Сервер Minecraft не выдал токен. Попробуйте ещё раз.");
		}
		bool owns = (await GetJson(EntitlementsUrl, mcToken, ct, McError)).Arr("items").Any((object i) =>
		{
			string name = i.Str("name");
			return name == "product_minecraft" || name == "game_minecraft";
		});
		Status("Загрузка профиля...");
		object profile;
		try
		{
			profile = await GetJson(ProfileUrl, mcToken, ct, McError);
		}
		catch (AuthException) when (!owns)
		{
			throw new AuthException(OAuthErrors.NoLicense());
		}
		string uuid = profile.Str("id");
		string name2 = profile.Str("name");
		if (string.IsNullOrEmpty(uuid))
		{
			throw new AuthException(owns ? new OAuthError
			{
				Short = "У аккаунта нет профиля Minecraft",
				Title = "Нужно создать профиль Minecraft",
				Detail = "Лицензия есть, но профиль (ник) ещё не создан. Откройте minecraft.net один раз и создайте его.",
				LinkText = "Открыть minecraft.net",
				LinkUrl = "https://www.minecraft.net/"
			} : OAuthErrors.NoLicense());
		}
		string skinUrl = (from s in profile.Arr("skins")
			where s.Str("state") == "ACTIVE"
			select s.Str("url")).FirstOrDefault();
		Status("Авторизация успешна!");
		return new MinecraftSession
		{
			Uuid = uuid,
			Name = name2,
			AccessToken = mcToken,
			Xuid = "0",
			SkinUrl = skinUrl,
			ExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60L, expires - 60)),
			RefreshToken = refresh
		};
	}

	private static string XboxError(int status, string body)
	{
		try
		{
			switch (Json.Parse(body).Long("XErr"))
			{
			case 2148916233L:
				return "У этого аккаунта Microsoft нет профиля Xbox. Зайдите на xbox.com и создайте его.";
			case 2148916235L:
				return "Xbox Live недоступен в вашей стране.";
			case 2148916236L:
			case 2148916237L:
				return "Аккаунт требует подтверждения взрослого (Южная Корея).";
			case 2148916238L:
				return "Это детский аккаунт — его нужно добавить в семью Microsoft взрослым.";
			}
		}
		catch
		{
		}
		return "Xbox Live отклонил вход (код " + status + ").";
	}

	private static string McError(int status, string body)
	{
		if (status == 403 && body.IndexOf("Invalid app registration", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "Mojang ещё не одобрил это приложение (Client ID). Отправьте его на aka.ms/mce-reviewappid и дождитесь ответа.";
		}
		return status switch
		{
			404 => "Профиль Minecraft не найден — на аккаунте нет лицензии Java Edition.", 
			429 => "Слишком много попыток входа. Подождите пару минут.", 
			401 => "Сервер Minecraft не принял вход. Попробуйте снова.", 
			_ => "Сервер Minecraft вернул ошибку " + status + ".", 
		};
	}

	private async Task<object> PostForm(string url, Dictionary<string, string> form, CancellationToken ct)
	{
		HttpResponseMessage r;
		try
		{
			r = await http.PostAsync(url, new FormUrlEncodedContent(form), ct);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			throw new AuthException(new OAuthError
			{
				Short = "Нет связи с Microsoft",
				Title = "Microsoft не отвечает",
				Detail = ex.Message + "\n\nПроверьте интернет, антивирус или VPN — запрос к login.microsoftonline.com не прошёл."
			});
		}
		string s = await r.Content.ReadAsStringAsync();
		object obj = null;
		try
		{
			obj = Json.Parse(s);
		}
		catch
		{
		}
		if (!r.IsSuccessStatusCode)
		{
			throw new AuthException(OAuthErrors.Describe(obj?.Str("error"), obj?.Str("error_description"), (int)r.StatusCode));
		}
		return obj;
	}

	private async Task<object> PostJson(string url, object payload, string bearer, CancellationToken ct, Func<int, string, string> errorText)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new StringContent(Json.Write(payload), Encoding.UTF8, "application/json")
		};
		httpRequestMessage.Headers.Accept.ParseAdd("application/json");
		if (bearer != null)
		{
			httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
		}
		return await Send(httpRequestMessage, ct, errorText);
	}

	private async Task<object> GetJson(string url, string bearer, CancellationToken ct, Func<int, string, string> errorText)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url);
		httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
		return await Send(httpRequestMessage, ct, errorText);
	}

	private async Task<object> Send(HttpRequestMessage req, CancellationToken ct, Func<int, string, string> errorText)
	{
		HttpResponseMessage r;
		try
		{
			r = await http.SendAsync(req, ct);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			throw new AuthException("Нет связи с сервером: " + ex.Message);
		}
		string text = await r.Content.ReadAsStringAsync();
		if (!r.IsSuccessStatusCode)
		{
			string msg = errorText((int)r.StatusCode, text);
			OAuthError e = new OAuthError
			{
				Short = FirstLine(msg),
				Title = "Сервер Minecraft или Xbox Live отклонил вход",
				Detail = msg,
				Fix = { "Повторите вход через минуту." }
			};
			if (msg.IndexOf("mce-reviewappid", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				e.Title = "Приложение ещё не одобрено Mojang";
				e.Detail = msg + "\n\nЭто последний шаг настройки: идентификатор приложения нужно отправить на проверку Mojang. Обычно ответ приходит за несколько дней.";
				e.Fix.Clear();
				e.Fix.Add("Откройте aka.ms/mce-reviewappid и отправьте свой Client ID.");
				e.Fix.Add("Дождитесь письма об одобрении — после этого вход заработает.");
				e.LinkText = "Открыть форму заявки Mojang";
				e.LinkUrl = OAuthErrors.MojangReview;
			}
			else if (msg.IndexOf("лицензии", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				e = OAuthErrors.NoLicense();
			}
			throw new AuthException(e);
		}
		try
		{
			return Json.Parse(text);
		}
		catch
		{
			throw new AuthException("Сервер вернул непонятный ответ.");
		}
	}

	private static string FirstLine(string s)
	{
		int i = s.IndexOf('\n');
		return (i < 0 ? s : s.Substring(0, i)).Trim();
	}

	public async Task<byte[]> Download(string url, CancellationToken ct)
	{
		return await http.GetByteArrayAsync(url);
	}
}
