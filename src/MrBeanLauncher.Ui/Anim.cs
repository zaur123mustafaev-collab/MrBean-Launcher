using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MrBeanLauncher.Ui;

public static class Anim
{
	private static readonly Timer timer;

	private static readonly List<XControl> list;

	static Anim()
	{
		timer = new Timer
		{
			Interval = 15
		};
		list = new List<XControl>();
		timer.Tick += (object s, EventArgs e) =>
		{
			XControl[] array = list.ToArray();
			foreach (XControl xControl in array)
			{
				if (!xControl.IsDisposed && xControl.Visible)
				{
					xControl.Step();
				}
			}
		};
	}

	public static void Add(XControl c)
	{
		list.Add(c);
		if (!timer.Enabled)
		{
			timer.Start();
		}
	}

	public static void Remove(XControl c)
	{
		list.Remove(c);
	}
}
