using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MrBeanLauncher.Core
{
    public class Settings
    {
        public string Nick = "MrBean";
        public int RamMb = 4096;
        public int Width = 1280, Height = 720;
        public bool Fullscreen = false;
        public bool HideOnLaunch = true;
        public bool ShowConsoleOnLaunch = false;
        public bool WithVisuals = true;       // false = чистый Fabric без MrBean Visuals
        public string JavaArgs = "";
        public int Parallel = 16;
        public string GcPreset = "G1 (рекомендуется)";
        public bool RussianLang = true;
        public const string BuiltInClientId = "ab5c7709-a699-4432-9ec5-13cbb5959d13";   // приложение MrBean Launcher в Microsoft Entra
        public string ClientId = BuiltInClientId;
        public bool OptMods = true;         // автоустановка модов оптимизации с Modrinth        // Client ID приложения Azure для входа Microsoft

        static string FilePath(string root) { return Path.Combine(root, "launcher.json"); }

        public static Settings Load(string root)
        {
            var s = new Settings();
            try
            {
                string f = FilePath(root);
                if (!File.Exists(f)) return s;
                var j = Json.Parse(File.ReadAllText(f, Encoding.UTF8));
                s.Nick = j.Str("nick") ?? s.Nick;
                if (j.Get("ramMb") != null) s.RamMb = (int)j.Long("ramMb");
                if (j.Get("width") != null) s.Width = (int)j.Long("width");
                if (j.Get("height") != null) s.Height = (int)j.Long("height");
                if (j.Get("fullscreen") is bool) s.Fullscreen = (bool)j.Get("fullscreen");
                if (j.Get("hideOnLaunch") is bool) s.HideOnLaunch = (bool)j.Get("hideOnLaunch");
                if (j.Get("showConsole") is bool) s.ShowConsoleOnLaunch = (bool)j.Get("showConsole");
                if (j.Get("withVisuals") is bool) s.WithVisuals = (bool)j.Get("withVisuals");
                if (j.Get("russian") is bool) s.RussianLang = (bool)j.Get("russian");
                s.JavaArgs = j.Str("javaArgs") ?? "";
                s.ClientId = j.Str("clientId");
                if (string.IsNullOrWhiteSpace(s.ClientId)) s.ClientId = BuiltInClientId;
                if (j.Get("optMods") is bool) s.OptMods = (bool)j.Get("optMods");
                s.GcPreset = j.Str("gc") ?? s.GcPreset;
                if (j.Get("parallel") != null) s.Parallel = Math.Max(2, Math.Min(64, (int)j.Long("parallel")));
            }
            catch { }
            return s;
        }

        public void Save(string root)
        {
            try
            {
                var d = new Dictionary<string, object>
                {
                    {"nick", Nick}, {"ramMb", RamMb}, {"width", Width}, {"height", Height},
                    {"fullscreen", Fullscreen}, {"hideOnLaunch", HideOnLaunch}, {"showConsole", ShowConsoleOnLaunch},
                    {"withVisuals", WithVisuals}, {"russian", RussianLang}, {"javaArgs", JavaArgs}, {"gc", GcPreset}, {"parallel", Parallel}, {"clientId", ClientId}, {"optMods", OptMods}
                };
                File.WriteAllText(FilePath(root), Json.Write(d, 2), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
