using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MrBeanLauncher.Core;

/// <summary>
/// Разбор ответов Microsoft OAuth: превращает код AADSTS в понятную инструкцию на русском.
/// </summary>
public class OAuthError
{
	public string Short;

	public string Title;

	public string Detail;

	public List<string> Fix = new List<string>();

	public string LinkText;

	public string LinkUrl;

	public bool NeedsRelogin;

	/// <summary>Локальный приём ответа Microsoft не сработал — есть смысл перейти на вход по коду.</summary>
	public bool SuggestCode;
}

public static class OAuthErrors
{
	public const string AzurePortal = "https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade";

	public const string MojangReview = "https://aka.ms/mce-reviewappid";

	private static readonly Regex CodeRx = new Regex("AADSTS(\\d+)", RegexOptions.Compiled);

	public static string Code(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		Match m = CodeRx.Match(text);
		return m.Success ? m.Groups[1].Value : "";
	}

	private static OAuthError Make(string shortLine, string title, string detail, string code = "",
		string[] fix = null, string linkText = null, string linkUrl = null, bool needsRelogin = false)
	{
		OAuthError oAuthError = new OAuthError
		{
			Short = shortLine,
			Title = title,
			Detail = string.IsNullOrEmpty(code) ? detail : (detail + "\n\nКод ошибки: AADSTS" + code),
			LinkText = linkText,
			LinkUrl = linkUrl,
			NeedsRelogin = needsRelogin
		};
		if (fix != null)
		{
			oAuthError.Fix.AddRange(fix);
		}
		return oAuthError;
	}

	private static string[] Fix_(params string[] lines)
	{
		return lines;
	}

