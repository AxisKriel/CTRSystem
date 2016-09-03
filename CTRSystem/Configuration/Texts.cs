using System;
using System.Collections.Generic;
using System.Linq;
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
	}
}
