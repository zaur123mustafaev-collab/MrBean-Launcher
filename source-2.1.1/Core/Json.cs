using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MrBeanLauncher.Core
{
    /// <summary>Маленький JSON-парсер/писатель без внешних зависимостей.</summary>
    public static class Json
    {
        public static object Parse(string s) { int i = 0; var v = Value(s, ref i); return v; }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                string k = Str(s, ref i);
                Ws(s, ref i); i++; // :
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return d;
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return l;
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
        }

        // ---------- удобный доступ
        public static object Get(this object o, string key)
        {
            var d = o as Dictionary<string, object>;
            if (d == null) return null;
            object v; return d.TryGetValue(key, out v) ? v : null;
        }
        public static string Str(this object o, string key) { var v = o.Get(key); return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture); }
        public static long Long(this object o, string key) { var v = o.Get(key); return v is double ? (long)(double)v : 0; }
        public static List<object> Arr(this object o, string key) { return o.Get(key) as List<object> ?? new List<object>(); }
        public static Dictionary<string, object> Obj(this object o, string key) { return o.Get(key) as Dictionary<string, object> ?? new Dictionary<string, object>(); }

        // ---------- запись
        public static string Write(object o, int indent = 0)
        {
            var sb = new StringBuilder(); W(sb, o, indent, 0); return sb.ToString();
        }
        static void W(StringBuilder sb, object o, int ind, int lvl)
        {
            string nl = ind > 0 ? "\n" + new string(' ', ind * (lvl + 1)) : "";
            string nlEnd = ind > 0 ? "\n" + new string(' ', ind * lvl) : "";
            if (o == null) sb.Append("null");
            else if (o is string) Esc(sb, (string)o);
            else if (o is bool) sb.Append((bool)o ? "true" : "false");
            else if (o is IDictionary<string, object>)
            {
                sb.Append('{'); bool first = true;
                foreach (var kv in (IDictionary<string, object>)o)
                {
                    if (!first) sb.Append(','); first = false;
                    sb.Append(nl); Esc(sb, kv.Key); sb.Append(ind > 0 ? ": " : ":"); W(sb, kv.Value, ind, lvl + 1);
                }
                if (!first) sb.Append(nlEnd); sb.Append('}');
            }
            else if (o is System.Collections.IEnumerable)
            {
                sb.Append('['); bool first = true;
                foreach (var v in (System.Collections.IEnumerable)o)
                {
                    if (!first) sb.Append(','); first = false;
                    sb.Append(nl); W(sb, v, ind, lvl + 1);
                }
                if (!first) sb.Append(nlEnd); sb.Append(']');
            }
            else sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture));
        }
        static void Esc(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            sb.Append('"');
        }
    }
}
