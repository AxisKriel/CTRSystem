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
				new SqlColumn("TotalCredits", MySqlDbType.Float),
				new SqlColumn("LastDonation", MySqlDbType.Int64) { DefaultValue = null },
				new SqlColumn("Tier", MySqlDbType.Int32),
				new SqlColumn("ChatColor", MySqlDbType.Text),
				new SqlColumn("Notifications", MySqlDbType.Int32),
				new SqlColumn("Settings", MySqlDbType.Int32))))
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

		public Task<Contributor> GetAsync(int userID, bool throwExceptions = false)
		{
			return Task.Run(() =>
			{
				Contributor contributor = _cache.Find(c => c.UserID.HasValue && c.UserID.Value == userID);
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

		public Task<Contributor> GetByXenforoIDAsync(int xenforoID, bool throwExceptions = false)
		{
			return Task.Run(() =>
			{
				Contributor contributor = _cache.Find(c => c.XenforoID.HasValue && c.XenforoID.Value == xenforoID);
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

		public async Task<bool> UpdateAsync(Contributor contributor, ContributorUpdates updates)
		{
			if (updates == 0)
				return true;

			return await Task.Run(() =>
			{
				List<string> updatesList = new List<string>();
				if ((updates & ContributorUpdates.TotalCredits) == ContributorUpdates.TotalCredits)
					updatesList.Add("TotalCredits = @1");
				if ((updates & ContributorUpdates.LastDonation) == ContributorUpdates.LastDonation)
					updatesList.Add("LastDonation = @2");
				if ((updates & ContributorUpdates.Tier) == ContributorUpdates.Tier)
					updatesList.Add("Tier = @3");
				if ((updates & ContributorUpdates.ChatColor) == ContributorUpdates.ChatColor)
					updatesList.Add("ChatColor = @4");
				if ((updates & ContributorUpdates.Notifications) == ContributorUpdates.Notifications)
					updatesList.Add("Notifications = @5");
				if ((updates & ContributorUpdates.Settings) == ContributorUpdates.Settings)
					updatesList.Add("Settings = @6");

				string query = $"UPDATE Contributors SET {String.Join(", ", updatesList)} WHERE UserID = @0;";
				lock (syncLock)
				{
					if (db.Query(query, contributor.UserID,
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