using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrBeanLauncher.Core;

/// <summary>
/// Одноразовый локальный HTTP-приёмник для OAuth: Microsoft возвращает сюда код входа
/// на http://localhost:PORT. Слушаем только 127.0.0.1, закрываемся после первого ответа.
/// </summary>
public class LoopbackServer : IDisposable
{
	private readonly TcpListener listener;

	public int Port { get; private set; }

	public string RedirectUri => "http://localhost:" + Port;

	public LoopbackServer(int preferredPort)
	{
		int[] tryPorts = (preferredPort > 0 && preferredPort < 65535)
			? new int[2] { preferredPort, 0 }
			: new int[1] { 0 };
		foreach (int port in tryPorts)
		{
			TcpListener l = new TcpListener(IPAddress.Loopback, port);
			try
			{
				l.Start();
				listener = l;
				Port = ((IPEndPoint)l.LocalEndpoint).Port;
				return;
			}
			catch
			{
				try
				{
					l.Stop();
				}
				catch
				{
				}
			}
		}
		throw new AuthException(new OAuthError
		{
			Short = "Локальный порт занят",
			Title = "Лаунчер не смог занять порт для приёма входа",
			Detail = "Другая программа мешает лаунчеру принять ответ Microsoft на http://localhost. Вход по коду работает без локального порта.",
			SuggestCode = true
		});
	}

	/// <summary>Ждёт запроса с code= или error=. Прочие запросы (favicon, пинг) просто закрывает.</summary>
	public async Task<Dictionary<string, string>> WaitForQueryAsync(TimeSpan timeout, CancellationToken ct)
	{
		Task<Dictionary<string, string>> wait = WaitInnerAsync(ct);
		if (await Task.WhenAny(wait, Task.Delay(timeout)) != wait)
		{
			throw new AuthException(new OAuthError
			{
				Short = "Браузер не передал вход лаунчеру",
				Title = "Не дождались ответа от Microsoft",
				Detail = "Страница входа не смогла вернуться на адрес лаунчера: " + RedirectUri
					+ ". Часто мешают антивирус, прокси или VPN. Можно войти по коду — там лаунчер сам опрашивает Microsoft.",
				SuggestCode = true
			});
		}
		return await wait;
	}

	private async Task<Dictionary<string, string>> WaitInnerAsync(CancellationToken ct)
	{
		while (true)
		{
			ct.ThrowIfCancellationRequested();
			TcpClient client;
			try
			{
				client = await listener.AcceptTcpClientAsync();
			}
			catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException)
			{
				ct.ThrowIfCancellationRequested();
				throw new AuthException(new OAuthError
				{
					Short = "Приём входа прервался",
					Title = "Локальный порт лаунчера закрылся",
					Detail = "Соединение с браузером оборвалось до того, как Microsoft передал код. Попробуйте ещё раз или войдите по коду.",
					SuggestCode = true
				});
			}
			using (client)
			{
				using NetworkStream stream = client.GetStream();
				string line = await ReadRequestLineAsync(stream, ct);
				if (line == null)
				{
					continue;
				}
				string[] parts = line.Split(' ');
				if (parts.Length < 2 || parts[0] != "GET")
				{
					continue;
				}
				string path = parts[1];
				if (path.StartsWith("/favicon"))
				{
					await SendAsync(stream, 404, "text/plain; charset=utf-8", Array.Empty<byte>(), ct);
					continue;
				}
				Dictionary<string, string> q = ParseQuery(path);
				if (!q.ContainsKey("code") && !q.ContainsKey("error"))
				{
					await SendAsync(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page("MrBean Launcher", "Страница открыта слишком рано. Вернитесь в лаунчер.", isOk: false)), ct);
					continue;
				}
				string title = (q.ContainsKey("code") ? "Вход выполнен" : "Вход не выполнен");
				string sub = (q.ContainsKey("code") ? "Можно закрыть эту вкладку и вернуться в MrBean Launcher." : "Вернитесь в MrBean Launcher — там объяснение, что делать дальше.");
				await SendAsync(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page(title, sub, q.ContainsKey("code"))), ct);
				return q;
			}
		}
	}

	private static async Task<string> ReadRequestLineAsync(NetworkStream s, CancellationToken ct)
	{
		byte[] buf = new byte[8192];
		int total = 0;
		while (total < buf.Length)
		{
			int n;
			try
			{
				n = await s.ReadAsync(buf, total, buf.Length - total, ct);
			}
			catch (IOException)
			{
				break;
			}
			if (n <= 0)
			{
				break;
			}
			total += n;
			int idx = Encoding.ASCII.GetString(buf, 0, total).IndexOf("\r\n", StringComparison.Ordinal);
			if (idx >= 0)
			{
				return Encoding.ASCII.GetString(buf, 0, idx);
			}
		}
		return null;
	}

	private static async Task SendAsync(NetworkStream s, int status, string contentType, byte[] body, CancellationToken ct)
	{
		string head = "HTTP/1.1 " + status + ((status == 200) ? " OK" : " Not Found") + "\r\n"
			+ "Content-Type: " + contentType + "\r\n"
			+ "Content-Length: " + body.Length + "\r\n"
			+ "Cache-Control: no-store\r\n"
			+ "Connection: close\r\n\r\n";
		await s.WriteAsync(Encoding.ASCII.GetBytes(head), 0, head.Length, ct);
		if (body.Length > 0)
		{
			await s.WriteAsync(body, 0, body.Length, ct);
		}
		await s.FlushAsync(ct);
	}

	public static string Page(string title, string sub, bool isOk)
	{
		string color = isOk ? "#7CE63C" : "#FF5660";
		string glyph = isOk ? "&#10003;" : "!";
		return "<!doctype html><html lang='ru'><head><meta charset='utf-8'><title>" + Esc(title)
			+ "</title><style>body{background:#0c0d12;color:#eef2f8;font-family:'Segoe UI',sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}"
			+ "div{background:#161821;border:1px solid #282c3a;border-radius:18px;padding:38px 54px;text-align:center;max-width:520px}"
			+ "span{display:inline-flex;align-items:center;justify-content:center;width:54px;height:54px;border-radius:50%;border:2px solid " + color + ";color:" + color + ";font-size:26px;font-weight:700;margin-bottom:14px}"
			+ "h1{font-size:22px;margin:0 0 8px}p{color:#8a91a5;font-size:14px;margin:0}</style></head><body><div><span>" + glyph + "</span><h1>" + Esc(title) + "</h1><p>" + Esc(sub) + "</p></div></body></html>";
	}

	private static string Esc(string s)
	{
		return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
	}

	public static Dictionary<string, string> ParseQuery(string path)
	{
		Dictionary<string, string> q = new Dictionary<string, string>();
		int i = path.IndexOf('?');
		if (i < 0)
		{
			return q;
		}
		foreach (string pair in path.Substring(i + 1).Split('&'))
		{
			if (pair.Length == 0)
			{
				continue;
			}
			string[] kv = pair.Split(new char[1] { '=' }, 2);
			q[Uri.UnescapeDataString(kv[0])] = (kv.Length == 2) ? Uri.UnescapeDataString(kv[1].Replace('+', ' ')) : "";
		}
		return q;
	}

	public void Dispose()
	{
		try
		{
			listener?.Stop();
		}
		catch
		{
		}
	}
}

internal static class QueryExtensions
{
	public static string GetOrEmpty(this Dictionary<string, string> q, string key)
	{
		return q != null && q.TryGetValue(key, out var v) ? v : "";
	}
}
