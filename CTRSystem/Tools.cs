using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTRSystem
{
	public static class Tools
	{
		public static Color? ColorFromRGB(string s)
		{
			if (String.IsNullOrWhiteSpace(s))
				return null;

			byte r, g, b;
			string[] parts = s.Trim().Split(',');
			if (parts.Length != 3)
				return null;

			if (!Byte.TryParse(parts[0], out r))
				r = 255;
			if (!Byte.TryParse(parts[1], out g))
				g = 255;
			if (!Byte.TryParse(parts[2], out b))
				b = 255;

			return new Color(r, g, b);
		}

		public static string ColorToRGB(Color? c)
		{
			if (!c.HasValue)
				return "";
			return ColorToRGB(c.Value);
		}

		public static string ColorToRGB(Color c)
		{
			string[] colors = new[] { c.R.ToString(), c.G.ToString(), c.B.ToString() };
			return String.Join(",", colors);
		}
	}
}
