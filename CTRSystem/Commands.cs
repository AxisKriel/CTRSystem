using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CTRSystem.Configuration;
using CTRSystem.DB;
using TShockAPI;
using Rests;
using TShockAPI.DB;
using HttpServer;
using System.Net;

namespace CTRSystem
{
	public enum ReturnCode
	{
		DatabaseError = 0,
		Success = 200,
		NotFound = 404
	}

	public class Commands
	{
		private static string spe = TShockAPI.Commands.Specifier;
		public static string Tag = TShock.Utils.ColorTag("CTRS:", Color.Purple);

		public static async void Authenticate(CommandArgs args)
		{
			if (!args.Player.IsLoggedIn || args.Player.User == null)
			{
				args.Player.SendErrorMessage("You must be logged in to do that!");
				return;
			}

			Color c = Color.LightGreen;

			// Only contributors may bind their accounts but this can be changed in the future
			Contributor contributor = await CTRS.Contributors.GetAsync(args.Player.User.ID);
			if (contributor == null)
			{
				args.Player.SendMessage($"{Tag} Currently, only contributors are able to connect their forum accounts to their game accounts.", c);
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
				// Temporary lockdown until a proper interface is made
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
			}
			else
			{
				string username = args.Parameters[0];
				string password = args.Parameters[1];

				Credentials cred = CTRS.CredentialHelper.Get(args.Player);
				if (cred == null)
					CTRS.CredentialHelper.AddPlayer(args.Player);

				bool update = CTRS.CredentialHelper.Update(args.Player, username, password);
				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("AUTH UPDATE: " + update.ToString());
#endif
				#endregion
				LMReturnCode response = await CTRS.CredentialHelper.Authenticate(args.Player, contributor);
				switch (response)
				{
					case LMReturnCode.Success:
						bool success = await CTRS.XenforoUsers.SetTShockID(contributor.XenforoID.Value, contributor.UserID.Value);
						#region DEBUG
#if DEBUG
						TShock.Log.ConsoleInfo($"CTRS-AUTH: Set TShockID for Contributor {contributor.UserID.Value}? {success}");
#endif
						#endregion
						if (success)
							args.Player.SendSuccessMessage($"{Tag} You are now authenticated for the forum account '{username}'.");
						else
							goto case LMReturnCode.DatabaseError;
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
				}
			}
		}

