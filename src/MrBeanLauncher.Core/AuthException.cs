using System;

namespace MrBeanLauncher.Core;

public class AuthException : Exception
{
	public readonly bool NeedsRelogin;

	/// <summary>Подробное описание ошибки для красивого окна (может отсутствовать).</summary>
	public readonly OAuthError Info;

	public AuthException(string message, bool needsRelogin = false)
		: base(message)
	{
		NeedsRelogin = needsRelogin;
	}

	public AuthException(OAuthError info)
		: base(info.Short)
	{
		Info = info;
		NeedsRelogin = info.NeedsRelogin;
	}
}
