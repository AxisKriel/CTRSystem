using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using MySql.Data.MySqlClient;
using TShockAPI;
using Dapper;

namespace CTRSystem.DB
{
	public class TierManager : DbManager
	{	
		/// <summary>
		/// The list of all currently loaded contributor tiers.
		/// </summary>
		public List<Tier> Tiers { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TierManager"/> class.
		/// Manages and caches <see cref="Tier"/> instances loaded from a database.
		/// </summary>
		/// <param name="main">The parent CTRSystem instance.</param>
		public TierManager(CTRS main) : base(main)
		{
			Task.Run(CreateTablesAsync);

			#region old code
			//var creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite
			//	? (IQueryBuilder)new SqliteQueryCreator()
			//	: new MysqlQueryCreator());

			//if (creator.EnsureTableStructure(new SqlTable(_main.Config.TierTableName,
			//		new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
			//		new SqlColumn("Name", MySqlDbType.VarChar) { Length = 12, Unique = true },
			//		new SqlColumn("CreditsRequired", MySqlDbType.Float) { NotNull = true, DefaultValue = "0" },
			//		new SqlColumn("ShortName", MySqlDbType.VarChar) { NotNull = true, DefaultValue = "", Length = 6 },
			//		new SqlColumn("ChatColor", MySqlDbType.Text) { NotNull = true, DefaultValue = "" },
			//		new SqlColumn("Permissions", MySqlDbType.Text) { NotNull = true, DefaultValue = "" },
			//		new SqlColumn("ExperienceMultiplier", MySqlDbType.Float) { NotNull = true, DefaultValue = "1" })))
			//{
			//	TShock.Log.ConsoleInfo($"CTRS: created table '{_main.Config.TierTableName}'");
			//}
			#endregion

			// Load all tiers to the cache
			Task.Run(async () => Tiers = await GetAllAsync());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TierManager"/> class using a custom connection string.
		/// Manages and caches <see cref="Tier"/> instances loaded from a database.
		/// </summary>
		/// <param name="main">The parent CTRSystem instance.</param>
		/// <param name="connectionString">The connection string to use when connecting to the database.</param>
		public TierManager(CTRS main, string connectionString) : base(main, connectionString)
		{
			Task.Run(CreateTablesAsync);

			// Load all tiers to the cache
			Task.Run(async () => Tiers = await GetAllAsync());
		}

		public Task CreateTablesAsync()
		{
			return Task.Run(() =>
			{
				#region SQL(tiers)
				string tiers = PrepareSql($@"
				CREATE TABLE IF NOT EXISTS `{Main.Config.TierTableName}` (
					`ID` INT(11) NOT NULL AUTO_INCREMENT,
					`Name` VARCHAR(12) NULL DEFAULT NULL,
					`CreditsRequired` FLOAT NOT NULL DEFAULT '0',
					`ShortName` VARCHAR(6) NOT NULL,
					`ChatColor` TEXT NOT NULL,
					`Permissions` TEXT NOT NULL,
					`ExperienceMultiplier` FLOAT NOT NULL DEFAULT '1',
					PRIMARY KEY (`ID`),
					UNIQUE INDEX `Name` (`Name`)
				)
				COLLATE='utf8_general_ci'
				ENGINE=InnoDB");
				#endregion

				using (var db = OpenConnection())
				{
					try
					{
						// Create Table {Tiers}
						db.Execute(tiers);
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

		/// <summary>
		/// Gets a tier from the tier list.
		/// </summary>
		/// <param name="id">The tier ID.</param>
		/// <returns>A tier with a matching ID, or null if none is found.</returns>
		public Tier Get(int id)
		{
			return Tiers.Find(t => t.ID == id);
		}

		/// <summary>
		/// Asynchronously gets a tier from the tier list.
		/// If a tier isn't found, attempts to fetch one from the database.
		/// </summary>
		/// <param name="id">The tier ID.</param>
		/// <exception cref="TierNotFoundException">Thrown if the database query returns no results.</exception>
		/// <returns>A tier with a matching ID.</returns>
		public Task<Tier> GetAsync(int id)
		{
			return Task.Run(() =>
			{
				Tier tier = Tiers.Find(t => t.ID == id);
				if (tier != null)
					return tier;
				else
				{
					#region SQL(tier_by_id)
					string tier_by_id = PrepareSql($@"
					SELECT *
					FROM {Main.Config.TierTableName}
					WHERE ID = @Id");
					#endregion

					using (var db = OpenConnection())
					{
						tier = (Tier)db.Query<Tier.DataModel>(tier_by_id, new { Id = id }).SingleOrDefault();
						if (tier == null)
							throw new TierNotFoundException(id);
						else
						{
							Tiers.Add(tier);
							return tier;
						}
					}
				}
			});
		}

		/// <summary>
		/// Asynchronously gets a tier from the tier list.
		/// If a tier isn't found, attempts to fetch one from the database.
		/// </summary>
		/// <param name="id">The tier name.</param>
		/// <exception cref="TierNotFoundException">Thrown if the database query returns no results.</exception>
		/// <returns>A tier with a matching name.</returns>
		public Task<Tier> GetAsync(string name)
		{
			return Task.Run(() =>
			{
				Tier tier = Tiers.Find(t => t.Name == name);
				if (tier != null)
					return tier;
				else
				{
					#region SQL(tier_by_name)
					string tier_by_name = PrepareSql($@"
					SELECT *
					FROM {Main.Config.TierTableName}
					WHERE Name = @Name");
					#endregion

					using (var db = OpenConnection())
					{
						tier = (Tier)db.Query<Tier.DataModel>(tier_by_name, new { Name = name }).SingleOrDefault();
						if (tier == null)
							throw new TierNotFoundException(name);
						else
						{
							Tiers.Add(tier);
							return tier;
						}
					}
				}
			});
		}

		/// <summary>
		/// Gets the ideal tier based on a credit balance.
		/// </summary>
		/// <param name="totalcredits">The total number of credits to account for.</param>
		/// <returns>A tier that matches the given credit balance, or null if none is found.</returns>
		public Tier GetByCredits(float totalcredits)
		{
			return Tiers.FindAll(t => t.CreditsRequired <= totalcredits).OrderBy(t => t.CreditsRequired).LastOrDefault();
		}

		/// <summary>
		/// Asynchronously fetches the ideal tier from the database based on a credit balance.
		/// </summary>
		/// <param name="totalcredits">The total number of credits to account for.</param>
		/// <exception cref="TierNotFoundException">Thrown if the database query returns no results.</exception>
		/// <returns>A tier that matches the given credit balance.</returns>
		public Task<Tier> GetByCreditsAsync(float totalcredits)
		{
			return Task.Run(() =>
			{
				#region SQL(tier_by_credits)
				string tier_by_credits = PrepareSql($@"
				SELECT *
				FROM {Main.Config.TierTableName}
				WHERE CreditsRequired <= @TotalCredits
				ORDER BY CreditsRequired DESC
				LIMIT 1");
				#endregion

				using (var db = OpenConnection())
				{
					Tier tier = (Tier)db.Query<Tier.DataModel>(tier_by_credits, new { TotalCredits = totalcredits }).SingleOrDefault();

					if (tier != null)
					{
						if (!Tiers.Contains(tier))
							Tiers.Add(tier);
						return tier;
					}
					else
						throw new TierNotFoundException(null);
				}
			});
		}

		/// <summary>
		/// Asynchronously fetches all tiers from the database.
		/// </summary>
		/// <returns>A list of all tiers that exist in the database.</returns>
		public Task<List<Tier>> GetAllAsync()
		{
			return Task.Run(() =>
			{
				#region SQL(select_all)
				string select_all = ($"SELECT * FROM {Main.Config.TierTableName}");
				#endregion

				using (var db = OpenConnection())
				{
					return db.Query<Tier.DataModel>(select_all).Cast<Tier>().ToList();
				}
			});
		}

		/// <summary>
		/// Upgrades a contributor's tier based on their total credit balance.
		/// </summary>
		/// <param name="contributor">A reference to the contributor object to upgrade.</param>
		/// <param name="suppressNotifications">Whether or not notification updates should be suppressed.</param>
		/// <returns>A task for this action.</returns>
		public async Task UpgradeTier(Contributor contributor, bool suppressNotifications = false)
		{
			if (suppressNotifications
				|| (contributor.Notifications & Notifications.TierUpdate) == Notifications.TierUpdate)
			{
				ContributorUpdates updates = 0;
				Tier tier = await GetByCreditsAsync(contributor.TotalCredits);
				if (contributor.Tier != tier.ID)
				{
					contributor.Tier = tier.ID;

					// Don't touch notifications on suppress
					if (!suppressNotifications)
					{
						contributor.Notifications |= Notifications.NewTier;
						contributor.Notifications ^= Notifications.TierUpdate;
						updates |= ContributorUpdates.Notifications;
					}

					updates |= ContributorUpdates.Tier;
				}


				if (!await Main.Contributors.UpdateAsync(contributor, updates))
					TShock.Log.ConsoleError("CTRS-DB: something went wrong while updating a contributor's notifications.");
			}
		}

		/// <summary>
		/// Reloads all tiers currently in cache, removing outdated ones.
		/// </summary>
		public async void Refresh()
		{
			if (Tiers.Count == 0)
				return;

			List<Tier> tiers = await GetAllAsync();
			lock (Tiers)
			{
				for (int i = 0; i < Tiers.Count; i++)
				{
					if (!tiers.Contains(Tiers[i]))
						Tiers.RemoveAt(i);
					else
						Tiers[i] = tiers.Find(t => t.ID == Tiers[i].ID);
				}
			}
		}

		public class TierManagerException : Exception
		{
			public TierManagerException(string message) : base(message)
			{

			}

			public TierManagerException(string message, Exception inner) : base(message, inner)
			{

			}
		}

		public class TierNotFoundException : TierManagerException
		{
			public TierNotFoundException(int id) : base($"Tier ID:{id} does not exist")
			{

			}

			public TierNotFoundException(string name) : base($"Tier '{name}' does not exist")
			{

			}
		}
	}
}
