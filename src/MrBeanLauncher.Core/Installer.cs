using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrBeanLauncher.Core;

public class Installer
{
	public class Item
	{
		public string Url;

		public string Path;

		public long Size;
	}

	public const string MC = "1.21.11";

	public const string FabricApiFallbackUrl = "https://cdn.modrinth.com/data/P7dR8mSH/versions/6qAuTtLR/fabric-api-0.141.6%2B1.21.11.jar";

	public const string FabricApiFallbackName = "fabric-api-0.141.6+1.21.11.jar";

	public readonly string Root;

	public readonly string GameDir;

	public readonly string LibDir;

	public readonly string AssetsDir;

	public readonly string VersionsDir;

	public readonly string RuntimeDir;

	public readonly string NativesDir;

	public readonly string ModsDir;

	public readonly string CacheDir;

	public Action<double, string> Progress = delegate
	{
	};

	public Action<string> Log = delegate
	{
	};

	public int Parallel = 16;

	public bool ForceJavaCheck;

	public Func<Stream> BundledModStream;

	public string BundledModName = "MrBeanVisuals-2.1.0.jar";

	public bool WithVisuals = true;

	public bool RussianLang = true;

	public bool OptimizationMods = true;

	private readonly HttpClient http;

	private long bytesDone;

	public long BytesDownloaded => Interlocked.Read(ref bytesDone);

