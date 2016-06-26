using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Text;

namespace RestRequestUI
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private static Window _active;
		public static Window ActiveWindow
		{
			get { return _active; }
			set
			{
				_active = value;
				//_active.GlobalActivate();
			}
		}

		public static string CompleteRequest(string serverIp, int port, string request, string user = null, string credits = null, string date = null, string token = null)
		{
			var sb = new StringBuilder();
			if (!serverIp.StartsWith("http://"))
				sb.Append("http://");
			sb.Append(serverIp);
			sb.Append(':');
			sb.Append(port);
			if (!request.StartsWith("/"))
				sb.Append('/');

			sb.Append(ParseParameters(request, user, credits, date, token));
			return sb.ToString();
		}

		public static string ParseParameters(string s, string user = null, string credits = null, string date = null, string token = null)
		{
			var dat = new Dictionary<string, string>
			{
				["token"] = token,
				["user"] = user ?? "",
				["credits"] = credits ?? "",
				["date"] = date ?? ""
			};

			foreach (var kvp in dat)
			{
				s = s.Replace($"${kvp.Key}", kvp.Value);
			}

			return s;
		}
	}
}
