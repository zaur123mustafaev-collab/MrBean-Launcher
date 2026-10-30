using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MrBeanLauncher.Core;

public class Settings
{
	public string Nick = "MrBean";

	public int RamMb = 4096;

	public int Width = 1280;

	public int Height = 720;

	public bool Fullscreen;

	public bool HideOnLaunch = true;

	public bool ShowConsoleOnLaunch;

	public bool WithVisuals = true;

	public string JavaArgs = "";

	public int Parallel = 16;

	public string GcPreset = "G1 (рекомендуется)";

	public bool RussianLang = true;

	public string ClientId = "";

	public bool OptMods = true;

	private static string FilePath(string root)
	{
		return Path.Combine(root, "launcher.json");
	}

	public static Settings Load(string root)
	{
		Settings settings = new Settings();
		try
		{
			string path = FilePath(root);
			if (!File.Exists(path))
			{
				return settings;
			}
			object o = Json.Parse(File.ReadAllText(path, Encoding.UTF8));
			settings.Nick = o.Str("nick") ?? settings.Nick;
			if (o.Get("ramMb") != null)
			{
				settings.RamMb = (int)o.Long("ramMb");
			}
			if (o.Get("width") != null)
			{
				settings.Width = (int)o.Long("width");
			}
			if (o.Get("height") != null)
			{
				settings.Height = (int)o.Long("height");
			}
			if (o.Get("fullscreen") is bool)
			{
				settings.Fullscreen = (bool)o.Get("fullscreen");
			}
			if (o.Get("hideOnLaunch") is bool)
			{
				settings.HideOnLaunch = (bool)o.Get("hideOnLaunch");
			}
			if (o.Get("showConsole") is bool)
			{
				settings.ShowConsoleOnLaunch = (bool)o.Get("showConsole");
			}
			if (o.Get("withVisuals") is bool)
			{
				settings.WithVisuals = (bool)o.Get("withVisuals");
			}
			if (o.Get("russian") is bool)
			{
				settings.RussianLang = (bool)o.Get("russian");
			}
			settings.JavaArgs = o.Str("javaArgs") ?? "";
			settings.ClientId = o.Str("clientId") ?? "";
			if (o.Get("optMods") is bool)
			{
				settings.OptMods = (bool)o.Get("optMods");
			}
			settings.GcPreset = o.Str("gc") ?? settings.GcPreset;
			if (o.Get("parallel") != null)
			{
				settings.Parallel = Math.Max(2, Math.Min(64, (int)o.Long("parallel")));
			}
		}
		catch
		{
		}
		return settings;
	}

	public void Save(string root)
	{
		try
		{
			Dictionary<string, object> o = new Dictionary<string, object>
			{
				{ "nick", Nick },
				{ "ramMb", RamMb },
				{ "width", Width },
				{ "height", Height },
				{ "fullscreen", Fullscreen },
				{ "hideOnLaunch", HideOnLaunch },
				{ "showConsole", ShowConsoleOnLaunch },
				{ "withVisuals", WithVisuals },
				{ "russian", RussianLang },
				{ "javaArgs", JavaArgs },
				{ "gc", GcPreset },
				{ "parallel", Parallel },
				{ "clientId", ClientId },
				{ "optMods", OptMods }
			};
			File.WriteAllText(FilePath(root), Json.Write(o, 2), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		catch
		{
		}
	}
}
