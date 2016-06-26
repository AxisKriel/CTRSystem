using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using CTRSystem.DB;

namespace CTRSystem.Configuration
{
	public class Texts
	{
		/// <summary>
		/// Separates a line when parsed by <see cref="SplitIntoLines(string)"/>.
		/// </summary>
		public static string NewLine = "|n";

		/// <summary>
		/// Splits a multiline string into a list of lines. Lines are separated by <see cref="NewLine"/>.
		/// </summary>
		/// <param name="s">The string to parse.</param>
		/// <returns>The resulting list of lines.</returns>
		public static List<string> SplitIntoLines(string s)
		{
			string[] lines = s.Split(new[] { "|n" }, StringSplitOptions.RemoveEmptyEntries);
			return lines.ToList();
		}

		public string Introduction = "Thank you for contributing to Saybrook's Planet! Enjoy your new perks as you join the Contributors!";

		public string Info = "You currently have {3}. You are a {6} Contributor, contributing with a total of {4} over your lifespan.";

		public string NewTier = "Congratulations! You have advanced to the {1} Contributor rank and have increased your experience multiplier!";

		public string NewDonation = "Welcome back to Saybrook's Planet! We've just received a contribution of {1} that was applied to your account!";

		public string RestrictedColorTip = "You can purchase this command with credits. Contact an admin for details.";

		// 0 - player name
		public string FormatIntroduction(TSPlayer player, Contributor contributor)
		{
			return String.Format(Introduction,
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

			return String.Format(Info,
				player.Name,
				contributor.Accounts.Count == 0 ? "N/A" : String.Join(",", contributor.Accounts),
				contributor.XenforoID.HasValue ? contributor.XenforoID.Value.ToString() : "N/A",
				String.Format(CTRS.Config.CreditsFormat, (int)credits),
				String.Format(CTRS.Config.CreditsFormat, (int)contributor.TotalCredits),
				contributor.LastDonation == DateTime.MinValue ? "N/A" : contributor.LastDonation.ToString("d-MMM-yyyy"),
				tier != null ? tier.ChatColor.HasValue ? TShock.Utils.ColorTag(tier.Name, tier.ChatColor.Value) : tier.Name : "N/A",
				nextTier != null ? nextTier.ChatColor.HasValue ? TShock.Utils.ColorTag(nextTier.Name, nextTier.ChatColor.Value) : nextTier.Name : "N/A",
				String.Format(CTRS.Config.CreditsFormat, (nextTier != null && tier != null) ? ((int)(nextTier.CreditsRequired - credits)).ToString() : "N/A"),
				Tools.ColorToRGB(contributor.ChatColor),
				tier != null ? $"{Math.Round(tier.ExperienceMultiplier * 100 - 100)}%" : "0%",
				tier != null ? $"{Math.Round(tier.ExperienceMultiplier * 100)}%" : "100%");
		}

		// 0 - player name | 1 - Tier.Name with ChatColor
		public string FormatNewTier(TSPlayer player, Contributor contributor, Tier tier)
		{
			return String.Format(NewTier, player.Name, tier.ChatColor.HasValue ? TShock.Utils.ColorTag(tier.Name, tier.ChatColor.Value) : tier.Name);
		}

		// 0 - player name | 1 - amount formatted to credits
		public string FormatNewDonation(TSPlayer player, Contributor contributor, float amount)
		{
			return String.Format(NewDonation, player.Name, String.Format(CTRS.Config.CreditsFormat, (int)amount));
		}
	}
}