	public Installer(string root)
	{
		Root = root;
		GameDir = Path.Combine(root, "game");
		LibDir = Path.Combine(GameDir, "libraries");
		AssetsDir = Path.Combine(GameDir, "assets");
		VersionsDir = Path.Combine(GameDir, "versions");
		RuntimeDir = Path.Combine(root, "runtime");
		NativesDir = Path.Combine(GameDir, "natives", "1.21.11");
		ModsDir = Path.Combine(GameDir, "mods");
		CacheDir = Path.Combine(root, "cache");
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		}
		catch
		{
		}
		ServicePointManager.DefaultConnectionLimit = 64;
		http = new HttpClient
		{
			Timeout = TimeSpan.FromMinutes(10.0)
		};
		http.DefaultRequestHeaders.UserAgent.ParseAdd("MrBeanLauncher/2.0");
	}

	private static void Dir(string p)
	{
		if (!string.IsNullOrEmpty(p))
		{
			Directory.CreateDirectory(p);
		}
	}

	private async Task<string> GetText(string url, CancellationToken ct)
	{
		Exception last = null;
		for (int i = 0; i < 3; i++)
		{
			try
			{
				HttpResponseMessage httpResponseMessage = await http.GetAsync(url, ct).ConfigureAwait(continueOnCapturedContext: false);
				httpResponseMessage.EnsureSuccessStatusCode();
				byte[] bytes = await httpResponseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(continueOnCapturedContext: false);
				return Encoding.UTF8.GetString(bytes);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex2)
			{
				last = ex2;
				await Task.Delay(1500, ct).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		throw new Exception("Не удалось открыть " + url + ": " + ((last == null) ? "" : last.Message));
	}

	private async Task<object> GetJson(string url, string cache, CancellationToken ct)
	{
		try
		{
			string text = await GetText(url, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (cache != null)
			{
				Dir(Path.GetDirectoryName(cache));
				File.WriteAllText(cache, text);
			}
			return Json.Parse(text);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			if (cache != null && File.Exists(cache))
			{
				Log("Нет сети — беру из кэша " + Path.GetFileName(cache));
				return Json.Parse(File.ReadAllText(cache));
			}
			throw;
		}
	}

	private async Task Download(List<Item> items, string label, double from, double to, CancellationToken ct)
	{
		List<Item> todo = items.Where((Item it) => !File.Exists(it.Path) || (it.Size > 0 && new FileInfo(it.Path).Length != it.Size)).ToList();
		if (todo.Count == 0)
		{
			Progress(to, label + " — готово");
			return;
		}
		Log(label + ": скачиваю " + todo.Count + " файлов");
		int done = 0;
		SemaphoreSlim sem = new SemaphoreSlim(Parallel);
		await Task.WhenAll(((IEnumerable<Item>)todo).Select((Func<Item, Task>)(async (Item it) =>
		{
			await sem.WaitAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				int attempt = 1;
				while (true)
				{
					try
					{
						using HttpResponseMessage r = await http.GetAsync(it.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false);
						r.EnsureSuccessStatusCode();
						Dir(Path.GetDirectoryName(it.Path));
						string tmp = it.Path + ".part";
						using (Stream src = await r.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false))
						{
							using FileStream dst = File.Create(tmp);
							byte[] buf = new byte[81920];
							while (true)
							{
								int num;
								int n = (num = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(continueOnCapturedContext: false));
								if (num <= 0)
								{
									break;
								}
								await dst.WriteAsync(buf, 0, n, ct).ConfigureAwait(continueOnCapturedContext: false);
								Interlocked.Add(ref bytesDone, n);
							}
						}
						if (File.Exists(it.Path))
						{
							File.Delete(it.Path);
						}
						File.Move(tmp, it.Path);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception ex2)
					{
						if (attempt >= 4)
						{
							throw new Exception("Не удалось скачать " + it.Url + ": " + ex2.Message);
						}
						await Task.Delay(1000 * attempt, ct).ConfigureAwait(continueOnCapturedContext: false);
						goto IL_04d5;
					}
					break;
					IL_04d5:
					attempt++;
				}
				int num2 = Interlocked.Increment(ref done);
				Progress(from + (to - from) * (double)num2 / (double)todo.Count, $"{label}: {num2} / {todo.Count}");
			}
			finally
			{
				sem.Release();
			}
		})).ToList()).ConfigureAwait(continueOnCapturedContext: false);
		Progress(to, label + " — готово");
	}

	private static bool Rules(object lib)
	{
		List<object> list = lib.Arr("rules");
		if (list.Count == 0)
		{
			return true;
		}
		bool result = false;
		foreach (object item in list)
		{
			bool flag = true;
			if (item.Get("features") != null)
			{
				flag = false;
			}
			object obj = item.Get("os");
			if (obj != null)
			{
				string text = obj.Str("name");
				string text2 = obj.Str("arch");
				if (text != null && text != "windows")
				{
					flag = false;
				}
				if (text2 != null && text2 != "x64")
				{
					flag = false;
				}
			}
			if (flag)
			{
				result = item.Str("action") == "allow";
			}
		}
		return result;
	}

	private static string MavenPath(string coord)
	{
		string[] array = coord.Split(':');
		string text = array[1] + "-" + array[2] + ((array.Length >= 4) ? ("-" + array[3]) : "") + ".jar";
		return string.Join("/", array[0].Replace('.', '/'), array[1], array[2], text);
	}

	private static string LibKey(string coord)
	{
		string[] array = coord.Split(':');
		if (array.Length < 4)
		{
			return array[0] + ":" + array[1];
		}
		return array[0] + ":" + array[1] + ":" + array[3];
	}

	private string Local(string root, string rel)
	{
		return Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
	}

	public string JavaExe(string component, bool console)
	{
		string path = Path.Combine(RuntimeDir, component, "bin");
		if (Path.DirectorySeparatorChar == '/')
		{
			return Path.Combine(path, "java");
		}
		return Path.Combine(path, console ? "java.exe" : "javaw.exe");
	}

	public async Task<GameInfo> Install(CancellationToken ct)
	{
		string[] array = new string[7] { GameDir, LibDir, AssetsDir, VersionsDir, ModsDir, NativesDir, CacheDir };
		for (int i = 0; i < array.Length; i++)
		{
			Dir(array[i]);
		}
		bytesDone = 0L;
		Progress(1.0, "Получаю данные версии 1.21.11...");
		string verPath = Path.Combine(VersionsDir, "1.21.11", "1.21.11.json");
		object ver;
		try
		{
			object o = (await GetJson("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", Path.Combine(CacheDir, "version_manifest.json"), ct)).Arr("versions").First((object v) => v.Str("id") == "1.21.11");
			ver = await GetJson(o.Str("url"), verPath, ct);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			if (!File.Exists(verPath))
			{
				throw;
			}
			ver = Json.Parse(File.ReadAllText(verPath));
		}
		string component = ver.Obj("javaVersion").Str("component") ?? "java-runtime-delta";
		string javaRoot = Path.Combine(RuntimeDir, component);
		string marker = Path.Combine(javaRoot, ".installed");
		if (!File.Exists(marker) || ForceJavaCheck)
		{
			Progress(3.0, "Скачиваю Java " + ver.Obj("javaVersion").Long("majorVersion") + "...");
			object rt = (await GetJson("https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json", null, ct)).Obj("windows-x64").Arr(component)[0];
			object o2 = await GetJson(rt.Obj("manifest").Str("url"), null, ct);
			List<Item> list = new List<Item>();
			foreach (KeyValuePair<string, object> item in o2.Obj("files"))
			{
				string text = Local(javaRoot, item.Key);
				string text2 = item.Value.Str("type");
				if (text2 == "directory")
				{
					Dir(text);
				}
				else if (!(text2 != "file"))
				{
					Dictionary<string, object> o3 = item.Value.Obj("downloads").Obj("raw");
					list.Add(new Item
					{
						Url = o3.Str("url"),
						Path = text,
						Size = o3.Long("size")
					});
				}
			}
			await Download(list, "Java", 3.0, 15.0, ct);
			File.WriteAllText(marker, rt.Obj("version").Str("name") ?? "ok");
		}
		Progress(15.0, "Java готова");
		Dictionary<string, object> o4 = ver.Obj("downloads").Obj("client");
		string clientJar = Path.Combine(VersionsDir, "1.21.11", "1.21.11.jar");
		await Download(new List<Item>
		{
			new Item
			{
				Url = o4.Str("url"),
				Path = clientJar,
				Size = o4.Long("size")
			}
		}, "Minecraft 1.21.11", 15.0, 20.0, ct);
		Dictionary<string, string> libs = new Dictionary<string, string>();
		List<string> order = new List<string>();
		List<Item> libItems = new List<Item>();
		List<string> nativeJars = new List<string>();
		foreach (object item2 in ver.Arr("libraries"))
		{
			if (!Rules(item2))
			{
				continue;
			}
			string text3 = item2.Str("name");
			if (text3.EndsWith(":natives-windows-arm64") || text3.EndsWith(":natives-windows-x86"))
			{
				continue;
			}
			Dictionary<string, object> dictionary = item2.Obj("downloads").Obj("artifact");
			if (dictionary.Count != 0)
			{
				string text4 = Local(LibDir, dictionary.Str("path"));
				libItems.Add(new Item
				{
					Url = dictionary.Str("url"),
					Path = text4,
					Size = dictionary.Long("size")
				});
				string text5 = LibKey(text3);
				if (!libs.ContainsKey(text5))
				{
					order.Add(text5);
				}
				libs[text5] = text4;
				if (text3.EndsWith(":natives-windows"))
				{
					nativeJars.Add(text4);
				}
			}
		}
		Progress(20.0, "Получаю Fabric...");
		List<object> list2 = (await GetJson("https://meta.fabricmc.net/v2/versions/loader/1.21.11", Path.Combine(CacheDir, "fabric-loaders.json"), ct)) as List<object>;
		object o5 = list2.FirstOrDefault((object x) => x.Obj("loader").Get("stable") is bool && (bool)x.Obj("loader").Get("stable")) ?? list2[0];
		string loader = o5.Obj("loader").Str("version");
		object fabric = await GetJson("https://meta.fabricmc.net/v2/versions/loader/1.21.11/" + loader + "/profile/json", Path.Combine(CacheDir, "fabric-" + loader + ".json"), ct);
		foreach (object item3 in fabric.Arr("libraries"))
		{
			string coord = item3.Str("name");
			string text6 = MavenPath(coord);
			string text7 = item3.Str("url");
			if (!text7.EndsWith("/"))
			{
				text7 += "/";
			}
			string text8 = Local(LibDir, text6);
			libItems.Add(new Item
			{
				Url = text7 + text6,
				Path = text8,
				Size = item3.Long("size")
			});
			string text9 = LibKey(coord);
			if (!libs.ContainsKey(text9))
			{
				order.Add(text9);
			}
			libs[text9] = text8;
		}
		await Download(libItems, "Библиотеки", 20.0, 35.0, ct);
		Progress(35.0, "Распаковываю natives...");
		foreach (string item4 in nativeJars)
		{
			using ZipArchive zipArchive = ZipFile.OpenRead(item4);
			foreach (ZipArchiveEntry entry in zipArchive.Entries)
			{
				if (entry.FullName.StartsWith("META-INF"))
				{
					continue;
				}
				string text10 = entry.Name.ToLowerInvariant();
				if (text10.EndsWith(".dll") || text10.EndsWith(".so") || text10.EndsWith(".dylib"))
				{
					string text11 = Path.Combine(NativesDir, entry.Name);
					if (!File.Exists(text11) || new FileInfo(text11).Length != entry.Length)
					{
						entry.ExtractToFile(text11, overwrite: true);
					}
				}
			}
		}
		Progress(36.0, "Получаю список ресурсов...");
		Dictionary<string, object> ai = ver.Obj("assetIndex");
		object o6 = await GetJson(ai.Str("url"), Path.Combine(AssetsDir, "indexes", ai.Str("id") + ".json"), ct);
		List<Item> list3 = new List<Item>();
		foreach (KeyValuePair<string, object> item5 in o6.Obj("objects"))
		{
			string text12 = item5.Value.Str("hash");
			string text13 = text12.Substring(0, 2);
			list3.Add(new Item
			{
				Url = "https://resources.download.minecraft.net/" + text13 + "/" + text12,
				Path = Path.Combine(AssetsDir, "objects", text13, text12),
				Size = item5.Value.Long("size")
			});
		}
		int oldPar = Parallel;
		Parallel = Math.Max(Parallel, 24);
		await Download(list3, "Ресурсы", 36.0, 92.0, ct);
		Parallel = oldPar;
		Progress(92.0, "Ставлю моды...");
		await InstallMods(ct);
		string path = Path.Combine(GameDir, "options.txt");
		if (!File.Exists(path) && RussianLang)
		{
			File.WriteAllText(path, "lang:ru_ru\n");
		}
		Progress(100.0, "Готово!");
		Log("Установка завершена: Minecraft 1.21.11, Fabric " + loader + ", скачано " + BytesDownloaded / 1048576 + " МБ");
		GameInfo gameInfo = new GameInfo
		{
			Java = JavaExe(component, console: true),
			MainClass = fabric.Str("mainClass"),
			VersionId = fabric.Str("id"),
			AssetIndex = ai.Str("id"),
			LoaderVersion = loader
		};
		foreach (string item6 in order)
		{
			gameInfo.Classpath.Add(libs[item6]);
		}
		gameInfo.Classpath.Add(clientJar);
		foreach (object item7 in fabric.Obj("arguments").Arr("jvm"))
		{
			gameInfo.FabricJvm.Add(Convert.ToString(item7));
		}
		List<string> list4 = gameInfo.Classpath.Where((string p) => !File.Exists(p)).ToList();
		if (list4.Count > 0)
		{
			throw new Exception("Не хватает файла: " + list4[0]);
		}
		if (Path.DirectorySeparatorChar == '\\' && !File.Exists(gameInfo.Java))
		{
			throw new Exception("Не найдена Java: " + gameInfo.Java);
		}
		return gameInfo;
	}

	private async Task InstallMods(CancellationToken ct)
	{
		Dir(ModsDir);
		if (Directory.GetFiles(ModsDir, "fabric-api-*.jar").Length == 0)
		{
			string url = "https://cdn.modrinth.com/data/P7dR8mSH/versions/6qAuTtLR/fabric-api-0.141.6%2B1.21.11.jar";
			string name = "fabric-api-0.141.6+1.21.11.jar";
			try
			{
				string text = Uri.EscapeDataString("[\"1.21.11\"]");
				string text2 = Uri.EscapeDataString("[\"fabric\"]");
				if (await GetJson("https://api.modrinth.com/v2/project/fabric-api/version?game_versions=" + text + "&loaders=" + text2, null, ct) is List<object> { Count: >0 } list)
				{
					object o = list[0].Arr("files").FirstOrDefault((object x) => x.Get("primary") is bool && (bool)x.Get("primary")) ?? list[0].Arr("files")[0];
					url = o.Str("url");
					name = o.Str("filename");
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch
			{
				Log("Modrinth недоступен — беру запасную ссылку Fabric API");
			}
			await Download(new List<Item>
			{
				new Item
				{
					Url = url,
					Path = Path.Combine(ModsDir, name)
				}
			}, "Fabric API", 92.0, 97.0, ct);
		}
		if (OptimizationMods)
		{
			OptMods optMods = new OptMods(ModsDir, CacheDir)
			{
				Log = Log,
				Progress = Progress
			};
			if (optMods.CheckDue())
			{
				Log("Проверяю моды оптимизации на Modrinth...");
				await optMods.Run("1.21.11", 97.0, 99.0, ct);
			}
		}
		string[] files = Directory.GetFiles(ModsDir, "MrBeanVisuals*");
		foreach (string path in files)
		{
			if (Path.GetFileName(path) != BundledModName && Path.GetFileName(path) != BundledModName + ".disabled")
			{
				File.Delete(path);
			}
		}
		string text3 = Path.Combine(ModsDir, BundledModName);
		string text4 = text3 + ".disabled";
		if (BundledModStream != null)
		{
			using Stream stream = BundledModStream();
			if (stream != null)
			{
				string text5 = (WithVisuals ? text3 : text4);
				if (!File.Exists(text5) || new FileInfo(text5).Length != stream.Length)
				{
					using FileStream destination = File.Create(text5);
					stream.CopyTo(destination);
				}
			}
		}
		if (WithVisuals && File.Exists(text4))
		{
			if (File.Exists(text3))
			{
				File.Delete(text4);
			}
			else
			{
				File.Move(text4, text3);
			}
		}
		if (!WithVisuals && File.Exists(text3))
		{
			if (File.Exists(text4))
			{
				File.Delete(text4);
			}
			File.Move(text3, text4);
		}
	}
}
