using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrBeanLauncher.Core
{
    /// <summary>
    /// Автоустановка модов оптимизации (Sodium, Lithium и др.).
    /// Только с официального API Modrinth, только версии под 1.21.11 + Fabric, проверка SHA-512,
    /// без дублей (уже стоящие моды узнаём по хэшу), обновляем только то, что ставили сами.
    /// </summary>
    public class OptMods
    {
        public static readonly string[] Slugs = {
            "sodium", "lithium", "ferrite-core", "immediatelyfast", "entityculling",
            "moreculling", "modernfix", "dynamic-fps", "sodium-extra", "reeses-sodium-options"
        };
        const string Api = "https://api.modrinth.com/v2";
        const string FabricApiId = "P7dR8mSH";
        static readonly HashSet<string> SodiumFamily = new HashSet<string> { "sodium", "sodium-extra", "reeses-sodium-options", "immediatelyfast" };

        readonly HttpClient http;
        readonly string modsDir, manifestFile;
        public Action<string> Log = delegate { };
        public Action<double, string> Progress = delegate { };

        public OptMods(string modsDir, string cacheDir)
        {
            this.modsDir = modsDir;
            manifestFile = Path.Combine(cacheDir, "optmods.json");
            http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MrBeanLauncher/2.0 (MrBean Visuals)");
        }

        static string Hex(byte[] b) { var sb = new StringBuilder(b.Length * 2); foreach (var x in b) sb.Append(x.ToString("x2")); return sb.ToString(); }
        static string Sha1(string f) { using (var h = SHA1.Create()) using (var s = File.OpenRead(f)) return Hex(h.ComputeHash(s)); }
        static string Sha512(string f) { using (var h = SHA512.Create()) using (var s = File.OpenRead(f)) return Hex(h.ComputeHash(s)); }

        async Task<object> Get(string url, CancellationToken ct)
        {
            var r = await http.GetAsync(url, ct).ConfigureAwait(false);
            if ((int)r.StatusCode == 404) return null;
            r.EnsureSuccessStatusCode();
            return Json.Parse(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        Dictionary<string, string> LoadManifest()
        {
            var d = new Dictionary<string, string>();
            try { if (File.Exists(manifestFile)) foreach (var kv in Json.Parse(File.ReadAllText(manifestFile)).Obj("installed")) d[kv.Key] = Convert.ToString(kv.Value); } catch { }
            return d;
        }

        void SaveManifest(Dictionary<string, string> m, DateTime checkedUtc)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(manifestFile));
                var inst = new Dictionary<string, object>(); foreach (var kv in m) inst[kv.Key] = kv.Value;
                File.WriteAllText(manifestFile, Json.Write(new Dictionary<string, object> { {"checked", checkedUtc.Ticks}, {"installed", inst} }, 2));
            }
            catch { }
        }

        /// <summary>Проверяем Modrinth не чаще раза в 12 часов, если все моды на месте.</summary>
        public bool CheckDue()
        {
            try
            {
                if (!File.Exists(manifestFile)) return true;
                var j = Json.Parse(File.ReadAllText(manifestFile));
                var m = LoadManifest();
                if (m.Values.Any(f => !File.Exists(Path.Combine(modsDir, f)))) return true;
                return DateTime.UtcNow - new DateTime(j.Long("checked"), DateTimeKind.Utc) > TimeSpan.FromHours(12);
            }
            catch { return true; }
        }

        public async Task Run(string mc, double from, double to, CancellationToken ct)
        {
            Directory.CreateDirectory(modsDir);
            var manifest = LoadManifest();
            // какие проекты уже стоят (по SHA-1 через Modrinth) — чтобы не ставить дубли
            var jars = Directory.GetFiles(modsDir, "*.jar");
            bool optifine = jars.Any(f => Path.GetFileName(f).ToLowerInvariant().Contains("optifine") || Path.GetFileName(f).ToLowerInvariant().Contains("optifabric"));
            var present = new Dictionary<string, string>();  // project_id -> имя файла
            try
            {
                var byHash = jars.ToDictionary(f => Sha1(f), f => Path.GetFileName(f));
                var req = new StringContent(Json.Write(new Dictionary<string, object> { {"hashes", byHash.Keys.Cast<object>().ToList()}, {"algorithm", "sha1"} }), Encoding.UTF8, "application/json");
                var r = await http.PostAsync(Api + "/version_files", req, ct).ConfigureAwait(false);
                if (r.IsSuccessStatusCode)
                {
                    var j = Json.Parse(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
                    if (j is Dictionary<string, object> map)
                        foreach (var kv in map) present[kv.Value.Str("project_id")] = byHash[kv.Key];
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { Log("Моды оптимизации: Modrinth недоступен (" + e.Message + ") — пропускаю"); return; }

            string gv = Uri.EscapeDataString("[\"" + mc + "\"]"), lv = Uri.EscapeDataString("[\"fabric\"]");
            var queue = new Queue<string>(Slugs);
            var seen = new HashSet<string>();
            int total = Slugs.Length, n = 0, added = 0, updated = 0;
            while (queue.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                string key = queue.Dequeue();
                if (!seen.Add(key)) continue;
                string label = Array.IndexOf(Slugs, key) >= 0 ? key : "зависимость";
                Progress(from + (to - from) * Math.Min(1, (double)n++ / total), "Моды оптимизации: " + label);
                if (optifine && SodiumFamily.Contains(key)) { Log("  " + label + ": пропущен — стоит OptiFine/OptiFabric (несовместимо)"); continue; }
                try
                {
                    var vers = await Get(Api + "/project/" + key + "/version?game_versions=" + gv + "&loaders=" + lv, ct).ConfigureAwait(false) as List<object>;
                    if (vers == null || vers.Count == 0) { Log("  " + label + ": нет версии для " + mc + " — пропускаю"); continue; }
                    var v = vers.FirstOrDefault(x => x.Str("version_type") == "release") ?? vers[0];
                    string pid = v.Str("project_id");
                    seen.Add(pid);
                    // несовместимые с уже стоящими
                    if (v.Arr("dependencies").Any(d => d.Str("dependency_type") == "incompatible" && d.Str("project_id") != null && present.ContainsKey(d.Str("project_id"))))
                    { Log("  " + label + ": несовместим с уже установленным модом — пропускаю"); continue; }
                    var file = v.Arr("files").FirstOrDefault(f => f.Get("primary") is bool && (bool)f.Get("primary")) ?? v.Arr("files").FirstOrDefault();
                    if (file == null) continue;
                    string fname = file.Str("filename"), sha512 = file.Obj("hashes").Str("sha512");
                    string url = file.Str("url");
                    if (url == null || !url.StartsWith("https://cdn.modrinth.com/")) { Log("  " + label + ": ссылка не с cdn.modrinth.com — пропускаю"); continue; }

                    string have;
                    if (present.TryGetValue(pid, out have))
                    {
                        string ours; manifest.TryGetValue(pid, out ours);
                        if (have == fname || ours != have) { QueueDeps(v, present, queue); continue; }   // актуален, либо поставлен вами вручную — не трогаем
                    }
                    string target = Path.Combine(modsDir, fname), tmp = target + ".part";
                    using (var r = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                    {
                        r.EnsureSuccessStatusCode();
                        using (var s = await r.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var d = File.Create(tmp)) await s.CopyToAsync(d, 81920, ct).ConfigureAwait(false);
                    }
                    if (sha512 == null || !string.Equals(Sha512(tmp), sha512, StringComparison.OrdinalIgnoreCase))
                    { File.Delete(tmp); Log("  " + label + ": файл не прошёл проверку SHA-512 — удалён"); continue; }
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(tmp, target);
                    if (have != null && have != fname) { try { File.Delete(Path.Combine(modsDir, have)); } catch { } updated++; Log("  " + label + ": обновлён → " + fname); }
                    else { added++; Log("  " + label + ": установлен " + fname); }
                    present[pid] = fname; manifest[pid] = fname;
                    QueueDeps(v, present, queue);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { Log("  " + label + ": ошибка — " + e.Message); }
            }
            SaveManifest(manifest, DateTime.UtcNow);
            Log("Моды оптимизации: новых " + added + ", обновлено " + updated + ", всего на месте " + manifest.Count(kv => File.Exists(Path.Combine(modsDir, kv.Value))));
            Progress(to, "Моды оптимизации — готово");
        }

        static void QueueDeps(object v, Dictionary<string, string> present, Queue<string> q)
        {
            foreach (var d in v.Arr("dependencies"))
            {
                string pid = d.Str("project_id");
                if (d.Str("dependency_type") == "required" && pid != null && pid != FabricApiId && !present.ContainsKey(pid)) q.Enqueue(pid);
            }
        }

        /// <summary>Выключить: убираем только то, что ставил лаунчер.</summary>
        public int RemoveOurs()
        {
            var m = LoadManifest(); int c = 0;
            foreach (var f in m.Values) { var p = Path.Combine(modsDir, f); if (File.Exists(p)) { File.Delete(p); c++; } }
            SaveManifest(new Dictionary<string, string>(), DateTime.MinValue);
            return c;
        }
    }
}