	/// <summary>
	/// error — машинный код OAuth (invalid_client и т.п.), description — текст Microsoft с AADSTSxxxxx.
	/// </summary>
	public static OAuthError Describe(string error, string description, int httpStatus)
	{
		string aad = Code(description);
		if (string.IsNullOrWhiteSpace(error))
		{
			error = "";
		}
		switch (aad)
		{
		case "50020":
			return Make("Аккаунт не подходит приложению (AADSTS50020)", "Эта учётная запись не найдена в каталоге приложения",
				"Вы вошли аккаунтом, которого нет в том каталоге Microsoft, где зарегистрирован Client ID. Обычно это значит, что приложение в Azure создано для рабочих или учебных аккаунтов, а для Minecraft нужен личный аккаунт Microsoft (Outlook, Hotmail, Live).",
				aad,
				Fix_("Откройте portal.azure.com → «Регистрация приложений» → выберите своё приложение → «Обзор».",
					"Пункт «Поддерживаемые типы учётных записей» поставьте на «Только личные учётные записи Microsoft».",
					"Если пункт не меняется — откройте «Манифест» и поставьте \"signInAudience\": \"PersonalMicrosoftAccount\", сохраните.",
					"Зайдите в лаунчер тем аккаунтом, на который куплен Minecraft (личный Outlook/Hotmail/Live), а не рабочим или учебным.",
					"После смены типа подождите пару минут и попробуйте вход заново."),
				"Открыть регистрацию приложений в Azure", AzurePortal, needsRelogin: true);
		case "50011":
		case "50013":
			return Make("Лаунчер не принял ответ Microsoft (AADSTS" + aad + ")", "Не совпадает адрес перенаправления",
				"Приложение в Azure не знает, что ему можно возвращать код входа на локальный адрес лаунчера.",
				aad,
				Fix_("Azure → «Регистрация приложений» → ваше приложение → «Проверка подлинности».",
					"«Добавить перенаправление» → платформа «Мобильные и настольные приложения».",
					"В адресе перенаправления укажите http://localhost и сохраните."),
				"Открыть «Проверка подлинности» в Azure", AzurePortal);
		case "700016":
			return Make("Приложение не найдено (AADSTS700016)", "Client ID не найден в Microsoft",
				"Такого идентификатора приложения нет в каталоге. Возможно, в лаунчере опечатка в Client ID или приложение в Azure удалили.",
				aad,
				Fix_("Скопируйте «Идентификатор приложения (клиента)» заново: Azure → «Регистрация приложений» → ваше приложение.",
					"Вставьте его в лаунчер целиком, без пробелов (формат 00000000-0000-0000-0000-000000000000)."),
				"Открыть список приложений в Azure", AzurePortal);
		case "7000215":
			return Make("Приложение просит секрет (AADSTS7000215)", "Секрет клиента не нужен",
				"Для входа в Minecraft используется общедоступный клиент — секретный ключ (client secret) создавать и вводить не нужно.",
				aad,
				Fix_("Azure → «Сертификаты и секреты» — удалите созданный секрет, если добавляли.",
					"Azure → «Проверка подлинности» → «Разрешить потоки общедоступных клиентов» = Да."),
				"Открыть приложение в Azure", AzurePortal);
		case "90077":
		case "90002":
			return Make("Каталог недоступен (AADSTS" + aad + ")", "Microsoft не видит каталог приложения",
				"Приложение зарегистрировано в каталоге, который недоступен для личных аккаунтов.",
				aad,
				Fix_("Создайте новое приложение: Azure → «Регистрация приложений» → «Новая регистрация».",
					"В поле «Поддерживаемые типы учётных записей» выберите «Только личные учётные записи Microsoft».",
					"Скопируйте новый Client ID в лаунчер."),
				"Создать приложение в Azure", AzurePortal);
		case "65001":
			return Make("Microsoft просит подтверждение (AADSTS65001)", "Нужно разрешить доступ в браузере",
				"На странице входа Microsoft ждёт, что вы подтвердите доступ приложения к Xbox Live.",
				aad, Fix_("Повторите вход и на странице Microsoft нажмите «Принять»."));
		}
		switch (error)
		{
		case "access_denied":
		case "login_required":
			return Make("Вход отменён", "Вы закрыли страницу входа",
				"Страница Microsoft была закрыта или вход отменён. Нажмите «Войти через Microsoft» ещё раз.");
		case "authorization_declined":
			return Make("Вы отклонили запрос", "Доступ не разрешён",
				"На странице Microsoft нажали «Отклонить». Повторите вход и нажмите «Принять».");
		case "expired_token":
		case "code_expired":
			return Make("Код входа устарел", "Слишком долго ждали",
				"Код живёт 15 минут. Нажмите «Войти через Microsoft» ещё раз — теперь доведите до конца.");
		case "bad_verification_code":
			return Make("Неверный код", "Код введён с ошибкой",
				"Проверьте код на странице Microsoft: его нужно вставить целиком, без лишних символов.");
		case "interaction_required":
			return Make("Нужен повторный вход", "Microsoft просит войти заново",
				"Сессия в браузере прервалась. Повторите вход.");
		case "invalid_grant":
			return Make("Сессия Microsoft истекла", "Нужно войти заново",
				"Токен обновления больше не действителен (прошло много времени или пароль сменили).",
				"", null, null, null, needsRelogin: true);
		case "unauthorized_client":
			return Make("Приложению запрещён такой вход", "Не включены потоки общедоступных клиентов",
				"Лаунчер — общедоступное приложение (без секрета). В Azure у него должен быть разрешён соответствующий поток.",
				"",
				Fix_("Azure → «Регистрация приложений» → ваше приложение → «Проверка подлинности».",
					"Внизу, «Дополнительные параметры» → «Разрешить потоки общедоступных клиентов» = Да → «Сохранить».",
					"Подождите пару минут и повторите вход."),
				"Открыть приложение в Azure", AzurePortal);
		case "invalid_client":
			return Make("Microsoft не принимает Client ID", "Client ID не подходит",
				"Идентификатор приложения указан неверно или приложение удалено/переименовано в Azure.",
				"", null, "Открыть список приложений в Azure", AzurePortal);
		case "unsupported_grant_type":
			return Make("Microsoft не поддерживает этот способ", "Сбой служебного запроса",
				"Такое бывает при временных проблемах на стороне Microsoft — попробуйте через минуту.");
		case "temporarily_unavailable":
			return Make("Microsoft временно недоступен", "Сервер Microsoft не отвечает",
				"Подождите пару минут и повторите вход.");
		}
		if (httpStatus == 429)
		{
			return Make("Слишком много попыток", "Microsoft ограничил частоту запросов",
				"Подождите 1–2 минуты и попробуйте снова.");
		}
		string tail = string.IsNullOrWhiteSpace(description) ? "" : ("\n\nMicrosoft сообщает: " + Trim(description, 400));
		return Make("Microsoft отклонил вход" + (string.IsNullOrEmpty(error) ? "" : " (" + error + ")"), "Не удалось войти в Microsoft",
			"Сервер Microsoft ответил ошибкой" + (string.IsNullOrEmpty(error) ? "" : " «" + error + "»") + "." + tail);
	}

	public static OAuthError NoLicense()
	{
		return Make("На аккаунте нет лицензии Minecraft: Java Edition", "Minecraft Java Edition не найден на этом аккаунте",
			"Microsoft-аккаунт подошёл, но купленного Minecraft: Java Edition на нём нет. Проверьте, что вход делаете тем аккаунтом, на который оформлена игра.",
			"",
			Fix_("Откройте minecraft.net и войдите тем же аккаунтом — там должен быть активен Java Edition.",
				"Если игра куплена на другой аккаунт — выйдите и войдите в лаунчере правильным аккаунтом.",
				"На детском или Xbox-аккаунте может потребоваться подтверждение из семьи Microsoft."),
			"Открыть minecraft.net", "https://www.minecraft.net/");
	}

	private static string Trim(string s, int max)
	{
		s = s.Replace("\r", " ").Replace("\n", " ").Trim();
		return s.Length <= max ? s : s.Substring(0, max) + "...";
	}
}
