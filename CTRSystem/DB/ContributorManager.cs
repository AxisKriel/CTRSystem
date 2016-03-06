using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;
using CTRSystem.Extensions;

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

			if (creator.EnsureTableStructure(new SqlTable("Contributors",
				new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
				new SqlColumn("UserID", MySqlDbType.Int32) { Unique = true, DefaultValue = null },
				new SqlColumn("XenforoID", MySqlDbType.Int32) { Unique = true, DefaultValue = null },
				new SqlColumn("TotalCredits", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("LastDonation", MySqlDbType.Int64) { DefaultValue = null },
				new SqlColumn("Tier", MySqlDbType.Int32) { NotNull = true, DefaultValue = "1" },
				new SqlColumn("ChatColor", MySqlDbType.Text) { NotNull = true, DefaultValue = "" },
				new SqlColumn("Notifications", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" },
				new SqlColumn("Settings", MySqlDbType.Int32) { NotNull = true, DefaultValue = "0" })))
			{
				TShock.Log.ConsoleInfo("CTRS: created table 'Contributors'");
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
			_cache = await GetAllAsync();
		}

		/// <summary>
		/// Gets a contributor from the cache, disregarding their sync state.
		/// </summary>
		/// <param name="userID">The contributor ID, usually their user ID.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Contributor Get(int userID)
		{
			return _cache.Find(c => c.UserID.HasValue && c.UserID.Value == userID);
		}

		/// <summary>
		/// Asynchronously gets a contributor from the cache.
		/// Also synchronizes with the database if needed.
		/// </summary>
		/// <param name="userID">The contributor ID, usually their user ID.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetAsync(int userID, bool throwExceptions = false)
		{
			return Task.Run(() =>
			{
				Contributor contributor = Get(userID);
				if (contributor == null)
					return null;
				else if (!contributor.Synced)
				{
					string query = "SELECT * FROM Contributors WHERE UserID = @0;";
					using (var result = db.QueryReader(query, userID))
					{
						if (result.Read())
						{
							contributor = new Contributor(userID)
							{
								XenforoID = result.Get<int?>("XenforoID"),
								//Credits = result.Get<int>("Credits"),
								TotalCredits = result.Get<float>("TotalCredits"),
								LastDonation = result.Get<long>("LastDonation").FromUnixTime(),
								Tier = result.Get<int>("Tier"),
								ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
								Notifications = (Notifications)result.Get<int>("Notifications"),
								Settings = (Settings)result.Get<int>("Settings"),
								Synced = true
							};
							_cache.RemoveAll(c => c.UserID.HasValue && c.UserID.Value == userID);
							_cache.Add(contributor);
							return contributor;
						}
						_cache.RemoveAll(c => c.UserID.HasValue && c.UserID.Value == userID);
						if (throwExceptions)
							throw new ContributorNotFoundException(userID);
						else
							return null;
					}
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
		/// Asynchronously gets a contributor from the cache.
		/// Also synchronizes with the database if needed.
		/// </summary>
		/// <param name="xenforoID">The contributor's xenforo ID, if any.</param>
		/// <param name="throwExceptions">If true, will throw exceptions when something goes wrong.</param>
		/// <returns>A contributor object, or null if not found.</returns>
		public Task<Contributor> GetByXenforoIDAsync(int xenforoID, bool throwExceptions = false)
		{
			return Task.Run(() =>
			{
				Contributor contributor = GetByXenforoID(xenforoID);
				if (contributor == null)
					return null;
				else if (!contributor.Synced)
				{
					string query = "SELECT * FROM Contributors WHERE XenforoID = @0;";
					using (var result = db.QueryReader(query, xenforoID))
					{
						if (result.Read())
						{
							contributor = new Contributor(result.Get<int?>("UserID"))
							{
								XenforoID = xenforoID,
								//Credits = result.Get<int>("Credits"),
								TotalCredits = result.Get<float>("TotalCredits"),
								LastDonation = result.Get<long>("LastDonation").FromUnixTime(),
								Tier = result.Get<int>("Tier"),
								ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
								Notifications = (Notifications)result.Get<int>("Notifications"),
								Settings = (Settings)result.Get<int>("Settings"),
								Synced = true
							};
							_cache.RemoveAll(c => c.XenforoID.HasValue && c.XenforoID.Value == xenforoID);
							_cache.Add(contributor);
							return contributor;
						}
						_cache.RemoveAll(c => c.XenforoID.HasValue && c.XenforoID == xenforoID);
						if (throwExceptions)
							throw new ContributorNotFoundException(xenforoID);
						else
							return null;
					}
				}
				else
					return contributor;
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
				string query = "SELECT * FROM Contributors;";
				using (var result = db.QueryReader(query))
				{
					while (result.Read())
					{
						list.Add(new Contributor(result.Get<int?>("UserID"))
						{
							XenforoID = result.Get<int?>("XenforoID"),
							//Credits = result.Get<int>("Credits"),
							TotalCredits = result.Get<float>("TotalCredits"),
							LastDonation = result.Get<long>("LastDonation").FromUnixTime(),
							Tier = result.Get<int>("Tier"),
							ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
							Notifications = (Notifications)result.Get<int>("Notifications"),
							Settings = (Settings)result.Get<int>("Settings")
						});
					}
				}
				return list;
			});
		}

		/// <summary>
		/// Clears up the cache, requiring all contributor data to be fetched once again.
		/// </summary>
		public void ClearCache()
		{
			_cache.Clear();
		}

		/// <summary>
		/// Sets the sync variable of a contributor, if it exists within the cache.
		/// </summary>
		/// <param name="userID">The user ID of the contributor.</param>
		/// <param name="synced">Whether the contributor is synced or not.</param>
		public void SetSync(int userID, bool synced)
		{
			Contributor con = _cache.Find(c => c.UserID == userID);
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
				TShock.Log.ConsoleError($"CTRS: An error occurred while updating a contributor's (ID: {contributor.UserID.Value} info\nMessage: {e.Message}\nCheck logs for more details");
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
				if ((updates & ContributorUpdates.Tier) == ContributorUpdates.Tier)
					updatesList.Add("Tier = @4");
				if ((updates & ContributorUpdates.ChatColor) == ContributorUpdates.ChatColor)
					updatesList.Add("ChatColor = @5");
				if ((updates & ContributorUpdates.Notifications) == ContributorUpdates.Notifications)
					updatesList.Add("Notifications = @6");
				if ((updates & ContributorUpdates.Settings) == ContributorUpdates.Settings)
					updatesList.Add("Settings = @7");

				string query = $"UPDATE Contributors SET {String.Join(", ", updatesList)} WHERE UserID = @0;";
				lock (syncLock)
				{
					if (db.Query(query, contributor.UserID,
						contributor.XenforoID,
						contributor.TotalCredits,
						contributor.LastDonation.ToUnixTime(),
						contributor.Tier,
						Tools.ColorToRGB(contributor.ChatColor),
						(int)contributor.Notifications,
						(int)contributor.Settings) == 1)
					{
						_cache.RemoveAll(c => c.UserID.HasValue && c.UserID.Value == contributor.UserID.Value);
						_cache.Add(contributor);
						return true;
					}
					else
						return false;
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