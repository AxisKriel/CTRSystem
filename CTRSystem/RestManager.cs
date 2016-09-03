using System;
using CTRSystem.DB;
using CTRSystem.Extensions;
using HttpServer;
using Rests;
using TShockAPI;
using TShockAPI.DB;

namespace CTRSystem
{
	public class RestManager
	{
		private CTRS _main;

		/// <summary>
		/// Occurs after a contributor has been updated.
		/// Contributor objects should hook to this event to properly follow changes.
		/// </summary>
		public event EventHandler<ContributorUpdateEventArgs> ContributorUpdate;

		/// <summary>
		/// Occurs after a contributor receives a new transaction.
		/// </summary>
		public event EventHandler<TransactionEventArgs> Transaction;

		/// <summary>
		/// Initializes a new instance of the <see cref="RestManager"/> class.
		/// </summary>
		/// <param name="main">The parent CTRSystem instance.</param>
		public RestManager(CTRS main)
		{
			_main = main;

			#region Register Commands

			TShock.RestApi.Register(new SecureRestCommand("/ctrs/transaction", restNewTransaction, Permissions.RestTransaction));
			TShock.RestApi.Register(new SecureRestCommand("/ctrs/update", restUpdateContributors, Permissions.RestTransaction));

			TShock.RestApi.Register(new SecureRestCommand("/ctrs/v2/transaction/{user_id}", restNewTransactionV2, Permissions.RestTransaction));
			TShock.RestApi.Register(new SecureRestCommand("/ctrs/v2/update/{user_id}", restUpdateContributorV2, Permissions.RestTransaction));

			#endregion
		}

		[Route("/ctrs/transaction")]
		[Permission(Permissions.RestTransaction)]
		[Noun("user", true, "The user account connected to the contributor's forum account.", typeof(String))]
		[Noun("type", false, "The search criteria type (name for name lookup, id for id lookup).", typeof(String))]
		[Noun("credits", true, "The amount of credits to transfer.", typeof(Int32))]
		[Noun("date", false, "The date on which the original transaction was performed, as a Int64 unix timestamp.", typeof(Int64))]
		[Token]
		[Obsolete("Use RestNewTransactionV2 instead.")]
		object restNewTransaction(RestRequestArgs args)
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

			Contributor con = _main.Contributors.Get(user.ID);
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
				success = _main.Contributors.AddLocal(con);
				if (!success)
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

				success = _main.Contributors.Update(con, updates);
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
		object restNewTransactionV2(RestRequestArgs args)
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

			Contributor con = _main.Contributors.GetByXenforoId(userID);
			bool success = false;

			if (con == null)
			{
				// Transactions must never be ignored. If the contributor doesn't exist, create it
				con = new Contributor(0);
				con.XenforoId = userID;
				con.LastAmount = credits;
				if (dateUnix > 0)
					con.LastDonation = dateUnix.FromUnixTime();
				con.Tier = 1;
				con.TotalCredits = credits;

				success = _main.Contributors.Add(con);
				if (!success)
				{
					TShock.Log.ConsoleInfo($"CTRS-WARNING: Failed to register contribution made by forum user ID [{userID}]!");
				}

				// Fire the Transaction event (must be done after Add to include the contributor Id)
				Transaction?.Invoke(_main.Contributors, new TransactionEventArgs(con.Id, credits, dateUnix.FromUnixTime()));
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

				// Fire the Transaction event
				var transactionArgs = new TransactionEventArgs(con.Id, credits, dateUnix.FromUnixTime());
				Transaction?.Invoke(_main.Contributors, transactionArgs);

				// Suppress notifications if needed
				if (!transactionArgs.SuppressNotifications)
				{
					con.Notifications |= Notifications.NewDonation;
					con.Notifications |= Notifications.TierUpdate;
					updates |= ContributorUpdates.Notifications;
				}

				success = _main.Contributors.Update(con, updates);
			}
			if (!success)
				return RestError("Transaction was not registered properly.");
			else
				return RestResponse("Transaction successful.");
		}

		[Route("/ctrs/update")]
		[Permission(Permissions.RestTransaction)]
		[Token]
		[Obsolete]
		object restUpdateContributors(RestRequestArgs args)
		{
			// An UpdateContributorsV2 will eventually replace this with more precise updating instead of grab all
			return RestError("This endpoint has been deprecated.");
		}

		[Route("/ctrs/v2/update/{user_id}")]
		[Permission(Permissions.RestTransaction)]
		[Verb("user_id", "The database ID of the Xenforo user account.", typeof(Int32))]
		[Noun("updates", true, "A list of updates to be run.", typeof(ContributorUpdates))]
		[Token]
		object restUpdateContributorV2(RestRequestArgs args)
		{
			int xenforoId;

			if (!Int32.TryParse(args.Verbs["user_id"], out xenforoId))
				return RestInvalidParam("contributor_id");

			if (String.IsNullOrWhiteSpace(args.Parameters["updates"]))
				return RestMissingParam("updates");

			ContributorUpdates updates;

			if (!Enum.TryParse(args.Parameters["updates"], out updates))
				return RestInvalidParam("updates");

			Contributor con = _main.Contributors.GetByXenforoId(xenforoId);
			if (con != null)
			{
				// Fire the Contributor Update event and let any listener pick it up
				ContributorUpdate?.Invoke(this, new ContributorUpdateEventArgs(con, updates));
			}

			// Possible improvement: send different response if something was certainly updated?
			return RestResponse("Update sent.");
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
