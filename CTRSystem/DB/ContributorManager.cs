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
				new SqlColumn("Accounts", MySqlDbType.Text) { Unique = true, DefaultValue = "" },
				new SqlColumn("TotalCredits", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("LastDonation", MySqlDbType.Int64) { DefaultValue = null },
				new SqlColumn("LastAmount", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("Tier", MySqlDbType.Int32) { NotNull = true, DefaultValue = "1" },
				new SqlColumn("ChatColor", MySqlDbType.Text) { NotNull = true, DefaultValue = "" },
				new SqlColumn("Notifications", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("Settings", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" })))
			{
				TShock.Log.ConsoleInfo($"CTRS: created table '{CTRS.Config.ContributorTableName}'");
			}

			// Load all contributors to the cache
			Task.Run(() => LoadCache());
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

			string query = $"INSERT INTO {CTRS.Config.ContributorTableName} (Accounts, TotalCredits, LastAmount, Tier, Notifications, Settings) "
						 + "VALUES (@0, @1, @3, @4, @5, @6);";
			if (contributor.LastDonation != DateTime.MinValue)
				query = $"INSERT INTO {CTRS.Config.ContributorTableName} (Accounts, TotalCredits, LastDonation, LastAmount, Tier, Notifications, Settings) "
					  + "VALUES (@0, @1, @2, @3, @4, @5, @6);";

			lock (_cache)
			{
				try
				{
					_cache.Add(contributor);
					return db.Query(query,
						String.Join(",", contributor.Accounts),
						contributor.TotalCredits,
						contributor.LastDonation.ToUnixTime(),
						contributor.LastAmount,
						contributor.Tier,
						contributor.Notifications,
						contributor.Settings) == 1;
				}
				catch
				{
					return false;
				}
			}
		}

		public async Task<bool> AddAsync(Contributor contributor)
		{
			if (_cache.Exists(c => c.Accounts.Intersect(contributor.Accounts).Any()))
				return false;

			return await Task.Run(() =>
			{
				string query = $"INSERT INTO {CTRS.Config.ContributorTableName} (Accounts, XenforoID, TotalCredits, LastAmount, Tier, Notifications, Settings) "
						 + "VALUES (@0, @1, @2, @4, @5, @6, @7);";
				if (contributor.LastDonation != DateTime.MinValue)
					query = $"INSERT INTO {CTRS.Config.ContributorTableName} (Accounts, XenforoID, TotalCredits, LastDonation, LastAmount, Tier, Notifications, Settings) "
						  + "VALUES (@0, @1, @2, @3, @4, @5, @6, @7);";

				lock (_cache)
				{
					try
					{
						_cache.Add(contributor);
						return db.Query(query,
							String.Join(",", contributor.Accounts),
							contributor.XenforoID.Value,
							contributor.TotalCredits,
							contributor.LastDonation.ToUnixTime(),
							contributor.LastAmount,
							contributor.Tier,
							contributor.Notifications,
							contributor.Settings) == 1;
					}
					catch
					{
						return false;
					}
				}
			});
		}

		/// <summary>
		/// Gets a contributor from the cache, disregarding their sync state.
		/// </summary>
		/// <param name="userID">The contributor ID, usually their user ID.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor Get(int userID)
		{
			return _cache.Find(c => c.Accounts.Contains(userID));
		}

		/// <summary>
		/// Asynchronously gets a contributor from the cache.
		/// Also synchronizes with the database if needed.
		/// </summary>
		/// <param name="userID">The contributor ID, usually their user ID.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <param name="force">If true, will force-synchronize with the database regardless of the sync state.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetAsync(int userID, bool throwExceptions = false, bool force = false)
		{
			return Task.Run(() =>
			{
				Contributor contributor = Get(userID);
				if (contributor == null)
					return null;
				else if (force || !contributor.Synced)
				{
					string query = $"SELECT * FROM {CTRS.Config.ContributorTableName} WHERE Accounts LIKE @0;";
					using (var result = db.QueryReader(query, $"%{userID}%"))
					{
						if (result.Read())
						{
							contributor = Contributor.Parse(result.Get<string>("Accounts").Split(','));
							contributor.XenforoID = result.Get<int?>("XenforoID");
							contributor.TotalCredits = result.Get<float>("TotalCredits");
							contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
							contributor.LastAmount = result.Get<float>("LastAmount");
							contributor.Tier = result.Get<int>("Tier");
							contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
							contributor.Notifications = (Notifications)result.Get<int>("Notifications");
							contributor.Settings = (Settings)result.Get<int>("Settings");
							contributor.Synced = true;

							_cache.RemoveAll(c => c.Accounts.Contains(userID));
							_cache.Add(contributor);

							contributor.Initialize(TShock.Players.FirstOrDefault(p => p != null && p.IsLoggedIn && p.User.ID == userID).Index);
							return contributor;
						}
					}
					_cache.RemoveAll(c => c.Accounts.Contains(userID));
					if (throwExceptions)
						throw new ContributorNotFoundException(userID);
					else
						return null;
				}
				else
					return contributor;
			});
		}

		/// <summary>
		/// Gets a contributor from the cache, disregarding their sync state.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor GetByXenforoID(int xenforoID)
		{
			return _cache.Find(c => c.XenforoID.HasValue && c.XenforoID.Value == xenforoID);
		}

		/// <summary>
		/// Asynchronously gets a contributor from the database.
		/// Also synchronizes with the database if needed.
		/// Note: Ignores the cache.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <param name="force">If true, will force-synchronize with the database regardless of the sync state.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetByXenforoIDAsync(int xenforoID, bool throwExceptions = false)
		{
			return Task.Run(() =>
			{
				//Contributor contributor = GetByXenforoID(xenforoID);
				//if (contributor == null)
				//	return null;
				string query = $"SELECT * FROM {CTRS.Config.ContributorTableName} WHERE XenforoID = @0;";
				using (var result = db.QueryReader(query, xenforoID))
				{
					if (result.Read())
					{
						Contributor contributor = Contributor.Parse(result.Get<string>("Accounts").Split(','));
						contributor.XenforoID = result.Get<int?>("XenforoID");
						contributor.TotalCredits = result.Get<float>("TotalCredits");
						contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
						contributor.LastAmount = result.Get<float>("LastAmount");
						contributor.Tier = result.Get<int>("Tier");
						contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
						contributor.Notifications = (Notifications)result.Get<int>("Notifications");
						contributor.Settings = (Settings)result.Get<int>("Settings");
						contributor.Synced = true;

						_cache.RemoveAll(c => c.XenforoID.HasValue && c.XenforoID.Value == xenforoID);
						_cache.Add(contributor);

						contributor.InitializeAll();
						return contributor;
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
				using (var result = db.QueryReader(query))
				{
					while (result.Read())
					{
						Contributor contributor = Contributor.Parse(result.Get<string>("Accounts").Split(','));
						contributor.XenforoID = result.Get<int?>("XenforoID");
						contributor.TotalCredits = result.Get<float>("TotalCredits");
						contributor.LastDonation = result.Get<long>("LastDonation").FromUnixTime();
						contributor.LastAmount = result.Get<float>("LastAmount");
						contributor.Tier = result.Get<int>("Tier");
						contributor.ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor"));
						contributor.Notifications = (Notifications)result.Get<int>("Notifications");
						contributor.Settings = (Settings)result.Get<int>("Settings");
						contributor.Synced = true;
						contributor.InitializeAll();
						list.Add(contributor);
					}
				}
				return list;
			});
		}

		/// <summary>
		/// Sets the sync variable of a contributor, if it exists within the cache.
		/// </summary>
		/// <param name="userID">The user ID of the contributor.</param>
		/// <param name="synced">Whether the contributor is synced or not.</param>
		public void SetSync(int userID, bool synced)
		{
			Contributor con = _cache.Find(c => c.Accounts.Contains(userID));
			if (con != null)
				con.Synced = synced;
		}

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
				// Update using the xenforo ID as much as possible, unless the ID itself is being set
				bool updateByXenforoID = contributor.XenforoID.HasValue
				&& !((updates & ContributorUpdates.XenforoID) == ContributorUpdates.XenforoID && contributor.XenforoID.HasValue);

				List<string> updatesList = new List<string>();
				if ((updates & ContributorUpdates.Accounts) == ContributorUpdates.Accounts && updateByXenforoID)
					updatesList.Add("Accounts = @1");
				if ((updates & ContributorUpdates.XenforoID) == ContributorUpdates.XenforoID && !updateByXenforoID)
					updatesList.Add("XenforoID = @2");
				if ((updates & ContributorUpdates.TotalCredits) == ContributorUpdates.TotalCredits)
					updatesList.Add("TotalCredits = @3");
				if ((updates & ContributorUpdates.LastDonation) == ContributorUpdates.LastDonation)
					updatesList.Add("LastDonation = @4");
				if ((updates & ContributorUpdates.LastAmount) == ContributorUpdates.LastAmount)
					updatesList.Add("LastAmount = @5");
				if ((updates & ContributorUpdates.Tier) == ContributorUpdates.Tier)
					updatesList.Add("Tier = @6");
				if ((updates & ContributorUpdates.ChatColor) == ContributorUpdates.ChatColor)
					updatesList.Add("ChatColor = @7");
				if ((updates & ContributorUpdates.Notifications) == ContributorUpdates.Notifications)
					updatesList.Add("Notifications = @8");
				if ((updates & ContributorUpdates.Settings) == ContributorUpdates.Settings)
					updatesList.Add("Settings = @9");

				string index = updateByXenforoID ? contributor.XenforoID.Value.ToString() : contributor.Accounts[0].ToString();
				string clause = updateByXenforoID ? "XenforoID =" : "Accounts LIKE";
				string query = $"UPDATE {CTRS.Config.ContributorTableName} SET {String.Join(", ", updatesList)} WHERE {clause} @0;";
				lock (_cache)
				{
					lock (syncLock)
					{
						if (db.Query(query, index,
							String.Join(",", contributor.Accounts),
							contributor.XenforoID,
							contributor.TotalCredits,
							contributor.LastDonation.ToUnixTime(),
							contributor.LastAmount,
							contributor.Tier,
							Tools.ColorToRGB(contributor.ChatColor),
							(int)contributor.Notifications,
							(int)contributor.Settings) == 1)
						{
							_cache.RemoveAll(c =>
							{
								if (updateByXenforoID)
									return c.XenforoID == contributor.XenforoID;
								else
									return c.Accounts.Contains(contributor.Accounts[0]);
							});
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