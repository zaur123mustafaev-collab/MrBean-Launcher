using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MrBeanLauncher.Core
{
    public static class GameLauncher
    {
        public static string OfflineUuid(string name)
        {
            using (var md5 = MD5.Create())
            {
                var b = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
                b[6] = (byte)((b[6] & 0x0f) | 0x30);
                b[8] = (byte)((b[8] & 0x3f) | 0x80);
                return string.Concat(b.Select(x => x.ToString("x2")));
            }
        }

        static IEnumerable<string> GcArgs(string preset)
        {
            if (preset != null && preset.StartsWith("ZGC")) return new[] { "-XX:+UseZGC", "-XX:+ZGenerational" };
            if (preset != null && preset.StartsWith("Parallel")) return new[] { "-XX:+UseParallelGC" };
            return new[] { "-XX:+UseG1GC", "-XX:+UnlockExperimentalVMOptions", "-XX:G1NewSizePercent=20",
                           "-XX:G1ReservePercent=20", "-XX:MaxGCPauseMillis=50", "-XX:G1HeapRegionSize=32M" };
        }

        /// <summary>Данные входа для запуска. null = офлайн-ник из настроек.</summary>
        public class LaunchAccount { public string Name, Uuid, AccessToken, Xuid, ClientId; }

        public static List<string> BuildArgs(Installer inst, GameInfo gi, Settings s, LaunchAccount acc = null)
        {
            char sep = Path.DirectorySeparatorChar == '\\' ? ';' : ':';
            string nat = inst.NativesDir;
            var a = new List<string> { "-Xms1024M", "-Xmx" + s.RamMb + "M" };
            a.AddRange(GcArgs(s.GcPreset));
            a.AddRange(new[] {
                "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump",
                "-Djava.library.path=" + nat, "-Djna.tmpdir=" + nat,
                "-Dorg.lwjgl.system.SharedLibraryExtractPath=" + nat, "-Dio.netty.native.workdir=" + nat,
                "-Dminecraft.launcher.brand=MrBeanLauncher", "-Dminecraft.launcher.version=2.0", "-Dmrbean.launcher=2.0",
                "-Dfile.encoding=UTF-8", "-Dstdout.encoding=UTF-8", "-Dstderr.encoding=UTF-8"
            });
            if (!string.IsNullOrWhiteSpace(s.JavaArgs))
                a.AddRange(SplitArgs(s.JavaArgs));
            a.AddRange(gi.FabricJvm);
            a.Add("-cp"); a.Add(string.Join(sep.ToString(), gi.Classpath));
            a.Add(gi.MainClass);
            a.AddRange(new[] {
                "--username", acc != null ? acc.Name : s.Nick, "--version", gi.VersionId, "--gameDir", inst.GameDir,
                "--assetsDir", inst.AssetsDir, "--assetIndex", gi.AssetIndex,
                "--uuid", acc != null ? acc.Uuid : OfflineUuid(s.Nick),
                "--accessToken", acc != null ? acc.AccessToken : "0",
                "--clientId", acc != null && !string.IsNullOrEmpty(acc.ClientId) ? acc.ClientId : "0",
                "--xuid", acc != null && !string.IsNullOrEmpty(acc.Xuid) ? acc.Xuid : "0",
                "--userType", acc != null ? "msa" : "legacy", "--versionType", "MrBean Launcher",
                "--width", s.Width.ToString(), "--height", s.Height.ToString()
            });
            if (s.Fullscreen) a.Add("--fullscreen");
            return a;
        }

        public static IEnumerable<string> SplitArgs(string line)
        {
            var cur = new StringBuilder(); bool q = false;
            foreach (char c in line)
            {
                if (c == '"') { q = !q; continue; }
                if (char.IsWhiteSpace(c) && !q) { if (cur.Length > 0) { yield return cur.ToString(); cur.Clear(); } continue; }
                cur.Append(c);
            }
            if (cur.Length > 0) yield return cur.ToString();
        }

        public static string Quote(string a)
        {
            if (a.Length == 0) return "\"\"";
            if (a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int bs = 0;
            foreach (char c in a)
            {
                if (c == '\\') { bs++; continue; }
                if (c == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); bs = 0; continue; }
                sb.Append('\\', bs); bs = 0; sb.Append(c);
            }
            sb.Append('\\', bs * 2); sb.Append('"');
            return sb.ToString();
        }

        public static Process Start(Installer inst, GameInfo gi, Settings s, Action<string> output, LaunchAccount acc = null)
        {
            var args = BuildArgs(inst, gi, s, acc);
            var psi = new ProcessStartInfo
            {
                FileName = gi.Java,
                Arguments = string.Join(" ", args.Select(Quote)),
                WorkingDirectory = inst.GameDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            string secret = acc != null ? acc.AccessToken : null;
            Func<string, string> clean = l => secret != null && secret.Length > 8 ? l.Replace(secret, "<скрыто>") : l;
            p.OutputDataReceived += (o, e) => { if (e.Data != null) output(clean(e.Data)); };
            p.ErrorDataReceived += (o, e) => { if (e.Data != null) output(clean(e.Data)); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            return p;
        }
    }
}
