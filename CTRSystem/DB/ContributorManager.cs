using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using TShockAPI;

namespace CTRSystem.DB
{
	public class ContributorManager : DbManager
	{
		private object syncLock = new object();

		public ContributorManager(CTRS main) : base(main)
		{
			Task.Run(CreateTablesAsync);

			#region old code
			//var creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite
			//	? (IQueryBuilder)new SqliteQueryCreator()
			//	: new MysqlQueryCreator());

			//if (creator.EnsureTableStructure(new SqlTable(_main.Config.ContributorTableName,
			//	new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
			//	new SqlColumn("XenforoID", MySqlDbType.Int32) { Unique = true, DefaultValue = null },
			//	new SqlColumn("TotalCredits", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
			//	new SqlColumn("LastDonation", MySqlDbType.Int64) { DefaultValue = null },
			//	new SqlColumn("LastAmount", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
			//	new SqlColumn("Tier", MySqlDbType.Int32) { NotNull = true, DefaultValue = "1" },
			//	new SqlColumn("ChatColor", MySqlDbType.VarChar) { Length = 11, DefaultValue = null },
			//	new SqlColumn("Notifications", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" },
			//	new SqlColumn("Settings", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" })))
			//{
			//	TShock.Log.ConsoleInfo($"CTRS: created table '{_main.Config.ContributorTableName}'");
			//}

			//if (creator.EnsureTableStructure(new SqlTable(_main.Config.ContributorAccountsTableName,
			//	new SqlColumn("UserID", MySqlDbType.Int32) { Primary = true },
			//	new SqlColumn("ContributorID", MySqlDbType.Int32) { NotNull = true })))
			//{
			//	// This needs to be included in the table creation query
			//	db.Query($@"ALTER TABLE {_main.Config.ContributorAccountsTableName}
			//				ADD FOREIGN KEY (ContributorID)
			//				REFERENCES {_main.Config.ContributorTableName} (ID);");
			//	TShock.Log.ConsoleInfo($"CTRS: created table '{_main.Config.ContributorAccountsTableName}'");
			//}
			#endregion
		}

