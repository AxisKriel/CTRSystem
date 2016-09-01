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
		public int AccountLimit { get; set; } = 0;

		public string StorageType { get; set; } = "sqlite";

		public string MySqlHost { get; set; } = "localhost:3306";

		public string MySqlDbName { get; set; } = "";

		public string MySqlUsername { get; set; } = "";

		public string MySqlPassword { get; set; } = "";

		public string ContributorTableName { get; set; } = "Contributors";

		public string ContributorAccountsTableName { get; set; } = "Contributors_Accounts";

		public string TierTableName { get; set; } = "Tiers";

		public bool LogDatabaseErrors { get; set; } = true;

		public string CreditsFormat { get; set; } = "{0} credit(s)";

		public string[] AdditionalCommandAliases { get; set; } = new []
		{
			"ctrsystem",
			"contributions"
		};

		public string ContributorChatFormat { get; set; } = TShock.Config.ChatFormat;

		public string ContributeURL { get; set; } = "sbplanet.co/forums/index.php?adcredits/packages";

		public string AuthCodeGetURL { get; set; } = "sbplanet.co/forums/link-account.php";

		public string AuthCodeHandlerURL { get; set; } = "http://sbplanet.co/authenticate.php";

		public bool RestrictCommands { get; set; } = false;

		/// <summary>
		/// Number of seconds to wait before sending the first notification message.
		/// </summary>
		public int NotificationDelaySeconds { get; set; } = 5;

		/// <summary>
		/// Number of seconds in between consecutive notification messages.
		/// </summary>
		public int NotificationCheckSeconds { get; set; } = 3;

		public int TierRefreshMinutes { get; set; } = 30;

		public XenforoConfig Xenforo { get; set; } = new XenforoConfig();

		public Texts Texts { get; set; } = new Texts();

		public static ConfigFile Read(string path)
		{
			if (String.IsNullOrWhiteSpace(path))
			{
				TShock.Log.ConsoleError("CTRS-Config: Invalid filepath given. Starting default configuration...");
				return new ConfigFile();
			}

			Directory.CreateDirectory(Path.GetDirectoryName(path));
			try
			{
				ConfigFile file = new ConfigFile();
				if (File.Exists(path))
				{
					file = JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(path));
				}
				File.WriteAllText(path, JsonConvert.SerializeObject(file, Formatting.Indented));
				return file;
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
