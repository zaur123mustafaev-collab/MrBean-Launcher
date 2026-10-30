using System;

namespace MrBeanLauncher.Core;

public class Account
{
	public string Uuid;

	public string Name;

	public string SkinUrl;

	public string RefreshToken;

	public string McToken;

	public DateTime McExpiresUtc;

	public string Status = "";

	public bool NeedsLogin;
}