		/// <summary>
		/// Creates all necessary tables if they don't exist.
		/// </summary>
		/// <returns>A task to perform this action.</returns>
		public Task CreateTablesAsync()
		{
			return Task.Run(() =>
			{
				#region SQL(contributors)
				string contributors = PrepareSql($@"
				CREATE TABLE IF NOT EXISTS `{Main.Config.ContributorTableName}` (
					`ID` INT(11) NOT NULL AUTO_INCREMENT,
					`XenforoID` INT(11) NULL DEFAULT NULL,
					`TotalCredits` FLOAT NOT NULL DEFAULT '0',
					`LastDonation` BIGINT(20) NULL DEFAULT NULL,
					`LastAmount` FLOAT NOT NULL DEFAULT '0',
					`Tier` INT(11) NOT NULL DEFAULT '1',
					`ChatColor` VARCHAR(11) NULL DEFAULT NULL,
					`Notifications` INT(11) NOT NULL DEFAULT '0',
					`Settings` INT(11) NOT NULL DEFAULT '0',
					PRIMARY KEY (`ID`),
					UNIQUE INDEX `XenforoID` (`XenforoID`),
					INDEX `Tier_fk` (`Tier`),
					CONSTRAINT `Tier_fk` FOREIGN KEY (`Tier`) REFERENCES `{Main.Config.TierTableName}` (`ID`) ON UPDATE CASCADE ON DELETE CASCADE
				)
				COLLATE='utf8_general_ci'
				ENGINE=InnoDB");
				#endregion

				#region SQL(contributors_a)
				string contributors_a = PrepareSql($@"
				CREATE TABLE IF NOT EXISTS `{Main.Config.ContributorAccountsTableName}` (
					`UserID` INT(11) NOT NULL,
					`ContributorID` INT(11) NOT NULL,
					PRIMARY KEY (`UserID`),
					INDEX `ContributorID_fk` (`ContributorID`),
					CONSTRAINT `ContributorID_fk` FOREIGN KEY (`ContributorID`) REFERENCES `{Main.Config.ContributorTableName}` (`ID`) ON UPDATE CASCADE ON DELETE CASCADE
				)
				COLLATE='utf8_general_ci'
				ENGINE=InnoDB");
				#endregion

				using (var db = OpenConnection())
				{
					try
					{
						// Create Table {Contributors}
						db.Execute(contributors);

						// Create Table {Contributors_Account}
						db.Execute(contributors_a);
					}
					catch (Exception ex)
					{
						if (Main.Config.LogDatabaseErrors)
						{
							TShock.Log.ConsoleError(ex.ToString());
						}
					}
				}
			});
		}

		[Obsolete("Only kept for the legacy REST transaction route.")]
		public bool AddLocal(Contributor contributor)
		{
			#region SQL(query)

			string query = PrepareSql($@"
			INSERT INTO {Main.Config.ContributorTableName} (
				TotalCredits,
				LastAmount,
				Notifications,
				Settings
			)
			VALUES (
				@TotalCredits,
				@LastAmount,
				@Notifications,
				@Settings
			)");

			if (contributor.LastDonation != DateTime.MinValue)
			{
				query = PrepareSql($@"
				INSERT INTO {Main.Config.ContributorTableName} (
					TotalCredits,
					LastDonation,
					LastAmount,
					Notifications,
					Settings
				)
				VALUES (
					@TotalCredits,
					@LastDonation,
					@LastAmount,
					@Notifications,
					@Settings
				)");
			}

			#endregion

			#region SQL(query_a)
			string query_a = PrepareSql($@"
			INSERT INTO {Main.Config.ContributorAccountsTableName} (
				UserID,
				ContributorID
			)
			VALUES (
				@UserID,
				@ContributorID
			)");
			#endregion

			lock (syncLock)
			{
				try
				{
					using (var db = OpenConnection())
					{
						if (db.Execute(query, contributor.ToDataModel()) == 1)
						{
							contributor.Id = GetLastInsertId();

							for (int i = 0; i < contributor.Accounts.Count; i++)
							{
								if (db.Execute(query_a, new { UserID = contributor.Accounts[i], ContributorID = contributor.Id }) == 0)
								{
									return false;
								}
							}
							return true;
						}
						return false;
					}
				}
				catch (Exception ex)
				{
					if (Main.Config.LogDatabaseErrors)
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
			#region SQL(query)

			string query = PrepareSql($@"
			INSERT INTO {Main.Config.ContributorTableName} (
				XenforoID,
				TotalCredits,
				LastAmount,
				Notifications,
				Settings
			)
			VALUES (
				@XenforoID,
				@TotalCredits,
				@LastAmount,
				@Notifications,
				@Settings
			)");

			if (contributor.LastDonation != DateTime.MinValue)
			{
				query = PrepareSql($@"
				INSERT INTO {Main.Config.ContributorTableName} (
					XenforoID,
					TotalCredits,
					LastDonation,
					LastAmount,
					Notifications,
					Settings
				)
				VALUES (
					@XenforoID,
					@TotalCredits,
					@LastDonation,
					@LastAmount,
					@Notifications,
					@Settings
				)");
			}

			#endregion

			#region SQL(query_a)
			string query_a = PrepareSql($@"
			INSERT INTO {Main.Config.ContributorAccountsTableName} (
				UserID,
				ContributorID
			)
			VALUES (
				@UserID,
				@ContributorID
			)");
			#endregion

			try
			{
				lock (syncLock)
				{
					using (var db = OpenConnection())
					{
						if (db.Execute(query, contributor.ToDataModel()) == 1)
						{
							contributor.Id = GetLastInsertId();

							for (int i = 0; i < contributor.Accounts.Count; i++)
							{
								if (db.Execute(query_a, new { UserID = contributor.Accounts[i], ContributorID = contributor.Id }) == 0)
								{
									return false;
								}
							}
							return true;
						}
						return false;
					}
				}
			}
			catch (Exception ex)
			{
				if (Main.Config.LogDatabaseErrors)
				{
					TShock.Log.ConsoleError($"CTRS-DB: Unable to add contributor with xenforoID:{contributor.XenforoId.Value}\nMessage: " + ex.Message);
					TShock.Log.Error(ex.ToString());
				}
				return false;
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
			#region SQL(query_a)
			string query_a = PrepareSql($@"
			INSERT INTO {Main.Config.ContributorAccountsTableName} (
				UserID,
				ContributorID
			)
			VALUES (
				@UserID,
				@ContributorID
			)");
			#endregion

			try
			{
				lock (syncLock)
				{
					using (var db = OpenConnection())
					{
						return db.Execute(query_a, new { UserID = userID, ContributorID = contributorID }) == 1;
					}
				}
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
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor Get(int userID)
		{
			#region SQL(select_id)
			string select_id = PrepareSql($@"
			SELECT ContributorID
			FROM {Main.Config.ContributorAccountsTableName}
			WHERE UserID = @UserID");
			#endregion

			#region SQL(select_con)
			string select_con = PrepareSql($@"
			SELECT *
			FROM {Main.Config.ContributorTableName}
			WHERE ID = @Id");
			#endregion

			#region SQL(select_a)
			string select_a = PrepareSql($@"
			SELECT UserID
			FROM {Main.Config.ContributorAccountsTableName}
			WHERE ContributorID = @ID");
			#endregion

			int contributorID;

			using (var db = OpenConnection())
			{
				contributorID = db.Query<int>(select_id, new { UserID = userID }).SingleOrDefault();

				if (contributorID == 0)
					return null;

				Contributor con = (Contributor)db.Query<Contributor.DataModel>(select_con, new { Id = contributorID }).SingleOrDefault();

				// The foreign key constraints would have to fail for this to happen, but better safe than sorry
				if (con == null)
					return null;

				con.Accounts = db.Query<int>(select_a, con.ToDataModel()).ToList();
				return con;
			}
		}

		/// <summary>
		/// Asynchronously fetches a contributor object from the database.
		/// </summary>
		/// <param name="userID">The user ID of an authenticated account.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetAsync(int userID)
		{
			return Task.Run(() => Get(userID));
		}

		/// <summary>
		/// Fetches a contributor object from the database.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor GetByXenforoId(int xenforoID)
		{
			#region SQL(select_con)
			string select_con = PrepareSql($@"
			SELECT *
			FROM {Main.Config.ContributorTableName}
			WHERE XenforoID = @XenforoID");
			#endregion

			#region SQL(select_a)
			string select_a = $@"
			SELECT UserID
			FROM {Main.Config.ContributorAccountsTableName}
			WHERE ContributorID = @ID";
			#endregion

			using (var db = OpenConnection())
			{
				Contributor con = (Contributor)db.Query<Contributor.DataModel>(select_con, new { XenforoID = xenforoID }).SingleOrDefault();
				
				if (con == null)
					return null;

				con.Accounts = db.Query<int>(select_a, con.ToDataModel()).ToList();
				return con;
			}
		}

		/// <summary>
		/// Asynchronously fetches a contributor object from the database.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetByXenforoIdAsync(int xenforoID)
		{
			return Task.Run(() => GetByXenforoId(xenforoID));
		}

		/// <summary>
		/// Sends updated contributor data to the database.
		/// Logs any exception thrown.
		/// </summary>
		/// <param name="contributor">The contributor to update with the already-updated values set.</param>
		/// <param name="updates">The list of values to update.</param>
		/// <returns>True if it updates one row, false if anything else..</returns>
		public bool Update(Contributor contributor, ContributorUpdates updates)
		{
			if (updates == 0)
				return true;

			List<string> updatesList = new List<string>();
			if ((updates & ContributorUpdates.XenforoID) == ContributorUpdates.XenforoID)
				updatesList.Add("XenforoID = @XenforoID");
			if ((updates & ContributorUpdates.TotalCredits) == ContributorUpdates.TotalCredits)
				updatesList.Add("TotalCredits = @TotalCredits");
			if ((updates & ContributorUpdates.LastDonation) == ContributorUpdates.LastDonation)
				updatesList.Add("LastDonation = @LastDonation");
			if ((updates & ContributorUpdates.LastAmount) == ContributorUpdates.LastAmount)
				updatesList.Add("LastAmount = @LastAmount");
			if ((updates & ContributorUpdates.Tier) == ContributorUpdates.Tier)
				updatesList.Add("Tier = @Tier");
			if ((updates & ContributorUpdates.ChatColor) == ContributorUpdates.ChatColor)
				updatesList.Add("ChatColor = @ChatColor");
			if ((updates & ContributorUpdates.Notifications) == ContributorUpdates.Notifications)
				updatesList.Add("Notifications = @Notifications");
			if ((updates & ContributorUpdates.Settings) == ContributorUpdates.Settings)
				updatesList.Add("Settings = @Settings");

			#region SQL(update)
			string update = PrepareSql($@"
			UPDATE {Main.Config.ContributorTableName}
			SET {String.Join(", ", updatesList)}
			WHERE ID = @ID");
			#endregion

			try
			{
				lock (syncLock)
				{
					using (var db = OpenConnection())
					{
						return db.Execute(update, contributor.ToDataModel()) == 1;
					}
				}
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError("CTRS: An error occurred while updating a contributor's (ACCOUNT: "
					+ $"{contributor.Accounts.ElementAtOrDefault(0)}) info\nMessage: {ex.Message}"
					+ "\nCheck logs for more details");
				TShock.Log.Error(ex.ToString());
				return false;
			}
		}

		/// <summary>
		/// Asynchronously sends updated contributor data to the database.
		/// Logs any exception thrown.
		/// </summary>
		/// <param name="contributor">The contributor to update with the already-updated values set.</param>
		/// <param name="updates">The list of values to update.</param>
		/// <returns>True if it updates one row, false if anything else.</returns>
		public async Task<bool> UpdateAsync(Contributor contributor, ContributorUpdates updates)
		{
			return await Task.Run(() => Update(contributor, updates));
		}

		#region Exceptions (Not in use)

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

		#endregion
	}
}