		public static async void Contributions(CommandArgs args)
		{
			// Even with the command permission, the player must be logged in and be a contributor to proceed
			if (!args.Player.IsLoggedIn || args.Player.User == null)
			{
				args.Player.SendErrorMessage("You must be logged in to do that!");
				return;
			}

			Contributor con = await CTRS.Contributors.GetAsync(args.Player.User.ID);
			if (con == null)
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

				XFUser xfuser;
				if (!con.XenforoID.HasValue || (xfuser = await CTRS.XenforoUsers.GetAsync(args.Player.User.ID)) == null)
				{
					args.Player.SendInfoMessage($"{Tag} Oops! It seems you're yet to authenticate to a valid forum account.");
					args.Player.SendInfoMessage($"{Tag} Use the {spe}auth <username> <password> command to authenticate first.");
					return;
				}

				// Finish this info message and then proceed with tests
				args.Player.SendMessage($"{Tag} Contributions Track & Reward System v{CTRS.PublicVersion}", Color.LightGreen);
				foreach (string s in Texts.SplitIntoLines(CTRS.Config.Texts.FormatInfo(args.Player, con, xfuser.Credits, tier, nextTier)))
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
						if (await CTRS.Contributors.UpdateAsync(con, ContributorUpdates.ChatColor))
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

		public static void RestBindUser(RestRequestArgs args)
		{

		}

		public static void RestNewTransaction(RestRequestArgs args)
		{

		}

		#region REST Utility Methods

		private static RestObject RestError(string message, string status = "400")
		{
			return new RestObject(status) { Error = message };
		}

		private static RestObject RestResponse(string message, string status = "200")
		{
			return new RestObject(status) { Response = message };
		}

		private static RestObject RestMissingParam(string var)
		{
			return RestError("Missing or empty " + var + " parameter");
		}

		private static RestObject RestMissingParam(params string[] vars)
		{
			return RestMissingParam(string.Join(", ", vars));
		}

		private static RestObject RestInvalidParam(string var)
		{
			return RestError("Missing or invalid " + var + " parameter");
		}

		private static bool GetBool(string val, bool def)
		{
			bool ret;
			return bool.TryParse(val, out ret) ? ret : def;
		}

		private static object PlayerFind(IParameterCollection parameters)
		{
			string name = parameters["player"];
			if (string.IsNullOrWhiteSpace(name))
				return RestMissingParam("player");

			var found = TShock.Utils.FindPlayer(name);
			switch (found.Count)
			{
				case 1:
					return found[0];
				case 0:
					return RestError("Player " + name + " was not found");
				default:
					return RestError("Player " + name + " matches " + found.Count + " players");
			}
		}

		private static object UserFind(IParameterCollection parameters)
		{
			string name = parameters["user"];
			if (string.IsNullOrWhiteSpace(name))
				return RestMissingParam("user");

			User user;
			string type = parameters["type"];
			try
			{
				switch (type)
				{
					case null:
					case "name":
						type = "name";
						user = TShock.Users.GetUserByName(name);
						break;
					case "id":
						user = TShock.Users.GetUserByID(Convert.ToInt32(name));
						break;
					default:
						return RestError("Invalid Type: '" + type + "'");
				}
			}
			catch (Exception e)
			{
				return RestError(e.Message);
			}

			if (null == user)
				return RestError(String.Format("User {0} '{1}' doesn't exist", type, name));

			return user;
		}

		private static object BanFind(IParameterCollection parameters)
		{
			string name = parameters["ban"];
			if (string.IsNullOrWhiteSpace(name))
				return RestMissingParam("ban");

			string type = parameters["type"];
			if (string.IsNullOrWhiteSpace(type))
				return RestMissingParam("type");

			Ban ban;
			switch (type)
			{
				case "ip":
					ban = TShock.Bans.GetBanByIp(name);
					break;
				case "name":
					ban = TShock.Bans.GetBanByName(name, GetBool(parameters["caseinsensitive"], true));
					break;
				default:
					return RestError("Invalid Type: '" + type + "'");
			}

			if (null == ban)
				return RestError("Ban " + type + " '" + name + "' doesn't exist");

			return ban;
		}

		private static object GroupFind(IParameterCollection parameters)
		{
			var name = parameters["group"];
			if (string.IsNullOrWhiteSpace(name))
				return RestMissingParam("group");

			var group = TShock.Groups.GetGroupByName(name);
			if (null == group)
				return RestError("Group '" + name + "' doesn't exist");

			return group;
		}

		private static Dictionary<string, object> PlayerFilter(TSPlayer tsPlayer, IParameterCollection parameters, bool viewips = false)
		{
			var player = new Dictionary<string, object>
				{
					{"nickname", tsPlayer.Name},
					{"username", tsPlayer.User == null ? "" : tsPlayer.User.Name},
					{"group", tsPlayer.Group.Name},
					{"active", tsPlayer.Active},
					{"state", tsPlayer.State},
					{"team", tsPlayer.Team},
				};

			if (viewips)
			{
				player.Add("ip", tsPlayer.IP);
			}
			foreach (IParameter filter in parameters)
			{
				if (player.ContainsKey(filter.Name) && !player[filter.Name].Equals(filter.Value))
					return null;
			}
			return player;
		}

		private static object PlayerSetMute(IParameterCollection parameters, bool mute)
		{
			var ret = PlayerFind(parameters);
			if (ret is RestObject)
				return ret;

			TSPlayer player = (TSPlayer)ret;
			player.mute = mute;
			var verb = mute ? "muted" : "unmuted";
			player.SendInfoMessage("You have been remotely " + verb);
			return RestResponse("Player " + player.Name + " was " + verb);
		}

		#endregion
	}
}
