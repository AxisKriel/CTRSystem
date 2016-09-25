using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CTRSystem.Configuration;
using CTRSystem.DB;
using CTRSystem.Extensions;
using HttpServer;
using Rests;
using TShockAPI;
using TShockAPI.DB;

namespace CTRSystem
{
	public enum ReturnCode
	{
		DatabaseError = 0,
		Success = 200,
		NotFound = 404
	}

	public class CommandManager
	{
		private CTRS _main;
		private string spe = Commands.Specifier;
		public string Tag = TShock.Utils.ColorTag("CTRS:", Color.Purple);

		public CommandManager(CTRS main)
		{
			_main = main;
		}

		public async void Authenticate(CommandArgs args)
		{
			if (!args.Player.IsLoggedIn || args.Player.User == null)
			{
				args.Player.SendErrorMessage("You must be logged in to do that!");
				return;
			}

			Color c = Color.LightGreen;

			if (args.Player.ContainsData(Contributor.DataKey))
			{
				// Players can only authenticate their user account to one contributor forum account
				args.Player.SendMessage($"{Tag} You already authenticated this user account.", c);
				return;
			}

			if (args.Parameters.Count == 0)
			{
				args.Player.SendMessage($"{Tag} Usage: {spe}auth <code> OR {spe}auth <username> <password>", c);
				//args.Player.SendMessage($"You can get your code in your profile page at {TShock.Utils.ColorTag(CTRS.Config.AuthCodeGetURL, Color.White)}", c);
				args.Player.SendMessage($"{Tag} If using the credentials method, please enter data for an existing forum account.", c);
				args.Player.SendMessage($"{Tag} Authentication via code is currently disabled.", c);
			}
			else if (args.Parameters.Count == 1)
			{
				#region Code Authentication

				// Temporary lockdown until a proper interface is made (I highly doubt this will ever get done)
				args.Player.SendMessage($"{Tag} Authentication via code is currently disabled. Please use the forum credentials method.", c);
				return;

				//string authCode = args.Parameters[0];
				//WebClient client = new WebClient();
				//var sb = new StringBuilder();
				//sb.Append(CTRS.Config.AuthCodeHandlerURL);
				//sb.Append("?code=").Append(authCode);
				//sb.Append("&user=").Append(args.Player.User.ID);
				//string[] result = (await client.DownloadStringTaskAsync(sb.ToString())).Split(',');
				//ReturnCode code = (ReturnCode)Int32.Parse(result[0]);
				//if (code == ReturnCode.DatabaseError)
				//{
				//	args.Player.SendMessage("An error occurred while trying to contact the xenforo database. Wait a few minutes and try again.", c);
				//}
				//else if (code == ReturnCode.NotFound)
				//{
				//	args.Player.SendMessage("The auth code you entered was invalid. Make sure you've entered it correctly (NOTE: codes are case-sensitive).", c);
				//}
				//else
				//{
				//	contributor.XenforoID = Int32.Parse(result[1]);
				//	if (await CTRS.Contributors.UpdateAsync(contributor, ContributorUpdates.XenforoID))
				//		args.Player.SendMessage($"You have binded this account to {TShock.Utils.ColorTag("xenforo:" + contributor.XenforoID.Value.ToString(), Color.White)}.", c);
				//	else
				//		args.Player.SendMessage("Something went wrong with the database... contact an admin and try again later.", c);
				//}

				#endregion
			}
			else
			{
				string username = args.Parameters[0];
				string password = args.Parameters[1];

				AuthResult response;
				try
				{
					response = await _main.CredentialHelper.Authenticate(args.Player.User, new Credentials(username, password));
				}
				catch (Exception ex)
				{
					// Catching the exception should hopefully prevent unknown outcomes from crashing the server
					args.Player.SendErrorMessage("Something went wrong... contact an admin and try again later.");
					TShock.Log.ConsoleError(
						$"An error occurred while trying to authenticate player '{args.Player.Name}'. Exception message: {ex.Message}.");
					TShock.Log.Error(ex.ToString());
					return;
				}

				switch (response.Code)
				{
					case LMReturnCode.Success:
						// Store the contributor object to finish the authentication process
						Contributor contributor = response.Contributor;

						// Start listening to events
						contributor.Listen(_main, args.Player);

						// Store the contributor object
						args.Player.SetData(Contributor.DataKey, contributor);
						args.Player.SendSuccessMessage($"{Tag} You are now authenticated for the forum account '{username}'.");

						await contributor.UpdateNotifications();
						break;
					case LMReturnCode.EmptyParameter:
					case LMReturnCode.InsuficientParameters:
						args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: {spe}auth <username> <password>");
						break;
					case LMReturnCode.UserNotFound:
						args.Player.SendErrorMessage($"The user '{username}' was not found. Make sure to create a forum account beforehand.");
						break;
					case LMReturnCode.IncorrectData:
						args.Player.SendErrorMessage($"Invalid username or password!");
						break;
					case LMReturnCode.UnloadedCredentials:
						// Should never happen here
						break;
					case LMReturnCode.DatabaseError:
						args.Player.SendMessage($"{Tag} Something went wrong with the database... contact an admin and try again later.", c);
						break;
					case LMReturnCode.UserNotAContributor:
						args.Player.SendMessage($"{Tag} Currently, only contributors are able to connect their forum accounts to their game accounts.", c);
						break;
					case LMReturnCode.AccountLimitReached:
						args.Player.SendErrorMessage($"Account limit reached. Contact an admin to revoke authentication on a previous account.");
						break;
				}
			}
		}

