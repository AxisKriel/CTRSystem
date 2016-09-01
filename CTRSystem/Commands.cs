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
using CTRSystem.Extensions;

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

			//Contributor contributor = await CTRS.Contributors.GetAsync(args.Player.User.ID);
			//if (contributor == null)
			//{
			//	args.Player.SendMessage($"{Tag} Currently, only contributors are able to connect their forum accounts to their game accounts.", c);
			//	return;
			//}

			if (args.Player.IsAuthenticated())
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

				LMReturnCode response;
				try
				{
					response = await CTRS.CredentialHelper.Authenticate(args.Player.User, new Credentials(username, password));
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

				switch (response)
				{
					case LMReturnCode.Success:
						// The contributor should already exist in the cache, so no need to re-fetch
						Contributor contributor = CTRS.Contributors.Get(args.Player.User.ID);
						bool success = await CTRS.XenforoUsers.SetTShockID(contributor.XenforoID.Value, args.Player.User.ID);
						#region DEBUG
#if DEBUG
						TShock.Log.ConsoleInfo($"CTRS-AUTH: Set TShockID for Contributor {contributor.UserID.Value}? {success}");
#endif
						#endregion
						if (success)
						{
							args.Player.Authenticate();
							contributor.Initialize(args.Player.Index);
							args.Player.SendSuccessMessage($"{Tag} You are now authenticated for the forum account '{username}'.");
						}
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
					case LMReturnCode.UserNotAContributor:
						args.Player.SendMessage($"{Tag} Currently, only contributors are able to connect their forum accounts to their game accounts.", c);
						break;
					case LMReturnCode.AccountLimitReached:
						args.Player.SendErrorMessage($"Account limit reached. Contact an admin to revoke authentication on a previous account.");
						break;
				}
			}
		}

		public static async void CAdmin(CommandArgs args)
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
					Contributor target = await CTRS.Contributors.GetAsync(user.ID);
					if (target == null)
						args.Player.SendErrorMessage($"User '{user.Name}' is not a contributor.");
					else
					{
						target.Notifications |= Notifications.TierUpdate;
						await CTRS.Tiers.UpgradeTier(target);
						args.Player.SendSuccessMessage($"Forced a tier upgrade on contributor '{user.Name}'.");
					}
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

			Contributor con;
			if (!args.Player.IsAuthenticated() || (con = await CTRS.Contributors.GetAsync(args.Player.User.ID)) == null)
			{
				args.Player.SendInfoMessage($"{Tag} You must be a contributor to use this command. Find out how to contribute to the server here: "
					+ CTRS.Config.GetContributeURL());
				args.Player.SendInfoMessage($"{Tag} If you've already sent a contribution, use the /auth command to get started.");
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
			else if (args.Parameters[0] == "-C" || args.Parameters[0].Equals("cmds", StringComparison.OrdinalIgnoreCase) || args.Parameters[0].Equals("commands", StringComparison.OrdinalIgnoreCase))
			{
				args.Player.SendInfoMessage($"{Tag} Unlocked commands:");
				args.Player.SendInfoMessage(String.Format("{0} -c Color     [{1}]", Tag, (con.Settings & Settings.CanChangeColor) == Settings.CanChangeColor ? "x" : " "));
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
				if (CTRS.Config.RestrictCommands && (con.Settings & Settings.CanChangeColor) != Settings.CanChangeColor)
				{
					args.Player.SendWarningMessage($"{Tag} You don't have permission to set chat colors!");
					if (!String.IsNullOrWhiteSpace(CTRS.Config.Texts.RestrictedColorTip))
						args.Player.SendInfoMessage($"{Tag} {CTRS.Config.Texts.RestrictedColorTip}");
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

		[Route("/ctrs/transaction")]
		[Permission(Permissions.RestTransaction)]
		[Noun("user", true, "The user account connected to the contributor's forum account.", typeof(String))]
		[Noun("type", false, "The search criteria type (name for name lookup, id for id lookup).", typeof(String))]
		[Noun("credits", true, "The amount of credits to transfer.", typeof(Int32))]
		[Noun("date", false, "The date on which the original transaction was performed, as a Int64 unix timestamp.", typeof(Int64))]
		[Token]
		public static object RestNewTransaction(RestRequestArgs args)
		{
			var ret = UserFind(args.Parameters);
			if (ret is RestObject)
				return ret;

			User user = (User)ret;
			if (String.IsNullOrWhiteSpace(args.Parameters["credits"]))
				return RestMissingParam("credits");

			float credits;
			if (!Single.TryParse(args.Parameters["credits"], out credits))
				return RestInvalidParam("credits");

			long dateUnix = 0;
			if (!String.IsNullOrWhiteSpace(args.Parameters["date"]))
				Int64.TryParse(args.Parameters["date"], out dateUnix);

			Contributor con = Task.Run(() => CTRS.Contributors.GetAsync(user.ID)).Result;
			bool success = false;
			if (con == null)
			{
				// Transactions must never be ignored. If the contributor doesn't exist, create it
				con = new Contributor(user);
				con.LastAmount = credits;
				if (dateUnix > 0)
					con.LastDonation = dateUnix.FromUnixTime();
				con.Tier = 1;
				con.TotalCredits = credits;
				success = CTRS.Contributors.Add(con);
				if (success)
					con.InitializeAll();
				else
					TShock.Log.ConsoleInfo($"CTRS-WARNING: Failed to register contribution made by user '{user.Name}'!");
			}
			else
			{
				ContributorUpdates updates = 0;

				con.LastAmount = credits;
				updates |= ContributorUpdates.LastAmount;

				if (dateUnix > 0)
				{
					con.LastDonation = dateUnix.FromUnixTime();
					updates |= ContributorUpdates.LastDonation;
				}

				con.TotalCredits += credits;
				updates |= ContributorUpdates.TotalCredits;

				con.Notifications |= Notifications.NewDonation;
				// Always prompt a tier update check here
				con.Notifications |= Notifications.TierUpdate;
				updates |= ContributorUpdates.Notifications;

				success = CTRS.Contributors.Update(con, updates);
			}
			if (!success)
				return RestError("Transaction was not registered properly.");
			else
				return RestResponse("Transaction successful.");
		}

		/// <summary>
		/// This is the REST Request route used by Xenforo's AD Credit payment processor.
		/// </summary>
		[Route("/ctrs/v2/transaction/{user_id}")]
		[Permission(Permissions.RestTransaction)]
		[Verb("user_id", "The database ID of the Xenforo user account that made the purchase.", typeof(Int32))]
		[Noun("credits", true, "The amount of credits to transfer.", typeof(Int32))]
		[Noun("date", true, "The date on which the original transaction was performed, as a Int64 unix timestamp.", typeof(Int64))]
		[Token]
		public static object RestNewTransactionV2(RestRequestArgs args)
		{
			int userID;

			if (!Int32.TryParse(args.Verbs["user_id"], out userID))
				return RestInvalidParam("user_id");

			if (String.IsNullOrWhiteSpace(args.Parameters["credits"]))
				return RestMissingParam("credits");

			float credits;
			if (!Single.TryParse(args.Parameters["credits"], out credits))
				return RestInvalidParam("credits");

			long dateUnix = 0;
			if (!String.IsNullOrWhiteSpace(args.Parameters["date"]))
				Int64.TryParse(args.Parameters["date"], out dateUnix);

			Contributor con = Task.Run(() => CTRS.Contributors.GetByXenforoIDAsync(userID)).Result;
			bool success = false;

			if (con == null)
			{
				// Transactions must never be ignored. If the contributor doesn't exist, create it
				con = new Contributor(0);
				con.XenforoID = userID;
				con.LastAmount = credits;
				if (dateUnix > 0)
					con.LastDonation = dateUnix.FromUnixTime();
				con.Tier = 1;
				con.TotalCredits = credits;
				success = Task.Run(() => CTRS.Contributors.AddAsync(con)).Result;
				if (!success)
				{
					TShock.Log.ConsoleInfo($"CTRS-WARNING: Failed to register contribution made by forum user ID [{userID}]!");
				}
			}
			else
			{
				ContributorUpdates updates = 0;

				con.LastAmount = credits;
				updates |= ContributorUpdates.LastAmount;

				if (dateUnix > 0)
				{
					con.LastDonation = dateUnix.FromUnixTime();
					updates |= ContributorUpdates.LastDonation;
				}

				con.TotalCredits += credits;
				updates |= ContributorUpdates.TotalCredits;

				con.Notifications |= Notifications.NewDonation;
				// Always prompt a tier update check here
				con.Notifications |= Notifications.TierUpdate;
				updates |= ContributorUpdates.Notifications;

				success = CTRS.Contributors.Update(con, updates);
			}
			if (!success)
				return RestError("Transaction was not registered properly.");
			else
				return RestResponse("Transaction successful.");
		}

		[Route("/ctrs/update")]
		[Permission(Permissions.RestTransaction)]
		[Token]
		public static object RestUpdateContributors(RestRequestArgs args)
		{
			try
			{
				// Fetch contributor data from the database (sadly we can't do this async)
				Task.Run(() => CTRS.Contributors.LoadCache());
				return RestResponse("Update sent.");
			}
			catch (Exception ex)
			{
				return RestError("Update failed: " + ex.Message);
			}
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

#endregion
	}
}
