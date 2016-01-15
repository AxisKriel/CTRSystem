using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace CTRSystem.DB
{
	public class ContributorManager
	{
		private IDbConnection db;
		private object syncLock = new object();

		public ContributorManager(IDbConnection db)
		{
			this.db = db;

			var creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite
				? (IQueryBuilder)new SqliteQueryCreator()
				: new MysqlQueryCreator());

			if (creator.EnsureTableStructure(new SqlTable("Contributors",
				new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
				new SqlColumn("UserID", MySqlDbType.Int32) { Unique = true, DefaultValue = null },
				new SqlColumn("WebID", MySqlDbType.Int32) { Unique = true, DefaultValue = null },
				new SqlColumn("Credits", MySqlDbType.Int32),
				new SqlColumn("TotalCredits", MySqlDbType.Int32),
				new SqlColumn("LastDonation", MySqlDbType.Text) { DefaultValue = DateTime.MinValue.ToString("s") },
				new SqlColumn("Tier", MySqlDbType.Int32),
				new SqlColumn("ChatColor", MySqlDbType.Text),
				new SqlColumn("Notifications", MySqlDbType.Int32),
				new SqlColumn("Settings", MySqlDbType.Int32))))
			{
				TShock.Log.ConsoleInfo("CTRS: created table 'Contributors'");
			}
		}

		public Task<Contributor> GetAsync(int userID)
		{
			return Task.Run(() =>
			{
				string query = "SELECT * FROM Contributors WHERE UserID = @0;";
				using (var result = db.QueryReader(query, userID))
				{
					if (result.Read())
					{
						return new Contributor(userID)
						{
							WebID = result.Get<int?>("WebID"),
							Credits = result.Get<int>("Credits"),
							TotalCredits = result.Get<int>("TotalCredits"),
							LastDonation = DateTime.Parse(result.Get<string>("LastDonation")),
							Tier = result.Get<int>("Tier"),
							ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
							Notifications = (Notifications)result.Get<int>("Notifications"),
							Settings = (Settings)result.Get<int>("Settings")
						};
					}
					throw new ContributorNotFoundException(userID);
				}
			});
		}

		public Task<Contributor> GetByWebIDAsync(int webID)
		{
			return Task.Run(() =>
			{
				string query = "SELECT * FROM Contributors WHERE WebID = @0;";
				using (var result = db.QueryReader(query, webID))
				{
					if (result.Read())
					{
						return new Contributor(result.Get<int?>("UserID"))
						{
							WebID = webID,
							Credits = result.Get<int>("Credits"),
							TotalCredits = result.Get<int>("TotalCredits"),
							LastDonation = DateTime.Parse(result.Get<string>("LastDonation")),
							Tier = result.Get<int>("Tier"),
							ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
							Notifications = (Notifications)result.Get<int>("Notifications"),
							Settings = (Settings)result.Get<int>("Settings")
						};
					}
					throw new ContributorNotFoundException(webID);
				}
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
							WebID = result.Get<int?>("WebID"),
							Credits = result.Get<int>("Credits"),
							TotalCredits = result.Get<int>("TotalCredits"),
							LastDonation = DateTime.Parse(result.Get<string>("LastDonation")),
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

		public async Task<bool> UpdateAsync(Contributor contributor, ContributorUpdates updates)
		{
			if (updates == 0)
				return true;

			return await Task.Run(() =>
			{
				List<string> updatesList = new List<string>();
				if ((updates & ContributorUpdates.Tier) == ContributorUpdates.Tier)
					updatesList.Add("Tier = @1");
				if ((updates & ContributorUpdates.ChatColor) == ContributorUpdates.ChatColor)
					updatesList.Add("ChatColor = @2");
				if ((updates & ContributorUpdates.Notifications) == ContributorUpdates.Notifications)
					updatesList.Add("Notifications = @3");
				if ((updates & ContributorUpdates.Settings) == ContributorUpdates.Settings)
					updatesList.Add("Settings = @4");

				string query = $"UPDATE Contributors SET {String.Join(", ", updatesList)} WHERE UserID = @0;";
				lock (syncLock)
				{
					return (db.Query(query, contributor.UserID, contributor.Tier, Tools.ColorToRGB(contributor.ChatColor), (int)contributor.Notifications, (int)contributor.Settings) == 1);
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