		public async void CAdmin(CommandArgs args)
		{
			var cmds = new Dictionary<char, string>
			{
				['T'] = "force-tier-upgrade"
			};

			if (args.Parameters.Count < 1)
			{
				args.Player.SendInfoMessage("CTRS Administrative Actions:");
				foreach (var kvp in cmds)
				{
					args.Player.SendInfoMessage($"-{kvp.Key}	--{kvp.Value}");
				}
				return;
			}

			// This is the first administrative command. Should probably make proper regex if more are to come
			// NOTE: Do not use {Tag} for those, seeing as they are often ran from the console
			if (args.Parameters[0] == "-T" || args.Parameters[0].Equals("--force-tier-upgrade", StringComparison.OrdinalIgnoreCase))
			{
				args.Parameters.RemoveAt(0);
				User user;
				string id = String.Join(" ", args.Parameters).Trim();
				int uID;
				if (Int32.TryParse(id, out uID))
					user = TShock.Users.GetUserByID(uID);
				else
					user = TShock.Users.GetUserByName(id);
				if (user == null)
					args.Player.SendErrorMessage("Invalid user!");
				else
				{
					Contributor target;

					// Check if the player is online
					TSPlayer player = TShock.Players.FirstOrDefault(p => p.User == user);
					if (player != null)
						target = player.GetData<Contributor>(Contributor.DataKey);
					else
						target = await _main.Contributors.GetAsync(user.ID);

					if (target == null)
						args.Player.SendErrorMessage($"User '{user.Name}' is not a contributor.");
					else
					{
						target.Notifications |= Notifications.TierUpdate;
						await _main.Tiers.UpgradeTier(target);
						args.Player.SendSuccessMessage($"Forced a tier upgrade on contributor '{user.Name}'.");
					}
				}
			}
		}

