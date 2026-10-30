using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MrBeanLauncher.Core
{
    public class Account
    {
        public string Uuid, Name, SkinUrl;
        public string RefreshToken;     // в памяти — открытый, на диске — только зашифрованный
        public string McToken;
        public DateTime McExpiresUtc;
        public string Status = "";      // для интерфейса: «Активен», «Нужен вход» и т.п.
        public bool NeedsLogin;
    }

    /// <summary>
    /// Хранилище аккаунтов. Токены шифруются средствами Windows (DPAPI, только для текущего пользователя)
    /// и никогда не пишутся в открытом виде и в логи. Пароли не хранятся вообще — их лаунчер не получает.
    /// </summary>
    public class AccountStore
    {
        readonly string file;
        public readonly List<Account> Accounts = new List<Account>();
        public string Selected;       // uuid аккаунта Microsoft или null = офлайн
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MrBeanLauncher.v2.accounts");

        public AccountStore(string root) { file = Path.Combine(root, "accounts.json"); }

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
#if NETFRAMEWORK
            var enc = System.Security.Cryptography.ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return "dpapi:" + Convert.ToBase64String(enc);
#else
            return "test:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));   // только для тестов вне Windows
#endif
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            try
            {
#if NETFRAMEWORK
                if (!stored.StartsWith("dpapi:")) return null;
                var dec = System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring(6)), Entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
#else
                return stored.StartsWith("test:") ? Encoding.UTF8.GetString(Convert.FromBase64String(stored.Substring(5))) : null;
#endif
            }
            catch { return null; }   // другой пользователь Windows или повреждено — просим войти заново
        }

        public void Load()
        {
            Accounts.Clear();
            try
            {
                if (!File.Exists(file)) return;
                var j = Json.Parse(File.ReadAllText(file, Encoding.UTF8));
                Selected = j.Str("selected");
                foreach (var a in j.Arr("accounts"))
                {
                    var acc = new Account
                    {
                        Uuid = a.Str("uuid"), Name = a.Str("name"), SkinUrl = a.Str("skin"),
                        RefreshToken = Unprotect(a.Str("refresh")), McToken = Unprotect(a.Str("mc")),
                        McExpiresUtc = new DateTime(a.Long("expires"), DateTimeKind.Utc)
                    };
                    if (acc.RefreshToken == null) { acc.NeedsLogin = true; acc.Status = "Нужен вход"; }
                    if (!string.IsNullOrEmpty(acc.Uuid)) Accounts.Add(acc);
                }
                if (Selected != null && Accounts.All(a => a.Uuid != Selected)) Selected = null;
            }
            catch { /* повреждённый файл — начинаем с пустого списка */ }
        }

        public void Save()
        {
            try
            {
                var list = new List<object>();
                foreach (var a in Accounts)
                    list.Add(new Dictionary<string, object> {
                        {"uuid", a.Uuid}, {"name", a.Name}, {"skin", a.SkinUrl ?? ""},
                        {"refresh", Protect(a.RefreshToken)}, {"mc", Protect(a.McToken)}, {"expires", a.McExpiresUtc.Ticks}
                    });
                var root = new Dictionary<string, object> { {"version", 1}, {"selected", Selected}, {"accounts", list} };
                string tmp = file + ".tmp";
                File.WriteAllText(tmp, Json.Write(root, 2), new UTF8Encoding(false));
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            catch { }
        }

        public Account Current { get { return Accounts.FirstOrDefault(a => a.Uuid == Selected); } }

        public Account Upsert(MinecraftSession s)
        {
            var a = Accounts.FirstOrDefault(x => x.Uuid == s.Uuid);
            if (a == null) { a = new Account { Uuid = s.Uuid }; Accounts.Add(a); }
            a.Name = s.Name; a.SkinUrl = s.SkinUrl; a.McToken = s.AccessToken; a.McExpiresUtc = s.ExpiresUtc;
            if (!string.IsNullOrEmpty(s.RefreshToken)) a.RefreshToken = s.RefreshToken;
            a.NeedsLogin = false; a.Status = "Активен";
            Selected = a.Uuid;
            Save();
            return a;
        }

        public void Remove(string uuid)
        {
            Accounts.RemoveAll(a => a.Uuid == uuid);
            if (Selected == uuid) Selected = Accounts.FirstOrDefault()?.Uuid;
            Save();
        }
    }
}
