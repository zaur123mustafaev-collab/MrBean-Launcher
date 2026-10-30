using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MrBeanLauncher.Core;

public class AccountStore
{
	private readonly string file;

	public readonly List<Account> Accounts = new List<Account>();

	public string Selected;

	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MrBeanLauncher.v2.accounts");

	public Account Current => Accounts.FirstOrDefault((Account a) => a.Uuid == Selected);

	public AccountStore(string root)
	{
		file = Path.Combine(root, "accounts.json");
	}

	public static string Protect(string plain)
	{
		if (string.IsNullOrEmpty(plain))
		{
			return "";
		}
		byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
		return "dpapi:" + Convert.ToBase64String(inArray);
	}

	public static string Unprotect(string stored)
	{
		if (string.IsNullOrEmpty(stored))
		{
			return null;
		}
		try
		{
			if (!stored.StartsWith("dpapi:"))
			{
				return null;
			}
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring(6)), Entropy, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(bytes);
		}
		catch
		{
			return null;
		}
	}

	public void Load()
	{
		Accounts.Clear();
		try
		{
			if (!File.Exists(file))
			{
				return;
			}
			object o = Json.Parse(File.ReadAllText(file, Encoding.UTF8));
			Selected = o.Str("selected");
			foreach (object item in o.Arr("accounts"))
			{
				Account account = new Account
				{
					Uuid = item.Str("uuid"),
					Name = item.Str("name"),
					SkinUrl = item.Str("skin"),
					RefreshToken = Unprotect(item.Str("refresh")),
					McToken = Unprotect(item.Str("mc")),
					McExpiresUtc = new DateTime(item.Long("expires"), DateTimeKind.Utc)
				};
				if (account.RefreshToken == null)
				{
					account.NeedsLogin = true;
					account.Status = "Нужен вход";
				}
				if (!string.IsNullOrEmpty(account.Uuid))
				{
					Accounts.Add(account);
				}
			}
			if (Selected != null && Accounts.All((Account a) => a.Uuid != Selected))
			{
				Selected = null;
			}
		}
		catch
		{
		}
	}

	public void Save()
	{
		try
		{
			List<object> list = new List<object>();
			foreach (Account account in Accounts)
			{
				list.Add(new Dictionary<string, object>
				{
					{ "uuid", account.Uuid },
					{ "name", account.Name },
					{
						"skin",
						account.SkinUrl ?? ""
					},
					{
						"refresh",
						Protect(account.RefreshToken)
					},
					{
						"mc",
						Protect(account.McToken)
					},
					{
						"expires",
						account.McExpiresUtc.Ticks
					}
				});
			}
			Dictionary<string, object> o = new Dictionary<string, object>
			{
				{ "version", 1 },
				{ "selected", Selected },
				{ "accounts", list }
			};
			string text = file + ".tmp";
			File.WriteAllText(text, Json.Write(o, 2), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(file))
			{
				File.Delete(file);
			}
			File.Move(text, file);
		}
		catch
		{
		}
	}

	public Account Upsert(MinecraftSession s)
	{
		Account account = Accounts.FirstOrDefault((Account x) => x.Uuid == s.Uuid);
		if (account == null)
		{
			account = new Account
			{
				Uuid = s.Uuid
			};
			Accounts.Add(account);
		}
		account.Name = s.Name;
		account.SkinUrl = s.SkinUrl;
		account.McToken = s.AccessToken;
		account.McExpiresUtc = s.ExpiresUtc;
		if (!string.IsNullOrEmpty(s.RefreshToken))
		{
			account.RefreshToken = s.RefreshToken;
		}
		account.NeedsLogin = false;
		account.Status = "Активен";
		Selected = account.Uuid;
		Save();
		return account;
	}

	public void Remove(string uuid)
	{
		Accounts.RemoveAll((Account a) => a.Uuid == uuid);
		if (Selected == uuid)
		{
			Selected = Accounts.FirstOrDefault()?.Uuid;
		}
		Save();
	}
}