		public async void Contributions(CommandArgs args)
		{
			// Even with the command permission, the player must be logged in and be a contributor to proceed
			if (!args.Player.IsLoggedIn || args.Player.User == null)
			{
				args.Player.SendErrorMessage("You must be logged in to do that!");
				return;
			}

			if (!args.Player.ContainsData(Contributor.DataKey))
			{
				args.Player.SendInfoMessage($"{Tag} You must be a contributor to use this command. Find out how to contribute to the server here: "
					+ _main.Config.GetContributeURL());
				args.Player.SendInfoMessage($"{Tag} If you've already sent a contribution, use the /auth command to get started.");
				return;
			}

			Contributor con = args.Player.GetData<Contributor>(Contributor.DataKey);
			if (args.Parameters.Count < 1 || args.Parameters[0] == "-i" || args.Parameters[0].Equals("info", StringComparison.OrdinalIgnoreCase))
			{
				// Info Command
				Tier tier = await _main.Tiers.GetAsync(con.Tier);
				Tier nextTier = null;
				try
				{
					nextTier = await _main.Tiers.GetAsync(con.Tier + 1);
				}
				catch (TierManager.TierNotFoundException)
				{
					// Keep it null
				}

				if (!con.XenforoId.HasValue)
				{
					args.Player.SendInfoMessage($"{Tag} Oops! It seems you're yet to authenticate to a valid forum account.");
					args.Player.SendInfoMessage($"{Tag} Use the {spe}auth <username> <password> command to authenticate first.");
					return;
				}

				float credits = await _main.XenforoUsers.GetContributorCredits(con);
				args.Player.SendMessage($"{Tag} Contributions Track & Reward System v{_main.Version}", Color.LightGreen);
				foreach (string s in Texts.SplitIntoLines(_main.Formatter.FormatInfo(args.Player, con, credits, tier, nextTier)))
				{
					args.Player.SendInfoMessage($"{Tag} {s}");
				}
			}
			else if (args.Parameters[0] == "-C" || args.Parameters[0].Equals("cmds", StringComparison.OrdinalIgnoreCase)
				|| args.Parameters[0].Equals("commands", StringComparison.OrdinalIgnoreCase))
			{
				args.Player.SendInfoMessage($"{Tag} Unlocked commands:");
				args.Player.SendInfoMessage("{0} -c Color     [{1}]", Tag,
					(con.Settings & Settings.CanChangeColor) == Settings.CanChangeColor ? "x" : " ");
				#region DEBUG
#if DEBUG
				args.Player.SendInfoMessage($"{Tag} Settings value: " + (int)con.Settings + " | " + con.Settings.ToString());
#endif
				#endregion
			}
			else
			{
				var regex = new Regex(@"^\w+ (?:-c|color) ?((?<RGB>\d{1,3},\d{1,3},\d{1,3})|(?<Remove>-r|remove))?$");
				var match = regex.Match(args.Message);
				if (!match.Success)
				{
					args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: {spe}ctrs <info/color> [rrr,ggg,bbb]");
					return;
				}

				// If command restrictions are on, check Settings.CanChangeColor
				if (_main.Config.RestrictCommands && (con.Settings & Settings.CanChangeColor) != Settings.CanChangeColor)
				{
					args.Player.SendWarningMessage($"{Tag} You don't have permission to set chat colors!");
					if (!String.IsNullOrWhiteSpace(_main.Config.Texts.RestrictedColorTip))
						args.Player.SendInfoMessage($"{Tag} {_main.Config.Texts.RestrictedColorTip}");
					return;
				}

				// Color command
				if (!String.IsNullOrEmpty(match.Groups["Remove"].Value))
				{
					con.ChatColor = null;
					if (await _main.Contributors.UpdateAsync(con, ContributorUpdates.ChatColor))
						args.Player.SendSuccessMessage($"{Tag} You are now using your group's default chat color.");
					else
						args.Player.SendErrorMessage($"Something went wrong while trying to contact our database. Please inform an administrator.");
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
						args.Player.SendErrorMessage($"Invalid color format! Proper format: RRR,GGG,BBB");
					else
					{
						con.ChatColor = color;
						if (await _main.Contributors.UpdateAsync(con, ContributorUpdates.ChatColor))
						{
							string colorString = TShock.Utils.ColorTag(Tools.ColorToRGB(con.ChatColor), con.ChatColor.Value);
							args.Player.SendSuccessMessage($"{Tag} Your chat color is now set to {colorString}.");
						}
						else
							args.Player.SendErrorMessage($"Something went wrong while trying to contact our database. Please inform an administrator.");
					}
				}
			}
		}
	}
}
