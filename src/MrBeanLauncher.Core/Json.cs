using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MrBeanLauncher.Core;

public static class Json
{
	public static object Parse(string s)
	{
		int i = 0;
		return Value(s, ref i);
	}

	private static void Ws(string s, ref int i)
	{
		while (i < s.Length && char.IsWhiteSpace(s[i]))
		{
			i++;
		}
	}

	private static object Value(string s, ref int i)
	{
		Ws(s, ref i);
		switch (s[i])
		{
		case '{':
			return Obj(s, ref i);
		case '[':
			return Arr(s, ref i);
		case '"':
			return Str(s, ref i);
		default:
		{
			if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0)
			{
				i += 4;
				return true;
			}
			if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0)
			{
				i += 5;
				return false;
			}
			if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0)
			{
				i += 4;
				return null;
			}
			int num = i;
			while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
			{
				i++;
			}
			return double.Parse(s.Substring(num, i - num), CultureInfo.InvariantCulture);
		}
		}
	}

	private static Dictionary<string, object> Obj(string s, ref int i)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		i++;
		Ws(s, ref i);
		if (s[i] == '}')
		{
			i++;
			return dictionary;
		}
		while (true)
		{
			Ws(s, ref i);
			string key = Str(s, ref i);
			Ws(s, ref i);
			i++;
			dictionary[key] = Value(s, ref i);
			Ws(s, ref i);
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		i++;
		return dictionary;
	}

	private static List<object> Arr(string s, ref int i)
	{
		List<object> list = new List<object>();
		i++;
		Ws(s, ref i);
		if (s[i] == ']')
		{
			i++;
			return list;
		}
		while (true)
		{
			list.Add(Value(s, ref i));
			Ws(s, ref i);
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		i++;
		return list;
	}

	private static string Str(string s, ref int i)
	{
		StringBuilder stringBuilder = new StringBuilder();
		i++;
		while (true)
		{
			char c = s[i++];
			switch (c)
			{
			case '"':
				return stringBuilder.ToString();
			default:
				stringBuilder.Append(c);
				break;
			case '\\':
			{
				char c2 = s[i++];
				switch (c2)
				{
				case 'n':
					stringBuilder.Append('\n');
					break;
				case 't':
					stringBuilder.Append('\t');
					break;
				case 'r':
					stringBuilder.Append('\r');
					break;
				case 'b':
					stringBuilder.Append('\b');
					break;
				case 'f':
					stringBuilder.Append('\f');
					break;
				case 'u':
					stringBuilder.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
					i += 4;
					break;
				default:
					stringBuilder.Append(c2);
					break;
				}
				break;
			}
			}
		}
	}

	public static object Get(this object o, string key)
	{
		if (!(o is Dictionary<string, object> dictionary))
		{
			return null;
		}
		if (!dictionary.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public static string Str(this object o, string key)
	{
		object obj = o.Get(key);
		if (obj != null)
		{
			return Convert.ToString(obj, CultureInfo.InvariantCulture);
		}
		return null;
	}

	public static long Long(this object o, string key)
	{
		object obj = o.Get(key);
		if (!(obj is double))
		{
			return 0L;
		}
		return (long)(double)obj;
	}

	public static List<object> Arr(this object o, string key)
	{
		return (o.Get(key) as List<object>) ?? new List<object>();
	}

	public static Dictionary<string, object> Obj(this object o, string key)
	{
		return (o.Get(key) as Dictionary<string, object>) ?? new Dictionary<string, object>();
	}

	public static string Write(object o, int indent = 0)
	{
		StringBuilder stringBuilder = new StringBuilder();
		W(stringBuilder, o, indent, 0);
		return stringBuilder.ToString();
	}

	private static void W(StringBuilder sb, object o, int ind, int lvl)
	{
		string value = ((ind > 0) ? ("\n" + new string(' ', ind * (lvl + 1))) : "");
		string value2 = ((ind > 0) ? ("\n" + new string(' ', ind * lvl)) : "");
		if (o == null)
		{
			sb.Append("null");
		}
		else if (o is string)
		{
			Esc(sb, (string)o);
		}
		else if (o is bool)
		{
			sb.Append(((bool)o) ? "true" : "false");
		}
		else if (o is IDictionary<string, object>)
		{
			sb.Append('{');
			bool flag = true;
			foreach (KeyValuePair<string, object> item in (IDictionary<string, object>)o)
			{
				if (!flag)
				{
					sb.Append(',');
				}
				flag = false;
				sb.Append(value);
				Esc(sb, item.Key);
				sb.Append((ind > 0) ? ": " : ":");
				W(sb, item.Value, ind, lvl + 1);
			}
			if (!flag)
			{
				sb.Append(value2);
			}
			sb.Append('}');
		}
		else if (o is IEnumerable)
		{
			sb.Append('[');
			bool flag2 = true;
			foreach (object item2 in (IEnumerable)o)
			{
				if (!flag2)
				{
					sb.Append(',');
				}
				flag2 = false;
				sb.Append(value);
				W(sb, item2, ind, lvl + 1);
			}
			if (!flag2)
			{
				sb.Append(value2);
			}
			sb.Append(']');
		}
		else
		{
			sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture));
		}
	}

	private static void Esc(StringBuilder sb, string s)
	{
		sb.Append('"');
		foreach (char c in s)
		{
			if (c == '"')
			{
				sb.Append("\\\"");
			}
			else if (c == '\\')
			{
				sb.Append("\\\\");
			}
			else if (c == '\n')
			{
				sb.Append("\\n");
			}
			else if (c == '\r')
			{
				sb.Append("\\r");
			}
			else if (c == '\t')
			{
				sb.Append("\\t");
			}
			else if (c < ' ')
			{
				StringBuilder stringBuilder = sb.Append("\\u");
				int num = c;
				stringBuilder.Append(num.ToString("x4"));
			}
			else
			{
				sb.Append(c);
			}
		}
		sb.Append('"');
	}
}
