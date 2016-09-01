using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using CTRSystem.Extensions;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;
using System.Linq;

namespace CTRSystem.DB
{
	public class ContributorManager
	{
		private IDbConnection db;
		private object syncLock = new object();

		/// <summary>
		/// Occurs after a contributor has been updated.
		/// Contributor objects should hook to this event to properly follow changes.
		/// </summary>
		public event EventHandler<ContributorUpdateEventArgs> ContributorUpdate;

		/// <summary>
		/// Occurs after a contributor receives a new transaction.
		/// </summary>
		public event EventHandler<TransactionEventArgs> Transaction;

		public void OnTransaction(object sender, TransactionEventArgs e)
		{
			Transaction?.Invoke(sender, e);
		}

		public ContributorManager(IDbConnection db)
		{
			this.db = db;

			var creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite
				? (IQueryBuilder)new SqliteQueryCreator()
				: new MysqlQueryCreator());

			if (creator.EnsureTableStructure(new SqlTable(CTRS.Config.ContributorTableName,
				new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
				new SqlColumn("XenforoID", MySqlDbType.Int32) { Unique = true, DefaultValue = null },
				new SqlColumn("TotalCredits", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("LastDonation", MySqlDbType.Int64) { DefaultValue = null },
				new SqlColumn("LastAmount", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("Tier", MySqlDbType.Int32) { NotNull = true, DefaultValue = "1" },
				new SqlColumn("ChatColor", MySqlDbType.VarChar) { Length = 11, DefaultValue = null },
				new SqlColumn("Notifications", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("Settings", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" })))
			{
				TShock.Log.ConsoleInfo($"CTRS: created table '{CTRS.Config.ContributorTableName}'");
			}

			if (creator.EnsureTableStructure(new SqlTable(CTRS.Config.ContributorAccountsTableName,
				new SqlColumn("UserID", MySqlDbType.Int32) { Primary = true },
				new SqlColumn("ContributorID", MySqlDbType.Int32) { NotNull = true })))
			{
				// This needs to be included in the table creation query
				db.Query($@"ALTER TABLE {CTRS.Config.ContributorAccountsTableName}
							ADD FOREIGN KEY (ContributorID)
							REFERENCES {CTRS.Config.ContributorTableName} (ID);");
				TShock.Log.ConsoleInfo($"CTRS: created table '{CTRS.Config.ContributorAccountsTableName}'");
			}
		}

		[Obsolete("Only kept for the legacy REST transaction route.")]
		public bool AddLocal(Contributor contributor)
		{
			string query = $"INSERT INTO {CTRS.Config.ContributorTableName} (TotalCredits, LastAmount, Tier, Notifications, Settings) "
						 + "VALUES (@0, @2, @3, @4, @5);";
			if (contributor.LastDonation != DateTime.MinValue)
				query = $"INSERT INTO {CTRS.Config.ContributorTableName} (TotalCredits, LastDonation, LastAmount, Tier, Notifications, Settings) "
					  + "VALUES (@0, @1, @2, @3, @4, @5);";
			string query2 = $"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);";

			lock (syncLock)
			{
				try
				{
					if (db.Query(query,
						contributor.TotalCredits,
						contributor.LastDonation.ToUnixTime(),
						contributor.LastAmount,
						contributor.Tier,
						contributor.Notifications,
						contributor.Settings) == 1)
					{
						using (var result = db.QueryReader("SELECT LAST_INSERT_ID() AS ContributorID;"))
						{
							if (result.Read())
							{
								contributor.Id = result.Get<int>("ContributorID");
							}
							else
							{
								return false;
							}
						}

						for (int i = 0; i < contributor.Accounts.Count; i++)
						{
							if (db.Query($"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);",
									contributor.Accounts[i], contributor.Id) != 1)
							{
								return false;
							}
						}
						return true;
					}
					return false;
				}
				catch (Exception ex)
				{
					if (CTRS.Config.LogDatabaseErrors)
					{
						TShock.Log.ConsoleError($"CTRS-DB: Unable to add contributor UserID:{contributor.Accounts[0]}\nMessage: " + ex.Message);
						TShock.Log.Error(ex.ToString());
					}
					return false;
				}
			}
		}

		/// <summary>
		/// Inserts data for a contributor object into the database.
		/// The contributor object must have a Xenforo Id associated with it.
		/// </summary>
		/// <param name="contributor">The contributor object to save.</param>
		/// <returns>A <see cref="bool"/> representing whether the operation was successful or not.</returns>
		public bool Add(Contributor contributor)
		{
			string query = $@"INSERT INTO {CTRS.Config.ContributorTableName} (
					XenforoID, TotalCredits, LastAmount, Tier, Notifications, Settings)
					VALUES (@0, @1, @3, @4, @5, @6);";

			if (contributor.LastDonation != DateTime.MinValue)
			{
				query = $@"INSERT INTO {CTRS.Config.ContributorTableName} (
						XenforoID, TotalCredits, LastDonation, LastAmount, Tier, Notifications, Settings)
						VALUES (@0, @1, @2, @3, @4, @5, @6);";
			}

			string query2 = $@"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID)
								   VALUES (@0, @1);";

			lock (syncLock)
			{
				try
				{
					if (db.Query(query,
						contributor.XenforoId.Value,
						contributor.TotalCredits,
						contributor.LastDonation.ToUnixTime(),
						contributor.LastAmount,
						contributor.Tier,
						contributor.Notifications,
						contributor.Settings) == 1)
					{
						using (var result = db.QueryReader("SELECT LAST_INSERT_ID() AS ContributorID;"))
						{
							if (result.Read())
							{
								contributor.Id = result.Get<int>("ContributorID");
							}
							else
							{
								return false;
							}
						}

						for (int i = 0; i < contributor.Accounts.Count; i++)
						{
							if (db.Query($"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);",
									contributor.Accounts[i], contributor.Id) != 1)
							{
								return false;
							}
						}
						return true;
					}
					return false;
				}
				catch (Exception ex)
				{
					if (CTRS.Config.LogDatabaseErrors)
					{
						TShock.Log.ConsoleError($"CTRS-DB: Unable to add contributor with xenforoID:{contributor.XenforoId.Value}\nMessage: " + ex.Message);
						TShock.Log.Error(ex.ToString());
					}
					return false;
				}
			}
		}

		/// <summary>
		/// Inserts data for a contributor object into the database asynchronously.
		/// The contributor object must have a Xenforo Id associated with it.
		/// </summary>
		/// <param name="contributor">The contributor object to save.</param>
		/// <returns>
		/// A task with a <see cref="bool"/> representing whether the operation was successful or not.
		/// </returns>
		public Task<bool> AddAsync(Contributor contributor)
		{
			return Task.Run(() => Add(contributor));
		}

		/// <summary>
		/// Binds a <see cref="User"/> account to a contributor object in the database.
		/// </summary>
		/// <param name="contributorID">The row ID of the contributor object in the database.</param>
		/// <param name="userID">The user account ID.</param>
		/// <returns>A <see cref="bool"/> representing whether the operation was successful or not.</returns>
		public bool AddAccount(int contributorID, int userID)
		{
			string query = $"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);";
			try
			{
				return db.Query(query, userID, contributorID) == 1;
			}
			catch
			{
				// Most likely outcome if the account is already added
				return false;
			}
		}

		/// <summary>
		/// Binds a <see cref="User"/> account to a contributor object in the database asynchronously.
		/// </summary>
		/// <param name="contributorID">The row ID of the contributor object in the database.</param>
		/// <param name="userID">The user account ID.</param>
		/// <returns>
		/// A task with a <see cref="bool"/> representing whether the operation was successful or not.
		/// </returns>
		public Task<bool> AddAccountAsync(int contributorID, int userID)
		{
			return Task.Run(() => AddAccount(contributorID, userID));
		}

		/// <summary>
		/// Fetches a contributor object from the database.
		/// </summary>
		/// <param name="userID">The user ID of an authenticated account.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor Get(int userID, bool throwExceptions = false)
		{
			string query = $"SELECT ContributorID FROM {CTRS.Config.ContributorAccountsTableName} WHERE UserID = @0;";
			string query2 = $"SELECT * FROM {CTRS.Config.ContributorTableName} WHERE ID = @0;";
			int contributorID;

			using (var result = db.QueryReader(query, userID))
			{
				if (result.Read())
					contributorID = result.Get<int>("ContributorID");
				else if (throwExceptions)
					throw new ContributorNotFoundException(userID);
				else
					return null;
			}

			using (var result = db.QueryReader(query2, contributorID))
			{
				if (result.Read())
				{
					Contributor contributor = new Contributor(contributorID);
					contributor.Accounts.Add(userID);
					contributor.XenforoId = result.Get<int?>("XenforoID");
					contributor.TotalCredits = result.Get<float>("TotalCredits");
					contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
					contributor.LastAmount = result.Get<float>("LastAmount");
					contributor.Tier = result.Get<int>("Tier");
					contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
					contributor.Notifications = (Notifications)result.Get<int>("Notifications");
					contributor.Settings = (Settings)result.Get<int>("Settings");

					return contributor;
				}
			}

			if (throwExceptions)
				throw new ContributorNotFoundException(userID);
			else
				return null;
		}

		/// <summary>
		/// Asynchronously fetches a contributor object from the database.
		/// </summary>
		/// <param name="userID">The user ID of an authenticated account.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetAsync(int userID, bool throwExceptions = false)
		{
			return Task.Run(() => Get(userID, throwExceptions));
		}

		/// <summary>
		/// Fetches a contributor object from the database.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor GetByXenforoId(int xenforoID, bool throwExceptions = false)
		{
			string query = $"SELECT * FROM {CTRS.Config.ContributorTableName} WHERE XenforoID = @0;";
			string query2 = $"SELECT UserID FROM {CTRS.Config.ContributorAccountsTableName} WHERE ContributorID = @0;";
			using (var result = db.QueryReader(query, xenforoID))
			{
				if (result.Read())
				{
					Contributor contributor = new Contributor(result.Get<int>("ID"));
					using (var result2 = db.QueryReader(query, contributor.Id))
					{
						while (result2.Read())
						{
							contributor.Accounts.Add(result2.Get<int>("UserID"));
						}
						contributor.XenforoId = result.Get<int?>("XenforoID");
						contributor.TotalCredits = result.Get<float>("TotalCredits");
						contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
						contributor.LastAmount = result.Get<float>("LastAmount");
						contributor.Tier = result.Get<int>("Tier");
						contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
						contributor.Notifications = (Notifications)result.Get<int>("Notifications");
						contributor.Settings = (Settings)result.Get<int>("Settings");

						return contributor;
					}
				}
			}

			if (throwExceptions)
				throw new ContributorNotFoundException(xenforoID);
			else
				return null;
		}

		/// <summary>
		/// Asynchronously fetches a contributor object from the database.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetByXenforoIdAsync(int xenforoID, bool throwExceptions = false)
		{
			return Task.Run(() => GetByXenforoId(xenforoID, throwExceptions));
		}

		/// <summary>
		/// Runs a task to update a contributor's data.
		/// Fires the <see cref="ContributorUpdate"/> event.
		/// Logs any exception thrown.
		/// </summary>
		/// <param name="contributor">The contributor to update with the already-updated values set.</param>
		/// <param name="updates">The list of values to update.</param>
		/// /// <param name="local">
		/// If set to true, will assume the contributor update is done manually and suppresses
		/// the firing of the contributor update global event. This can be helpful when a user
		/// authenticated to this contributor is running a command.
		/// </param>
		/// <returns>True if it goes smooth, false if exceptions are thrown.</returns>
		public bool Update(Contributor contributor, ContributorUpdates updates, bool local = false)
		{
			if (updates == 0)
				return true;

			List<string> updatesList = new List<string>();
			if ((updates & ContributorUpdates.XenforoID) == ContributorUpdates.XenforoID)
				updatesList.Add("XenforoID = @1");
			if ((updates & ContributorUpdates.TotalCredits) == ContributorUpdates.TotalCredits)
				updatesList.Add("TotalCredits = @2");
			if ((updates & ContributorUpdates.LastDonation) == ContributorUpdates.LastDonation)
				updatesList.Add("LastDonation = @3");
			if ((updates & ContributorUpdates.LastAmount) == ContributorUpdates.LastAmount)
				updatesList.Add("LastAmount = @4");
			if ((updates & ContributorUpdates.Tier) == ContributorUpdates.Tier)
				updatesList.Add("Tier = @5");
			if ((updates & ContributorUpdates.ChatColor) == ContributorUpdates.ChatColor)
				updatesList.Add("ChatColor = @6");
			if ((updates & ContributorUpdates.Notifications) == ContributorUpdates.Notifications)
				updatesList.Add("Notifications = @7");
			if ((updates & ContributorUpdates.Settings) == ContributorUpdates.Settings)
				updatesList.Add("Settings = @8");

			string query = $@"UPDATE {CTRS.Config.ContributorTableName}
							  SET {String.Join(", ", updatesList)}
							  WHERE ID = @0;";

			try
			{
				lock (syncLock)
				{
					if (db.Query(query, contributor.Id,
						contributor.XenforoId,
						contributor.TotalCredits,
						contributor.LastDonation.ToUnixTime(),
						contributor.LastAmount,
						contributor.Tier,
						Tools.ColorToRGB(contributor.ChatColor),
						(int)contributor.Notifications,
						(int)contributor.Settings) == 1)
					{
						if (!local)
							ContributorUpdate?.Invoke(this, new ContributorUpdateEventArgs(contributor, updates));
						return true;
					}
					else
					{
						return false;
					}
				}
			}
			catch (Exception e)
			{
				TShock.Log.ConsoleError("CTRS: An error occurred while updating a contributor's (ACCOUNT: "
					+ $"{contributor.Accounts.ElementAtOrDefault(0)}) info\nMessage: {e.Message}"
					+ "\nCheck logs for more details");
				TShock.Log.Error(e.ToString());
				return false;
			}
		}

		/// <summary>
		/// Asynchronously updates a contributor's data.
		/// Fires the <see cref="ContributorUpdate"/> event.
		/// Logs any exception thrown.
		/// </summary>
		/// <param name="contributor">The contributor to update with the already-updated values set.</param>
		/// <param name="updates">The list of values to update.</param>
		/// <param name="local">
		/// If set to true, will assume the contributor update is done manually and suppresses
		/// the firing of the contributor update global event. This can be helpful when a user
		/// authenticated to this contributor is running a command.
		/// </param>
		/// <returns>True if it updates one row, false if anything else.</returns>
		public async Task<bool> UpdateAsync(Contributor contributor, ContributorUpdates updates, bool local = false)
		{
			return await Task.Run(() => Update(contributor, updates, local));
		}

		public class ContributorManagerException : Exception
		{
			public ContributorManagerException(string message) : base(message)
			{

			}

			public ContributorManagerException(string message, Exception inner) : base(message, inner)
			{

			}
		}

		public class ContributorNotFoundException : ContributorManagerException
		{
			public ContributorNotFoundException(int id) : base($"Contributor with the ID {id} does not exist")
			{

			}
		}
	}
}