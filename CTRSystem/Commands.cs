using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CTRSystem.Configuration;
using CTRSystem.DB;
using TShockAPI;

namespace CTRSystem
{
	public class Commands
	{
		private static string spe = TShockAPI.Commands.Specifier;
		public static string Tag = TShock.Utils.ColorTag("CTRS:", Color.Purple);

		public static async void Contributions(CommandArgs args)
		{
			// Even with the command permission, the player must be logged in and be a contributor to proceed
			if (!args.Player.IsLoggedIn || args.Player.User == null)
			{
				args.Player.SendErrorMessage("You must be logged in to use this command!");
				return;
			}

			Contributor con;
			try
			{
				con = await CTRS.Contributors.GetAsync(args.Player.User.ID);
			}
			catch (ContributorManager.ContributorNotFoundException)
			{
				args.Player.SendInfoMessage($"{Tag} You must be a contributor to use this command. Find out how to contribute to the server here: "
					+ CTRS.Config.GetContributeURL());
				args.Player.SendInfoMessage($"{Tag} If you've already sent a contribution, contact an administrator to receive your privileges.");
				return;
			}

			if (args.Parameters.Count < 1 || args.Parameters[0] == "-i" || args.Parameters[0].Equals("info", StringComparison.OrdinalIgnoreCase))
			{
				// Info Command
				Tier tier = await CTRS.Tiers.GetAsync(con.Tier);
				Tier nextTier = null;
				try
				{
					nextTier = await CTRS.Tiers.GetAsync(con.Tier + 1);
				}
				catch (TierManager.TierNotFoundException)
				{
					// Keep it null
				}

				args.Player.SendMessage($"{Tag} Contributions Track & Reward System v{CTRS.PublicVersion}", Color.LightGreen);
				foreach (string s in Texts.SplitIntoLines(CTRS.Config.Texts.FormatInfo(con, tier, nextTier)))
				{
					args.Player.SendInfoMessage($"{Tag} {s}");
				}
			}
			else
			{
				var regex = new Regex(@"^\w+ (?:-c|color) ?((?<RGB>\d{1,3},\d{1,3},\d{1,3})|(?<Remove>-r|remove))?$");
				var match = regex.Match(args.Message);
				if (!match.Success)
				{
					args.Player.SendErrorMessage($"{Tag} Invalid syntax! Proper syntax: {spe}ctrs <info/color> [rrr,ggg,bbb]");
					return;
				}

				// Color command
				if (!String.IsNullOrEmpty(match.Groups["Remove"].Value))
				{
					con.ChatColor = null;
					if (await CTRS.Contributors.UpdateAsync(con, ContributorUpdates.ChatColor))
						args.Player.SendSuccessMessage($"{Tag} You are now using your group's default chat color.");
					else
						args.Player.SendErrorMessage($"{Tag} Something went wrong while trying to contact our database. Please inform an administrator.");
				}
				else if (String.IsNullOrEmpty(match.Groups["RGB"].Value))
				{
					if (!con.ChatColor.HasValue)
					{
						args.Player.SendInfoMessage($"{Tag} You are currently using your group's chat color.");
						args.Player.SendInfoMessage($"{Tag} You may set a custom chat color with {spe}ctrs color RRR,GGG,BBB.");
					}
					else
					{
						string colorString = TShock.Utils.ColorTag(Tools.ColorToRGB(con.ChatColor), con.ChatColor.Value);
						args.Player.SendInfoMessage($"{Tag} Your chat color is currently set to {colorString}.");
						args.Player.SendInfoMessage($"{Tag} You may change it with {spe}ctrs color RRR,GGG,BBB or remove it with {spe}ctrs color -r/remove.");
					}
				}
				else
				{
					Color? color = Tools.ColorFromRGB(match.Groups["RGB"].Value);
					if (!color.HasValue)
						args.Player.SendErrorMessage($"{Tag} Invalid color format! Proper format: RRR,GGG,BBB");
					else
					{
						con.ChatColor = color;
						if (await CTRS.Contributors.UpdateAsync(con, ContributorUpdates.ChatColor))
						{
							string colorString = TShock.Utils.ColorTag(Tools.ColorToRGB(con.ChatColor), con.ChatColor.Value);
							args.Player.SendSuccessMessage($"{Tag} Your chat color is now set to {colorString}.");
						}
						else
							args.Player.SendErrorMessage($"{Tag} Something went wrong while trying to contact our database. Please inform an administrator.");
					}
				}
			}
		}
	}
}
