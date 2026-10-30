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

namespace MrBeanLauncher.Core
{
    public class GameInfo
    {
        public string Java;
        public List<string> Classpath = new List<string>();
        public string MainClass;
        public List<string> FabricJvm = new List<string>();
        public string VersionId;
        public string AssetIndex;
        public string LoaderVersion;
    }

    /// <summary>Скачивает и проверяет Java, Minecraft, Fabric и моды.</summary>
    public class Installer
    {
        public const string MC = "1.21.11";
        public const string FabricApiFallbackUrl = "https://cdn.modrinth.com/data/P7dR8mSH/versions/6qAuTtLR/fabric-api-0.141.6%2B1.21.11.jar";
        public const string FabricApiFallbackName = "fabric-api-0.141.6+1.21.11.jar";

        public readonly string Root, GameDir, LibDir, AssetsDir, VersionsDir, RuntimeDir, NativesDir, ModsDir, CacheDir;
        public Action<double, string> Progress = delegate { };
        public Action<string> Log = delegate { };
        public int Parallel = 16;
        public bool ForceJavaCheck;
        public Func<Stream> BundledModStream;   // jar MrBean Visuals внутри exe
        public string BundledModName = "MrBeanVisuals-2.1.0.jar";
        public bool WithVisuals = true;
        public bool RussianLang = true;
        public bool OptimizationMods = true;

        readonly HttpClient http;
        long bytesDone;
        public long BytesDownloaded { get { return Interlocked.Read(ref bytesDone); } }

        public Installer(string root)
        {
            Root = root;
            GameDir = Path.Combine(root, "game");
            LibDir = Path.Combine(GameDir, "libraries");
            AssetsDir = Path.Combine(GameDir, "assets");
            VersionsDir = Path.Combine(GameDir, "versions");
            RuntimeDir = Path.Combine(root, "runtime");
            NativesDir = Path.Combine(GameDir, "natives", MC);
            ModsDir = Path.Combine(GameDir, "mods");
            CacheDir = Path.Combine(root, "cache");
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
            ServicePointManager.DefaultConnectionLimit = 64;
            http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MrBeanLauncher/2.0");
        }

        static void Dir(string p) { if (!string.IsNullOrEmpty(p)) Directory.CreateDirectory(p); }

        async Task<string> GetText(string url, CancellationToken ct)
        {
            Exception last = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var r = await http.GetAsync(url, ct).ConfigureAwait(false);
                    r.EnsureSuccessStatusCode();
                    var b = await r.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return Encoding.UTF8.GetString(b);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { last = e; await Task.Delay(1500, ct).ConfigureAwait(false); }
            }
            throw new Exception("Не удалось открыть " + url + ": " + (last == null ? "" : last.Message));
        }

        /// <summary>JSON из сети; при ошибке — из кэша.</summary>
        async Task<object> GetJson(string url, string cache, CancellationToken ct)
        {
            try
            {
                string t = await GetText(url, ct).ConfigureAwait(false);
                if (cache != null) { Dir(Path.GetDirectoryName(cache)); File.WriteAllText(cache, t); }
                return Json.Parse(t);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                if (cache != null && File.Exists(cache)) { Log("Нет сети — беру из кэша " + Path.GetFileName(cache)); return Json.Parse(File.ReadAllText(cache)); }
                throw;
            }
        }

        public class Item { public string Url, Path; public long Size; }

