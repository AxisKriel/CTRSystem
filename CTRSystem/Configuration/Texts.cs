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

		public string Introduction = "Welcome, @player! Thank you for contributing!";

		public string Info = "You currently have {2}";

		public string NewTier = "";

		public string NewDonation = "";

		public string FormatIntroduction(TSPlayer player, Contributor contributor)
		{
			return String.Format(Introduction,
				player.Name);
		}

		/// <summary>
		/// Formats the info message sent to players when they use the info command.
		/// {0} - Player Name
		/// {1} - UserID
		/// {2} - WebID
		/// {3} - Credits
		/// {4} - TotalCredits
		/// {5} - LastDonation (dd-MMM-yyyy)
		/// {6} - Tier.Name with ChatColor
		/// {7} - NextTier.Name
		/// {8} - CreditsForNextTier
		/// {9} - ChatColor
		/// </summary>
		/// <param name="player">The contributor to take elements from.</param>
		/// <returns>The formatted string.</returns>
		public string FormatInfo(TSPlayer player, Contributor contributor, float credits, Tier tier = null, Tier nextTier = null)
		{

			return String.Format(Info,
				player.Name,
				contributor.UserID.HasValue ? contributor.UserID.Value.ToString() : "N/A",
				contributor.XenforoID.HasValue ? contributor.XenforoID.Value.ToString() : "N/A",
				String.Format(CTRS.Config.CreditsFormat, credits),
				String.Format(CTRS.Config.CreditsFormat, contributor.TotalCredits),
				contributor.LastDonation == DateTime.MinValue ? "N/A" : contributor.LastDonation.ToString("d-MMM-yyyy"),
				tier != null ? tier.ChatColor.HasValue ? TShock.Utils.ColorTag(tier.Name, tier.ChatColor.Value) : tier.Name : "N/A",
				nextTier != null ? nextTier.ChatColor.HasValue ? TShock.Utils.ColorTag(nextTier.Name, nextTier.ChatColor.Value) : nextTier.Name : "N/A",
				(nextTier != null && tier != null) ? (nextTier.CreditsRequired - credits).ToString() : "N/A",
				Tools.ColorToRGB(contributor.ChatColor));
		}

		public string FormatNewTier(TSPlayer player, Contributor contributor, Tier tier)
		{
			return String.Format(NewTier);
		}

		public string FormatNewDonation(TSPlayer player, Contributor contributor, float amount)
		{
			return String.Format(NewDonation);
		}
	}
}
