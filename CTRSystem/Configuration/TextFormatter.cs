using System;
using CTRSystem.DB;
using TShockAPI;

namespace CTRSystem.Configuration
{
	public class TextFormatter
	{
		private CTRS _main;
		private ConfigFile _config;

		public TextFormatter(CTRS main, ConfigFile config)
		{
			_main = main;
			_config = config;
		}

		// 0 - player name
		public string FormatIntroduction(TSPlayer player, Contributor contributor)
		{
			return String.Format(_config.Texts.Introduction,
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

			return String.Format(_config.Texts.Info,
				player.Name,
				contributor.Accounts.Count == 0 ? "N/A" : String.Join(",", contributor.Accounts),
				contributor.XenforoId.HasValue ? contributor.XenforoId.Value.ToString() : "N/A",
				String.Format(_main.Config.CreditsFormat, (int)credits),
				String.Format(_main.Config.CreditsFormat, (int)contributor.TotalCredits),
				!contributor.LastDonation.HasValue ? "N/A" : contributor.LastDonation.Value.ToString("d-MMM-yyyy"),
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
			return String.Format(_config.Texts.NewTier, player.Name, tier.ChatColor.HasValue ? TShock.Utils.ColorTag(tier.Name, tier.ChatColor.Value) : tier.Name);
		}

		// 0 - player name | 1 - amount formatted to credits
		public string FormatNewDonation(TSPlayer player, Contributor contributor, float amount)
		{
			return String.Format(_config.Texts.NewDonation, player.Name, String.Format(_main.Config.CreditsFormat, (int)amount));
		}
	}
}
