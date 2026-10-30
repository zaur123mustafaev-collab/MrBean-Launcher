using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MrBeanLauncher.Core;

public static class GameLauncher
{
	public class LaunchAccount
	{
		public string Name;

		public string Uuid;

		public string AccessToken;

		public string Xuid;

		public string ClientId;
	}

	public static string OfflineUuid(string name)
	{
		using MD5 mD = MD5.Create();
		byte[] array = mD.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
		array[6] = (byte)((array[6] & 0xF) | 0x30);
		array[8] = (byte)((array[8] & 0x3F) | 0x80);
		return string.Concat(array.Select((byte x) => x.ToString("x2")));
	}

	private static IEnumerable<string> GcArgs(string preset)
	{
		if (preset != null && preset.StartsWith("ZGC"))
		{
			return new string[2] { "-XX:+UseZGC", "-XX:+ZGenerational" };
		}
		if (preset != null && preset.StartsWith("Parallel"))
		{
			return new string[1] { "-XX:+UseParallelGC" };
		}
		return new string[6] { "-XX:+UseG1GC", "-XX:+UnlockExperimentalVMOptions", "-XX:G1NewSizePercent=20", "-XX:G1ReservePercent=20", "-XX:MaxGCPauseMillis=50", "-XX:G1HeapRegionSize=32M" };
	}

	public static List<string> BuildArgs(Installer inst, GameInfo gi, Settings s, LaunchAccount acc = null)
	{
		char c = ((Path.DirectorySeparatorChar == '\\') ? ';' : ':');
		string nativesDir = inst.NativesDir;
		List<string> list = new List<string>
		{
			"-Xms1024M",
			"-Xmx" + s.RamMb + "M"
		};
		list.AddRange(GcArgs(s.GcPreset));
		list.AddRange(new string[11]
		{
			"-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump",
			"-Djava.library.path=" + nativesDir,
			"-Djna.tmpdir=" + nativesDir,
			"-Dorg.lwjgl.system.SharedLibraryExtractPath=" + nativesDir,
			"-Dio.netty.native.workdir=" + nativesDir,
			"-Dminecraft.launcher.brand=MrBeanLauncher",
			"-Dminecraft.launcher.version=2.0",
			"-Dmrbean.launcher=2.0",
			"-Dfile.encoding=UTF-8",
			"-Dstdout.encoding=UTF-8",
			"-Dstderr.encoding=UTF-8"
		});
		if (!string.IsNullOrWhiteSpace(s.JavaArgs))
		{
			list.AddRange(SplitArgs(s.JavaArgs));
		}
		list.AddRange(gi.FabricJvm);
		list.Add("-cp");
		list.Add(string.Join(c.ToString(), gi.Classpath));
		list.Add(gi.MainClass);
		list.AddRange(new string[26]
		{
			"--username",
			(acc != null) ? acc.Name : s.Nick,
			"--version",
			gi.VersionId,
			"--gameDir",
			inst.GameDir,
			"--assetsDir",
			inst.AssetsDir,
			"--assetIndex",
			gi.AssetIndex,
			"--uuid",
			(acc != null) ? acc.Uuid : OfflineUuid(s.Nick),
			"--accessToken",
			(acc != null) ? acc.AccessToken : "0",
			"--clientId",
			(acc != null && !string.IsNullOrEmpty(acc.ClientId)) ? acc.ClientId : "0",
			"--xuid",
			(acc != null && !string.IsNullOrEmpty(acc.Xuid)) ? acc.Xuid : "0",
			"--userType",
			(acc != null) ? "msa" : "legacy",
			"--versionType",
			"MrBean Launcher",
			"--width",
			s.Width.ToString(),
			"--height",
			s.Height.ToString()
		});
		if (s.Fullscreen)
		{
			list.Add("--fullscreen");
		}
		return list;
	}

	public static IEnumerable<string> SplitArgs(string line)
	{
		StringBuilder cur = new StringBuilder();
		bool q = false;
		foreach (char c in line)
		{
			if (c == '"')
			{
				q = !q;
			}
			else if (char.IsWhiteSpace(c) && !q)
			{
				if (cur.Length > 0)
				{
					yield return cur.ToString();
					cur.Clear();
				}
			}
			else
			{
				cur.Append(c);
			}
		}
		if (cur.Length > 0)
		{
			yield return cur.ToString();
		}
	}

	public static string Quote(string a)
	{
		if (a.Length == 0)
		{
			return "\"\"";
		}
		if (a.IndexOfAny(new char[3] { ' ', '\t', '"' }) < 0)
		{
			return a;
		}
		StringBuilder stringBuilder = new StringBuilder("\"");
		int num = 0;
		foreach (char c in a)
		{
			switch (c)
			{
			case '\\':
				num++;
				break;
			case '"':
				stringBuilder.Append('\\', num * 2 + 1);
				stringBuilder.Append('"');
				num = 0;
				break;
			default:
				stringBuilder.Append('\\', num);
				num = 0;
				stringBuilder.Append(c);
				break;
			}
		}
		stringBuilder.Append('\\', num * 2);
		stringBuilder.Append('"');
		return stringBuilder.ToString();
	}

	public static Process Start(Installer inst, GameInfo gi, Settings s, Action<string> output, LaunchAccount acc = null)
	{
		List<string> source = BuildArgs(inst, gi, s, acc);
		ProcessStartInfo startInfo = new ProcessStartInfo
		{
			FileName = gi.Java,
			Arguments = string.Join(" ", source.Select(Quote)),
			WorkingDirectory = inst.GameDir,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};
		Process process = new Process
		{
			StartInfo = startInfo,
			EnableRaisingEvents = true
		};
		string secret = acc?.AccessToken;
		Func<string, string> clean = (string l) => (secret == null || secret.Length <= 8) ? l : l.Replace(secret, "<скрыто>");
		process.OutputDataReceived += (object o, DataReceivedEventArgs e) =>
		{
			if (e.Data != null)
			{
				output(clean(e.Data));
			}
		};
		process.ErrorDataReceived += (object o, DataReceivedEventArgs e) =>
		{
			if (e.Data != null)
			{
				output(clean(e.Data));
			}
		};
		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		return process;
	}
}
