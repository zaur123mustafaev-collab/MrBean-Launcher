using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MrBeanLauncher.Ui;

public static class SkinFaces
{
	private static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

	public static event Action Updated = () =>
	{
	};

	public static Image Get(MainForm f, string uuid, string url)
	{
		if (string.IsNullOrEmpty(uuid))
		{
			return null;
		}
		if (cache.TryGetValue(uuid, out var value))
		{
			return value;
		}
		cache[uuid] = null;
		string file = Path.Combine(f.Root, "cache", "skins", uuid + ".png");
		try
		{
			if (File.Exists(file))
			{
				cache[uuid] = Face(Image.FromStream(new MemoryStream(File.ReadAllBytes(file))));
				return cache[uuid];
			}
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(url))
		{
			return null;
		}
		Task.Run(async () =>
		{
			try
			{
				byte[] array = await f.Auth.Download(url.Replace("http://", "https://"), CancellationToken.None);
				Directory.CreateDirectory(Path.GetDirectoryName(file));
				File.WriteAllBytes(file, array);
				Image face = Face(Image.FromStream(new MemoryStream(array)));
				f.BeginInvoke((Action)(() =>
				{
					cache[uuid] = face;
					Updated();
				}));
			}
			catch
			{
			}
		});
		return null;
	}

	private static Image Face(Image skin)
	{
		Bitmap bitmap = new Bitmap(64, 64);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
		graphics.PixelOffsetMode = PixelOffsetMode.Half;
		graphics.DrawImage(skin, new Rectangle(0, 0, 64, 64), new Rectangle(8, 8, 8, 8), GraphicsUnit.Pixel);
		graphics.DrawImage(skin, new Rectangle(0, 0, 64, 64), new Rectangle(40, 8, 8, 8), GraphicsUnit.Pixel);
		return bitmap;
	}

	public static void DrawHead(Graphics g, Image face, RectangleF r, string name)
	{
		using GraphicsPath clip = Theme.Round(r, r.Width * 0.22f);
		GraphicsState gstate = g.Save();
		g.SetClip(clip);
		if (face != null)
		{
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = PixelOffsetMode.Half;
			g.DrawImage(face, r);
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		}
		else
		{
			using (Brush brush = Theme.Accent(r, 45f))
			{
				g.FillRectangle(brush, r);
			}
			using Font f = Theme.Black(r.Height * 0.5f / Theme.Scale);
			Theme.Text(g, string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper(), f, Color.FromArgb(14, 18, 10), r, StringAlignment.Center);
		}
		g.Restore(gstate);
	}
}