        async Task Download(List<Item> items, string label, double from, double to, CancellationToken ct)
        {
            var todo = items.Where(it => !(File.Exists(it.Path) && (it.Size <= 0 || new FileInfo(it.Path).Length == it.Size))).ToList();
            if (todo.Count == 0) { Progress(to, label + " — готово"); return; }
            Log(label + ": скачиваю " + todo.Count + " файлов");
            int done = 0;
            var sem = new SemaphoreSlim(Parallel);
            var tasks = todo.Select(async it =>
            {
                await sem.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    for (int attempt = 1; ; attempt++)
                    {
                        try
                        {
                            using (var r = await http.GetAsync(it.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                            {
                                r.EnsureSuccessStatusCode();
                                Dir(Path.GetDirectoryName(it.Path));
                                string tmp = it.Path + ".part";
                                using (var src = await r.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                using (var dst = File.Create(tmp))
                                {
                                    var buf = new byte[81920]; int n;
                                    while ((n = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                                    {
                                        await dst.WriteAsync(buf, 0, n, ct).ConfigureAwait(false);
                                        Interlocked.Add(ref bytesDone, n);
                                    }
                                }
                                if (File.Exists(it.Path)) File.Delete(it.Path);
                                File.Move(tmp, it.Path);
                            }
                            break;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception e)
                        {
                            if (attempt >= 4) throw new Exception("Не удалось скачать " + it.Url + ": " + e.Message);
                            await Task.Delay(1000 * attempt, ct).ConfigureAwait(false);
                        }
                    }
                    int d = Interlocked.Increment(ref done);
                    Progress(from + (to - from) * d / todo.Count, string.Format("{0}: {1} / {2}", label, d, todo.Count));
                }
                finally { sem.Release(); }
            }).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            Progress(to, label + " — готово");
        }

        static bool Rules(object lib)
        {
            var rules = lib.Arr("rules");
            if (rules.Count == 0) return true;
            bool allowed = false;
            foreach (var r in rules)
            {
                bool match = true;
                if (r.Get("features") != null) match = false;
                var os = r.Get("os");
                if (os != null)
                {
                    var name = os.Str("name"); var arch = os.Str("arch");
                    if (name != null && name != "windows") match = false;
                    if (arch != null && arch != "x64") match = false;
                }
                if (match) allowed = r.Str("action") == "allow";
            }
            return allowed;
        }

        static string MavenPath(string coord)
        {
            var p = coord.Split(':');
            string file = p[1] + "-" + p[2] + (p.Length >= 4 ? "-" + p[3] : "") + ".jar";
            return string.Join("/", p[0].Replace('.', '/'), p[1], p[2], file);
        }

        static string LibKey(string coord)
        {
            var p = coord.Split(':');
            return p.Length >= 4 ? p[0] + ":" + p[1] + ":" + p[3] : p[0] + ":" + p[1];
        }

        string Local(string root, string rel) { return Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)); }

        public string JavaExe(string component, bool console)
        {
            string bin = Path.Combine(RuntimeDir, component, "bin");
            if (Path.DirectorySeparatorChar == '/') return Path.Combine(bin, "java");
            return Path.Combine(bin, console ? "java.exe" : "javaw.exe");
        }

        public async Task<GameInfo> Install(CancellationToken ct)
        {
            foreach (var d in new[] { GameDir, LibDir, AssetsDir, VersionsDir, ModsDir, NativesDir, CacheDir }) Dir(d);
            bytesDone = 0;

            // 1. версия
            Progress(1, "Получаю данные версии " + MC + "...");
            string verPath = Path.Combine(VersionsDir, MC, MC + ".json");
            object ver;
            try
            {
                var man = await GetJson("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", Path.Combine(CacheDir, "version_manifest.json"), ct);
                var entry = man.Arr("versions").First(v => v.Str("id") == MC);
                ver = await GetJson(entry.Str("url"), verPath, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                if (!File.Exists(verPath)) throw;
                ver = Json.Parse(File.ReadAllText(verPath));
            }

            // 2. Java
            string component = ver.Obj("javaVersion").Str("component") ?? "java-runtime-delta";
            string javaRoot = Path.Combine(RuntimeDir, component);
            string marker = Path.Combine(javaRoot, ".installed");
            if (!File.Exists(marker) || ForceJavaCheck)
            {
                Progress(3, "Скачиваю Java " + ver.Obj("javaVersion").Long("majorVersion") + "...");
                var all = await GetJson("https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json", null, ct);
                var rt = all.Obj("windows-x64").Arr(component)[0];
                var rtMan = await GetJson(rt.Obj("manifest").Str("url"), null, ct);
                var items = new List<Item>();
                foreach (var kv in rtMan.Obj("files"))
                {
                    string dst = Local(javaRoot, kv.Key);
                    string type = kv.Value.Str("type");
                    if (type == "directory") { Dir(dst); continue; }
                    if (type != "file") continue;
                    var raw = kv.Value.Obj("downloads").Obj("raw");
                    items.Add(new Item { Url = raw.Str("url"), Path = dst, Size = raw.Long("size") });
                }
                await Download(items, "Java", 3, 15, ct);
                File.WriteAllText(marker, rt.Obj("version").Str("name") ?? "ok");
            }
            Progress(15, "Java готова");

            // 3. клиент
            var client = ver.Obj("downloads").Obj("client");
            string clientJar = Path.Combine(VersionsDir, MC, MC + ".jar");
            await Download(new List<Item> { new Item { Url = client.Str("url"), Path = clientJar, Size = client.Long("size") } }, "Minecraft " + MC, 15, 20, ct);

            // 4. библиотеки
            var libs = new Dictionary<string, string>();
            var order = new List<string>();
            var libItems = new List<Item>();
            var nativeJars = new List<string>();
            foreach (var l in ver.Arr("libraries"))
            {
                if (!Rules(l)) continue;
                string name = l.Str("name");
                if (name.EndsWith(":natives-windows-arm64") || name.EndsWith(":natives-windows-x86")) continue;
                var a = l.Obj("downloads").Obj("artifact");
                if (a.Count == 0) continue;
                string path = Local(LibDir, a.Str("path"));
                libItems.Add(new Item { Url = a.Str("url"), Path = path, Size = a.Long("size") });
                string key = LibKey(name);
                if (!libs.ContainsKey(key)) order.Add(key);
                libs[key] = path;
                if (name.EndsWith(":natives-windows")) nativeJars.Add(path);
            }

            Progress(20, "Получаю Fabric...");
            var loaders = await GetJson("https://meta.fabricmc.net/v2/versions/loader/" + MC, Path.Combine(CacheDir, "fabric-loaders.json"), ct) as List<object>;
            var stable = loaders.FirstOrDefault(x => x.Obj("loader").Get("stable") is bool && (bool)x.Obj("loader").Get("stable")) ?? loaders[0];
            string loader = stable.Obj("loader").Str("version");
            var fabric = await GetJson("https://meta.fabricmc.net/v2/versions/loader/" + MC + "/" + loader + "/profile/json", Path.Combine(CacheDir, "fabric-" + loader + ".json"), ct);
            foreach (var l in fabric.Arr("libraries"))
            {
                string name = l.Str("name");
                string rel = MavenPath(name);
                string baseUrl = l.Str("url"); if (!baseUrl.EndsWith("/")) baseUrl += "/";
                string path = Local(LibDir, rel);
                libItems.Add(new Item { Url = baseUrl + rel, Path = path, Size = l.Long("size") });
                string key = LibKey(name);
                if (!libs.ContainsKey(key)) order.Add(key);
                libs[key] = path;
            }
            await Download(libItems, "Библиотеки", 20, 35, ct);

            Progress(35, "Распаковываю natives...");
            foreach (var nj in nativeJars)
            {
                using (var z = ZipFile.OpenRead(nj))
                    foreach (var e in z.Entries)
                    {
                        if (e.FullName.StartsWith("META-INF")) continue;
                        string n = e.Name.ToLowerInvariant();
                        if (!(n.EndsWith(".dll") || n.EndsWith(".so") || n.EndsWith(".dylib"))) continue;
                        string dst = Path.Combine(NativesDir, e.Name);
                        if (!File.Exists(dst) || new FileInfo(dst).Length != e.Length) e.ExtractToFile(dst, true);
                    }
            }

            // 5. ресурсы
            Progress(36, "Получаю список ресурсов...");
            var ai = ver.Obj("assetIndex");
            var idx = await GetJson(ai.Str("url"), Path.Combine(AssetsDir, "indexes", ai.Str("id") + ".json"), ct);
            var assets = new List<Item>();
            foreach (var kv in idx.Obj("objects"))
            {
                string h = kv.Value.Str("hash"), pre = h.Substring(0, 2);
                assets.Add(new Item { Url = "https://resources.download.minecraft.net/" + pre + "/" + h, Path = Path.Combine(AssetsDir, "objects", pre, h), Size = kv.Value.Long("size") });
            }
            int oldPar = Parallel; Parallel = Math.Max(Parallel, 24);
            await Download(assets, "Ресурсы", 36, 92, ct);
            Parallel = oldPar;

            // 6. моды
            Progress(92, "Ставлю моды...");
            await InstallMods(ct);

            string opt = Path.Combine(GameDir, "options.txt");
            if (!File.Exists(opt) && RussianLang) File.WriteAllText(opt, "lang:ru_ru\n");

            Progress(100, "Готово!");
            Log("Установка завершена: Minecraft " + MC + ", Fabric " + loader + ", скачано " + (BytesDownloaded / 1048576) + " МБ");

            var gi = new GameInfo
            {
                Java = JavaExe(component, true),
                MainClass = fabric.Str("mainClass"),
                VersionId = fabric.Str("id"),
                AssetIndex = ai.Str("id"),
                LoaderVersion = loader
            };
            foreach (var k in order) gi.Classpath.Add(libs[k]);
            gi.Classpath.Add(clientJar);
            foreach (var a in fabric.Obj("arguments").Arr("jvm")) gi.FabricJvm.Add(Convert.ToString(a));
            var missing = gi.Classpath.Where(p => !File.Exists(p)).ToList();
            if (missing.Count > 0) throw new Exception("Не хватает файла: " + missing[0]);
            if (Path.DirectorySeparatorChar == '\\' && !File.Exists(gi.Java)) throw new Exception("Не найдена Java: " + gi.Java);
            return gi;
        }

        async Task InstallMods(CancellationToken ct)
        {
            Dir(ModsDir);
            bool haveApi = Directory.GetFiles(ModsDir, "fabric-api-*.jar").Length > 0;
            if (!haveApi)
            {
                string url = FabricApiFallbackUrl, name = FabricApiFallbackName;
                try
                {
                    string q = Uri.EscapeDataString("[\"" + MC + "\"]"), ql = Uri.EscapeDataString("[\"fabric\"]");
                    var vers = await GetJson("https://api.modrinth.com/v2/project/fabric-api/version?game_versions=" + q + "&loaders=" + ql, null, ct) as List<object>;
                    if (vers != null && vers.Count > 0)
                    {
                        var f = vers[0].Arr("files").FirstOrDefault(x => x.Get("primary") is bool && (bool)x.Get("primary")) ?? vers[0].Arr("files")[0];
                        url = f.Str("url"); name = f.Str("filename");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { Log("Modrinth недоступен — беру запасную ссылку Fabric API"); }
                await Download(new List<Item> { new Item { Url = url, Path = Path.Combine(ModsDir, name) } }, "Fabric API", 92, 97, ct);
            }

            // моды оптимизации (Sodium, Lithium и др.) — только Modrinth, с проверкой SHA-512
            if (OptimizationMods)
            {
                var om = new OptMods(ModsDir, CacheDir) { Log = Log, Progress = Progress };
                if (om.CheckDue()) { Log("Проверяю моды оптимизации на Modrinth..."); await om.Run(MC, 97, 99, ct); }
            }

            // MrBean Visuals
            foreach (var f in Directory.GetFiles(ModsDir, "MrBeanVisuals*"))
            {
                if (Path.GetFileName(f) != BundledModName && Path.GetFileName(f) != BundledModName + ".disabled") File.Delete(f); // старые версии
            }
            string on = Path.Combine(ModsDir, BundledModName), off = on + ".disabled";
            if (BundledModStream != null)
            {
                using (var s = BundledModStream())
                {
                    if (s != null)
                    {
                        string target = WithVisuals ? on : off;
                        if (!File.Exists(target) || new FileInfo(target).Length != s.Length)
                            using (var d = File.Create(target)) s.CopyTo(d);
                    }
                }
            }
            if (WithVisuals && File.Exists(off)) { if (File.Exists(on)) File.Delete(off); else File.Move(off, on); }
            if (!WithVisuals && File.Exists(on)) { if (File.Exists(off)) File.Delete(off); File.Move(on, off); }
        }
    }
}
