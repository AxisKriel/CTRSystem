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
		private List<Contributor> _cache = new List<Contributor>();
		private object syncLock = new object();

		public List<Contributor> Cache
		{
			get { return _cache; }
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
				//new SqlColumn("Accounts", MySqlDbType.Text) { DefaultValue = "" },
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

			// Load all contributors to the cache
			//Task.Run(() => LoadCache());
		}

		/// <summary>
		/// Loads contributor data from the database to the memory cache.
		/// </summary>
		/// <returns>The task for this action.</returns>
		public async Task LoadCache()
		{
			List<Contributor> list = await GetAllAsync();
			lock (_cache)
			{
				_cache = list;
			}
		}

		public bool Add(Contributor contributor)
		{
			if (_cache.Exists(c => c.Accounts.Intersect(contributor.Accounts).Any()))
				return false;

			string query = $"INSERT INTO {CTRS.Config.ContributorTableName} (TotalCredits, LastAmount, Tier, Notifications, Settings) "
						 + "VALUES (@0, @2, @3, @4, @5);";
			if (contributor.LastDonation != DateTime.MinValue)
				query = $"INSERT INTO {CTRS.Config.ContributorTableName} (TotalCredits, LastDonation, LastAmount, Tier, Notifications, Settings) "
					  + "VALUES (@0, @1, @2, @3, @4, @5);";
			string query2 = $"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);";

			lock (_cache)
			{
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
									contributor.ID = result.Get<int>("ContributorID");
								}
								else
								{
									return false;
								}
							}

							_cache.Add(contributor);
							for (int i = 0; i < contributor.Accounts.Count; i++)
							{
								if (db.Query($"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);",
										contributor.Accounts[i], contributor.ID) != 1)
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
		}

		public async Task<bool> AddAsync(Contributor contributor)
		{
			if (_cache.Exists(c => c.Accounts.Intersect(contributor.Accounts).Any()))
				return false;

			return await Task.Run(() =>
			{
				string query = $"INSERT INTO {CTRS.Config.ContributorTableName} (XenforoID, TotalCredits, LastAmount, Tier, Notifications, Settings) "
						 + "VALUES (@0, @1, @3, @4, @5, @6);";
				if (contributor.LastDonation != DateTime.MinValue)
					query = $"INSERT INTO {CTRS.Config.ContributorTableName} (XenforoID, TotalCredits, LastDonation, LastAmount, Tier, Notifications, Settings) "
						  + "VALUES (@0, @1, @2, @3, @4, @5, @6);";
				string query2 = $"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);";

				lock (_cache)
				{
					lock (syncLock)
					{
						try
						{
							if (db.Query(query,
								contributor.XenforoID.Value,
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
										contributor.ID = result.Get<int>("ContributorID");
									}
									else
									{
										return false;
									}
								}

								_cache.Add(contributor);
								for (int i = 0; i < contributor.Accounts.Count; i++)
								{
									if (db.Query($"INSERT INTO {CTRS.Config.ContributorAccountsTableName} (UserID, ContributorID) VALUES (@0, @1);",
											contributor.Accounts[i], contributor.ID) != 1)
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
								TShock.Log.ConsoleError($"CTRS-DB: Unable to add contributor with xenforoID:{contributor.XenforoID.Value}\nMessage: " + ex.Message);
								TShock.Log.Error(ex.ToString());
							}
							return false;
						}
					}
				}
			});
		}

		public async Task<bool> AddAccountAsync(int contributorID, int userID)
		{
			return await Task.Run(() =>
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
			});
		}

		/// <summary>
		/// Gets a contributor from the cache.
		/// </summary>
		/// <param name="userID">The contributor ID, usually their user ID.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor Get(int userID)
		{
			return _cache.Find(c => c.Accounts.Contains(userID));
		}

		/// <summary>
		/// Asynchronously gets a contributor directly from the database.
		/// Updates the cache as needed.
		/// </summary>
		/// <param name="userID">The user ID of an authenticated account.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetAsync(int userID, bool throwExceptions = false)
		{
			return Task.Run(() =>
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
						contributor.XenforoID = result.Get<int?>("XenforoID");
						contributor.TotalCredits = result.Get<float>("TotalCredits");
						contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
						contributor.LastAmount = result.Get<float>("LastAmount");
						contributor.Tier = result.Get<int>("Tier");
						contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
						contributor.Notifications = (Notifications)result.Get<int>("Notifications");
						contributor.Settings = (Settings)result.Get<int>("Settings");
						//contributor.Synced = true;

						Contributor old = _cache.Find(c => c.ID == contributorID);
						if (old != null)
						{
							// Update accounts and remove the previous contributor object
							if (!old.Accounts.Contains(userID))
							{
								old.Accounts.Add(userID);
							}
							contributor.Accounts = new List<int>(old.Accounts);
							_cache.RemoveAll(c => c.ID == contributorID);
						}

						_cache.Add(contributor);
						return contributor;
					}
				}
				_cache.RemoveAll(c => c.ID == contributorID);
				if (throwExceptions)
					throw new ContributorNotFoundException(userID);
				else
					return null;
			});
		}

		/// <summary>
		/// Gets a contributor from the cache.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor GetByXenforoID(int xenforoID)
		{
			return _cache.Find(c => c.XenforoID.HasValue && c.XenforoID.Value == xenforoID);
		}

		/// <summary>
		/// Asynchronously gets a contributor from the database.
		/// Updates the cache as needed.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetByXenforoIDAsync(int xenforoID, bool throwExceptions = false)
		{
			return Task.Run(() =>
			{
				string query = $"SELECT * FROM {CTRS.Config.ContributorTableName} WHERE XenforoID = @0;";
				string query2 = $"SELECT UserID FROM {CTRS.Config.ContributorAccountsTableName} WHERE ContributorID = @0;";
				using (var result = db.QueryReader(query, xenforoID))
				{
					if (result.Read())
					{
						Contributor contributor = new Contributor(result.Get<int>("ID"));
						using (var result2 = db.QueryReader(query, contributor.ID))
						{
							while (result2.Read())
							{
								contributor.Accounts.Add(result2.Get<int>("UserID"));
							}
							contributor.XenforoID = result.Get<int?>("XenforoID");
							contributor.TotalCredits = result.Get<float>("TotalCredits");
							contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
							contributor.LastAmount = result.Get<float>("LastAmount");
							contributor.Tier = result.Get<int>("Tier");
							contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
							contributor.Notifications = (Notifications)result.Get<int>("Notifications");
							contributor.Settings = (Settings)result.Get<int>("Settings");
							//contributor.Synced = true;

							_cache.RemoveAll(c => c.ID == contributor.ID);
							_cache.Add(contributor);

							return contributor;
						}
					}
				}
				_cache.RemoveAll(c => c.XenforoID.HasValue && c.XenforoID == xenforoID);
				if (throwExceptions)
					throw new ContributorNotFoundException(xenforoID);
				else
					return null;
			});
		}

		/// <summary>
		/// Asynchronously gets the full list of contributors directly from the database.
		/// </summary>
		/// <returns></returns>
		public Task<List<Contributor>> GetAllAsync()
		{
			return Task.Run(() =>
			{
				List<Contributor> list = new List<Contributor>();
				string query = $"SELECT * FROM {CTRS.Config.ContributorTableName};";
				string query2 = $"SELECT UserID FROM {CTRS.Config.ContributorAccountsTableName} WHERE ContributorID = @0;";
				try
				{
					using (var result = db.QueryReader(query))
					{
						while (result.Read())
						{
							Contributor contributor = new Contributor(result.Get<int>("ID"));
							using (var result2 = db.QueryReader(query2, contributor.ID))
							{
								while (result2.Read())
								{
									contributor.Accounts.Add(result2.Get<int>("UserID"));
								}
								contributor.XenforoID = result.Get<int?>("XenforoID");
								contributor.TotalCredits = result.Get<float>("TotalCredits");
								contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
								contributor.LastAmount = result.Get<float>("LastAmount");
								contributor.Tier = result.Get<int>("Tier");
								contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
								contributor.Notifications = (Notifications)result.Get<int>("Notifications");
								contributor.Settings = (Settings)result.Get<int>("Settings");
								//contributor.Synced = true;
								contributor.InitializeAll();
								list.Add(contributor);
							}
						}
					}
				}
				catch (Exception ex)
				{
					if (CTRS.Config.LogDatabaseErrors)
					{
						TShock.Log.ConsoleError("CTRS-DB: Error during contributor fetching\nMessage: " + ex.Message);
						TShock.Log.Error(ex.ToString());
					}
					return list;
				}
				return list;
			});
		}

		#region SetSync [Deprecated]
		/// <summary>
		/// Sets the sync variable of a contributor, if it exists within the cache.
		/// </summary>
		/// <param name="userID">The user ID of the contributor.</param>
		/// <param name="synced">Whether the contributor is synced or not.</param>
		//public void SetSync(int userID, bool synced)
		//{
		//	Contributor con = _cache.Find(c => c.Accounts.Contains(userID));
		//	if (con != null)
		//		con.Synced = synced;
		//}
		#endregion

		/// <summary>
		/// Runs a task to update a contributor's data.
		/// Logs any exception thrown.
		/// </summary>
		/// <param name="contributor">The contributor to update with the already-updated values set.</param>
		/// <param name="updates">The list of values to update.</param>
		/// <returns>True if it goes smooth, false if exceptions are thrown.</returns>
		public bool Update(Contributor contributor, ContributorUpdates updates)
		{
			try
			{
				Task.Run(() => UpdateAsync(contributor, updates));
				return true;
			}
			catch (Exception e)
			{
				TShock.Log.ConsoleError($"CTRS: An error occurred while updating a contributor's (ACCOUNT: {contributor.Accounts.ElementAtOrDefault(0)}) info\nMessage: {e.Message}\nCheck logs for more details");
				TShock.Log.Error(e.ToString());
				return false;
			}
		}

		/// <summary>
		/// Asynchronously updates a contributor's data.
		/// </summary>
		/// <param name="contributor">The contributor to update with the already-updated values set.</param>
		/// <param name="updates">The list of values to update.</param>
		/// <returns>True if it updates one row, false if anything else.</returns>
		public async Task<bool> UpdateAsync(Contributor contributor, ContributorUpdates updates)
		{
			if (updates == 0)
				return true;

			return await Task.Run(() =>
			{
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

				string query = $"UPDATE {CTRS.Config.ContributorTableName} SET {String.Join(", ", updatesList)} WHERE ID = @0;";
				lock (_cache)
				{
					lock (syncLock)
					{
						if (db.Query(query, contributor.ID,
							contributor.XenforoID,
							contributor.TotalCredits,
							contributor.LastDonation.ToUnixTime(),
							contributor.LastAmount,
							contributor.Tier,
							Tools.ColorToRGB(contributor.ChatColor),
							(int)contributor.Notifications,
							(int)contributor.Settings) == 1)
						{
							_cache.RemoveAll(c => c.ID == contributor.ID);
							_cache.Add(contributor);
							return true;
						}
						else
							return false;
					}
				}
			});
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