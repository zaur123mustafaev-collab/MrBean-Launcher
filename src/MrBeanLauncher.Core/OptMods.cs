using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrBeanLauncher.Core;

public class OptMods
{
	public static readonly string[] Slugs = new string[10] { "sodium", "lithium", "ferrite-core", "immediatelyfast", "entityculling", "moreculling", "modernfix", "dynamic-fps", "sodium-extra", "reeses-sodium-options" };

	private const string Api = "https://api.modrinth.com/v2";

	private const string FabricApiId = "P7dR8mSH";

	private static readonly HashSet<string> SodiumFamily = new HashSet<string> { "sodium", "sodium-extra", "reeses-sodium-options", "immediatelyfast" };

	private readonly HttpClient http;

	private readonly string modsDir;

	private readonly string manifestFile;

	public Action<string> Log = delegate
	{
	};

	public Action<double, string> Progress = delegate
	{
	};

	public OptMods(string modsDir, string cacheDir)
	{
		this.modsDir = modsDir;
		manifestFile = Path.Combine(cacheDir, "optmods.json");
		http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(60.0)
		};
		http.DefaultRequestHeaders.UserAgent.ParseAdd("MrBeanLauncher/2.0 (MrBean Visuals)");
	}

	private static string Hex(byte[] b)
	{
		StringBuilder stringBuilder = new StringBuilder(b.Length * 2);
		foreach (byte b2 in b)
		{
			stringBuilder.Append(b2.ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	private static string Sha1(string f)
	{
		using SHA1 sHA = SHA1.Create();
		using FileStream inputStream = File.OpenRead(f);
		return Hex(sHA.ComputeHash(inputStream));
	}

	private static string Sha512(string f)
	{
		using SHA512 sHA = SHA512.Create();
		using FileStream inputStream = File.OpenRead(f);
		return Hex(sHA.ComputeHash(inputStream));
	}

	private async Task<object> Get(string url, CancellationToken ct)
	{
		HttpResponseMessage httpResponseMessage = await http.GetAsync(url, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		httpResponseMessage.EnsureSuccessStatusCode();
		return Json.Parse(await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	private Dictionary<string, string> LoadManifest()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		try
		{
			if (File.Exists(manifestFile))
			{
				foreach (KeyValuePair<string, object> item in Json.Parse(File.ReadAllText(manifestFile)).Obj("installed"))
				{
					dictionary[item.Key] = Convert.ToString(item.Value);
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	private void SaveManifest(Dictionary<string, string> m, DateTime checkedUtc)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(manifestFile));
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			foreach (KeyValuePair<string, string> item in m)
			{
				dictionary[item.Key] = item.Value;
			}
			File.WriteAllText(manifestFile, Json.Write(new Dictionary<string, object>
			{
				{ "checked", checkedUtc.Ticks },
				{ "installed", dictionary }
			}, 2));
		}
		catch
		{
		}
	}

	public bool CheckDue()
	{
		try
		{
			if (!File.Exists(manifestFile))
			{
				return true;
			}
			object o = Json.Parse(File.ReadAllText(manifestFile));
			if (LoadManifest().Values.Any((string f) => !File.Exists(Path.Combine(modsDir, f))))
			{
				return true;
			}
			return DateTime.UtcNow - new DateTime(o.Long("checked"), DateTimeKind.Utc) > TimeSpan.FromHours(12.0);
		}
		catch
		{
			return true;
		}
	}

	public async Task Run(string mc, double from, double to, CancellationToken ct)
	{
		Directory.CreateDirectory(modsDir);
		Dictionary<string, string> manifest = LoadManifest();
		string[] files = Directory.GetFiles(modsDir, "*.jar");
		bool optifine = files.Any((string f) => Path.GetFileName(f).ToLowerInvariant().Contains("optifine") || Path.GetFileName(f).ToLowerInvariant().Contains("optifabric"));
		Dictionary<string, string> present = new Dictionary<string, string>();
		try
		{
			Dictionary<string, string> byHash = files.ToDictionary((string f) => Sha1(f), (string f) => Path.GetFileName(f));
			StringContent content = new StringContent(Json.Write(new Dictionary<string, object>
			{
				{
					"hashes",
					byHash.Keys.Cast<object>().ToList()
				},
				{ "algorithm", "sha1" }
			}), Encoding.UTF8, "application/json");
			HttpResponseMessage httpResponseMessage = await http.PostAsync("https://api.modrinth.com/v2/version_files", content, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (httpResponseMessage.IsSuccessStatusCode && Json.Parse(await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false)) is Dictionary<string, object> dictionary)
			{
				foreach (KeyValuePair<string, object> item in dictionary)
				{
					present[item.Value.Str("project_id")] = byHash[item.Key];
				}
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			Log("Моды оптимизации: Modrinth недоступен (" + ex2.Message + ") — пропускаю");
			return;
		}
		string gv = Uri.EscapeDataString("[\"" + mc + "\"]");
		string lv = Uri.EscapeDataString("[\"fabric\"]");
		Queue<string> queue = new Queue<string>(Slugs);
		HashSet<string> seen = new HashSet<string>();
		int total = Slugs.Length;
		int n = 0;
		int added = 0;
		int updated = 0;
		while (queue.Count > 0)
		{
			ct.ThrowIfCancellationRequested();
			string text = queue.Dequeue();
			if (!seen.Add(text))
			{
				continue;
			}
			string label = ((Array.IndexOf(Slugs, text) >= 0) ? text : "зависимость");
			Progress(from + (to - from) * Math.Min(1.0, (double)n++ / (double)total), "Моды оптимизации: " + label);
			if (optifine && SodiumFamily.Contains(text))
			{
				Log("  " + label + ": пропущен — стоит OptiFine/OptiFabric (несовместимо)");
				continue;
			}
			try
			{
				if (!(await Get("https://api.modrinth.com/v2/project/" + text + "/version?game_versions=" + gv + "&loaders=" + lv, ct).ConfigureAwait(continueOnCapturedContext: false) is List<object> { Count: not 0 } list))
				{
					Log("  " + label + ": нет версии для " + mc + " — пропускаю");
					continue;
				}
				object v = list.FirstOrDefault((object x) => x.Str("version_type") == "release") ?? list[0];
				string pid = v.Str("project_id");
				seen.Add(pid);
				if (v.Arr("dependencies").Any((object o) => o.Str("dependency_type") == "incompatible" && o.Str("project_id") != null && present.ContainsKey(o.Str("project_id"))))
				{
					Log("  " + label + ": несовместим с уже установленным модом — пропускаю");
					continue;
				}
				object obj = v.Arr("files").FirstOrDefault((object f) => f.Get("primary") is bool && (bool)f.Get("primary")) ?? v.Arr("files").FirstOrDefault();
				if (obj == null)
				{
					continue;
				}
				string fname = obj.Str("filename");
				string sha512 = obj.Obj("hashes").Str("sha512");
				string text2 = obj.Str("url");
				if (text2 == null || !text2.StartsWith("https://cdn.modrinth.com/"))
				{
					Log("  " + label + ": ссылка не с cdn.modrinth.com — пропускаю");
					continue;
				}
				if (!present.TryGetValue(pid, out var have))
				{
					goto IL_0754;
				}
				manifest.TryGetValue(pid, out var value);
				if (!(have == fname) && !(value != have))
				{
					goto IL_0754;
				}
				QueueDeps(v, present, queue);
				continue;
				IL_0754:
				string target = Path.Combine(modsDir, fname);
				string tmp = target + ".part";
				using (HttpResponseMessage r = await http.GetAsync(text2, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false))
				{
					r.EnsureSuccessStatusCode();
					using Stream s = await r.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false);
					using FileStream d = File.Create(tmp);
					await s.CopyToAsync(d, 81920, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				if (sha512 == null || !string.Equals(Sha512(tmp), sha512, StringComparison.OrdinalIgnoreCase))
				{
					File.Delete(tmp);
					Log("  " + label + ": файл не прошёл проверку SHA-512 — удалён");
					continue;
				}
				if (File.Exists(target))
				{
					File.Delete(target);
				}
				File.Move(tmp, target);
				if (have != null && have != fname)
				{
					try
					{
						File.Delete(Path.Combine(modsDir, have));
					}
					catch
					{
					}
					updated++;
					Log("  " + label + ": обновлён → " + fname);
				}
				else
				{
					added++;
					Log("  " + label + ": установлен " + fname);
				}
				present[pid] = fname;
				manifest[pid] = fname;
				QueueDeps(v, present, queue);
				have = null;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex4)
			{
				Log("  " + label + ": ошибка — " + ex4.Message);
			}
		}
		SaveManifest(manifest, DateTime.UtcNow);
		Log("Моды оптимизации: новых " + added + ", обновлено " + updated + ", всего на месте " + Enumerable.Count(manifest, (KeyValuePair<string, string> kv) => File.Exists(Path.Combine(modsDir, kv.Value))));
		Progress(to, "Моды оптимизации — готово");
	}

	private static void QueueDeps(object v, Dictionary<string, string> present, Queue<string> q)
	{
		foreach (object item in v.Arr("dependencies"))
		{
			string text = item.Str("project_id");
			if (item.Str("dependency_type") == "required" && text != null && text != "P7dR8mSH" && !present.ContainsKey(text))
			{
				q.Enqueue(text);
			}
		}
	}

	public int RemoveOurs()
	{
		Dictionary<string, string> dictionary = LoadManifest();
		int num = 0;
		foreach (string value in dictionary.Values)
		{
			string path = Path.Combine(modsDir, value);
			if (File.Exists(path))
			{
				File.Delete(path);
				num++;
			}
		}
		SaveManifest(new Dictionary<string, string>(), DateTime.MinValue);
		return num;
	}
}
