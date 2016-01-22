using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace CTRSystem.Configuration
{
	public class ConfigFile
	{
		public string StorageType { get; set; } = "sqlite";

		public string MySqlHost { get; set; } = "localhost:3306";

		public string MySqlDbName { get; set; } = "";

		public string MySqlUsername { get; set; } = "";

		public string MySqlPassword { get; set; } = "";

		public bool LogDatabaseErrors { get; set; } = true;

		public string CreditsFormat { get; set; } = "{0} credit(s)";

		public List<string> AdditionalCommandAliases { get; set; } = new List<string>
		{
			"ctrsystem",
			"contributions"
		};

		public string ContributorChatFormat { get; set; } = TShock.Config.ChatFormat;

		public string ContributeURL { get; set; } = "sbplanet.co/forums/index.php?adcredits/packages";

		public string AuthCodeGetURL { get; set; } = "sbplanet.co/forums/link-account.php";

		public string AuthCodeHandlerURL { get; set; } = "http://sbplanet.co/auth-handler.php";

		public int NotificationCheckSeconds { get; set; } = 30;

		public int TierRefreshMinutes { get; set; } = 30;

		public XenforoConfig Xenforo { get; set; } = new XenforoConfig();

		public Texts Texts { get; set; } = new Texts();

		public static ConfigFile Read(string path)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				if (!File.Exists(path))
				{
					ConfigFile config = new ConfigFile();
					File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
					return config;
				}
				else
					return JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(path));
			}
			catch (Exception e)
			{
				TShock.Log.ConsoleError("CTRS-Config: " + e.Message);
				TShock.Log.Error(e.ToString());
				return new ConfigFile();
			}
		}

		public string GetContributeURL()
		{
			return TShock.Utils.ColorTag(ContributeURL, Color.LightGreen);
		}
	}
}
