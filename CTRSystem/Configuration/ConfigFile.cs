using System;
using System.IO;
using CTRSystem.DB;
using Newtonsoft.Json;
using TShockAPI;

namespace CTRSystem.Configuration
{
	public class ConfigFile
	{
		private CTRS _main;

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

		public static ConfigFile Read(CTRS main, string path)
		{
			if (String.IsNullOrWhiteSpace(path))
			{
				TShock.Log.ConsoleError("CTRS-Config: Invalid filepath given. Starting default configuration...");
				return new ConfigFile() { _main = main };
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
				file._main = main;
				return file;
			}
			catch (Exception e)
			{
				TShock.Log.ConsoleError("CTRS-Config: " + e.Message);
				TShock.Log.Error(e.ToString());
				return new ConfigFile() { _main = main };
			}
		}

		public string GetContributeURL()
		{
			return TShock.Utils.ColorTag(ContributeURL, Color.LightGreen);
		}

		// 0 - player name
		public string FormatIntroduction(TSPlayer player, Contributor contributor)
		{
			return String.Format(Texts.Introduction,
				player.Name);
		}

		/// <summary>
		/// Formats the info message sent to players when they use the info command.
		/// {0} - Player Name
		/// {1} - Accounts (comma separated if more than one)
		/// {2} - WebID
		/// {3} - Credits
		/// {4} - TotalCredits
		/// {5} - LastDonation (dd-MMM-yyyy)
		/// {6} - Tier.Name with ChatColor
		/// {7} - NextTier.Name
		/// {8} - CreditsForNextTier
		/// {9} - ChatColor
		/// {10} - Experience Multiplier in percentage aditive: 1.10 = '10%'
		/// {11} - Experience Multiplier in percentage total: 1.10 = '110%'
		/// </summary>
		/// <param name="player">The contributor to take elements from.</param>
		/// <returns>The formatted string.</returns>
		public string FormatInfo(TSPlayer player, Contributor contributor, float credits, Tier tier = null, Tier nextTier = null)
		{

			return String.Format(Texts.Info,
				player.Name,
				contributor.Accounts.Count == 0 ? "N/A" : String.Join(",", contributor.Accounts),
				contributor.XenforoId.HasValue ? contributor.XenforoId.Value.ToString() : "N/A",
				String.Format(_main.Config.CreditsFormat, (int)credits),
				String.Format(_main.Config.CreditsFormat, (int)contributor.TotalCredits),
				contributor.LastDonation == DateTime.MinValue ? "N/A" : contributor.LastDonation.ToString("d-MMM-yyyy"),
				tier != null ? tier.ChatColor.HasValue ? TShock.Utils.ColorTag(tier.Name, tier.ChatColor.Value) : tier.Name : "N/A",
				nextTier != null ? nextTier.ChatColor.HasValue ? TShock.Utils.ColorTag(nextTier.Name, nextTier.ChatColor.Value) : nextTier.Name : "N/A",
				String.Format(_main.Config.CreditsFormat, (nextTier != null && tier != null) ? ((int)(nextTier.CreditsRequired - credits)).ToString() : "N/A"),
				Tools.ColorToRGB(contributor.ChatColor),
				tier != null ? $"{Math.Round(tier.ExperienceMultiplier * 100 - 100)}%" : "0%",
				tier != null ? $"{Math.Round(tier.ExperienceMultiplier * 100)}%" : "100%");
		}

		// 0 - player name | 1 - Tier.Name with ChatColor
		public string FormatNewTier(TSPlayer player, Contributor contributor, Tier tier)
		{
			return String.Format(Texts.NewTier, player.Name, tier.ChatColor.HasValue ? TShock.Utils.ColorTag(tier.Name, tier.ChatColor.Value) : tier.Name);
		}

		// 0 - player name | 1 - amount formatted to credits
		public string FormatNewDonation(TSPlayer player, Contributor contributor, float amount)
		{
			return String.Format(Texts.NewDonation, player.Name, String.Format(_main.Config.CreditsFormat, (int)amount));
		}
	}
}
