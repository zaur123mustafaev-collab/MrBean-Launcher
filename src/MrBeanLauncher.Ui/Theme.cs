using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace MrBeanLauncher.Ui;

public static class Theme
{
	public static float Scale = 1f;

	public static readonly Color Bg = Color.FromArgb(12, 13, 18);

	public static readonly Color Side = Color.FromArgb(16, 17, 24);

	public static readonly Color Card = Color.FromArgb(22, 24, 33);

	public static readonly Color CardHover = Color.FromArgb(30, 33, 45);

	public static readonly Color Border = Color.FromArgb(40, 44, 58);

	public static readonly Color Fg = Color.FromArgb(240, 242, 248);

	public static readonly Color Muted = Color.FromArgb(138, 145, 165);

	public static readonly Color Dim = Color.FromArgb(90, 96, 116);

	public static readonly Color Green = Color.FromArgb(124, 230, 60);

	public static readonly Color Lime = Color.FromArgb(190, 255, 70);

	public static readonly Color Yellow = Color.FromArgb(246, 222, 70);

	public static readonly Color Red = Color.FromArgb(255, 86, 96);

	private static string ui;

	private static string uiBold;

	private static string uiBlack;

	private static string icons;

	private static string mono;

	public const string IcHome = "\ue80f";

	public const string IcMods = "\ue74c";

	public const string IcSettings = "\ue713";

	public const string IcConsole = "\ue756";

	public const string IcInfo = "\ue946";

	public const string IcFolder = "\ue838";

	public const string IcPlay = "\ue768";

	public const string IcAdd = "\ue710";

	public const string IcDelete = "\ue74d";

	public const string IcClose = "\ue8bb";

	public const string IcMin = "\ue921";

	public const string IcGlobe = "\ue774";

	public const string IcRefresh = "\ue72c";

	public const string IcCopy = "\ue8c8";

	public const string IcMemory = "\ue964";

	public const string IcMonitor = "\ue7f4";

	public const string IcCode = "\ue943";

	public const string IcSword = "\ue7c1";

	public const string IcLeaf = "\ue8be";

	public const string IcEye = "\ue7b3";

	public const string IcKeyboard = "\ue765";

	public const string IcStop = "\ue71a";

	public const string IcCheck = "\ue73e";

	public const string IcWarn = "\ue7ba";

	public const string IcPerson = "\ue77b";

	public const string IcCamera = "\ue722";

	public const string IcPicture = "\ueb9f";

	public const string IcDoc = "\ue8a5";

	public static int S(float v)
	{
		return (int)Math.Round(v * Scale);
	}

	public static float Sf(float v)
	{
		return v * Scale;
	}

	private static string Family(params string[] names)
	{
		using (InstalledFontCollection installedFontCollection = new InstalledFontCollection())
		{
			foreach (string text in names)
			{
				FontFamily[] families = installedFontCollection.Families;
				for (int j = 0; j < families.Length; j++)
				{
					if (families[j].Name.Equals(text, StringComparison.OrdinalIgnoreCase))
					{
						return text;
					}
				}
			}
		}
		return FontFamily.GenericSansSerif.Name;
	}

	public static void InitFonts()
	{
		ui = Family("Segoe UI Variable Text", "Segoe UI");
		uiBold = Family("Segoe UI Semibold", "Segoe UI");
		uiBlack = Family("Segoe UI Black", "Segoe UI");
		icons = Family("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");
		mono = Family("Cascadia Mono", "Consolas", "Courier New");
	}

	public static Font Reg(float px, FontStyle st = FontStyle.Regular)
	{
		return new Font(ui, Sf(px), st, GraphicsUnit.Pixel);
	}

	public static Font Bold(float px)
	{
		return new Font(uiBold, Sf(px), (uiBold == ui) ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
	}

	public static Font Black(float px)
	{
		return new Font(uiBlack, Sf(px), (uiBlack == ui) ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
	}

	public static Font Icon(float px)
	{
		return new Font(icons, Sf(px), FontStyle.Regular, GraphicsUnit.Pixel);
	}

	public static Font Mono(float px)
	{
		return new Font(mono, Sf(px), FontStyle.Regular, GraphicsUnit.Pixel);
	}

	public static GraphicsPath Round(RectangleF r, float rad)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		float num = Math.Min(rad * 2f, Math.Min(r.Width, r.Height));
		if (num <= 0.5f)
		{
			graphicsPath.AddRectangle(r);
			return graphicsPath;
		}
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	public static void Hq(Graphics g)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.CompositingQuality = CompositingQuality.HighQuality;
	}

	public static void FillRound(Graphics g, Brush b, RectangleF r, float rad)
	{
		using GraphicsPath path = Round(r, rad);
		g.FillPath(b, path);
	}

	public static void FillRound(Graphics g, Color c, RectangleF r, float rad)
	{
		using SolidBrush b = new SolidBrush(c);
		FillRound(g, b, r, rad);
	}

	public static void StrokeRound(Graphics g, Color c, RectangleF r, float rad, float w = 1f)
	{
		using GraphicsPath path = Round(r, rad);
		using Pen pen = new Pen(c, w);
		g.DrawPath(pen, path);
	}

	public static Brush Accent(RectangleF r, float angle = 0f)
	{
		if (r.Width < 1f)
		{
			r.Width = 1f;
		}
		if (r.Height < 1f)
		{
			r.Height = 1f;
		}
		return new LinearGradientBrush(r, Green, Yellow, angle);
	}

	public static Color Lerp(Color a, Color b, float t)
	{
		t = Math.Max(0f, Math.Min(1f, t));
		return Color.FromArgb((int)((float)(int)a.A + (float)(b.A - a.A) * t), (int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
	}

	public static Color A(Color c, int a)
	{
		return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c);
	}

	public static Image Res(string name)
	{
		Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MrBeanLauncher.Assets." + name);
		if (manifestResourceStream == null)
		{
			return null;
		}
		MemoryStream memoryStream = new MemoryStream();
		manifestResourceStream.CopyTo(memoryStream);
		memoryStream.Position = 0L;
		return Image.FromStream(memoryStream);
	}

	public static void Text(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center)
	{
		using StringFormat stringFormat = new StringFormat
		{
			Alignment = h,
			LineAlignment = v,
			Trimming = StringTrimming.EllipsisCharacter
		};
		using SolidBrush brush = new SolidBrush(c);
		stringFormat.FormatFlags |= StringFormatFlags.NoWrap;
		g.DrawString(s, f, brush, r, stringFormat);
	}

	public static void TextWrap(Graphics g, string s, Font f, Color c, RectangleF r)
	{
		using SolidBrush brush = new SolidBrush(c);
		g.DrawString(s, f, brush, r);
	}
}
