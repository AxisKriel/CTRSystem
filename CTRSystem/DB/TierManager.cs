using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace CTRSystem.DB
{
	public class TierManager
	{
		private IDbConnection db;
		private List<Tier> _cache = new List<Tier>();
		public List<Tier> Cache
		{
			get { return _cache; }
		}

		public TierManager(IDbConnection db)
		{
			this.db = db;

			var creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite
				? (IQueryBuilder)new SqliteQueryCreator()
				: new MysqlQueryCreator());

			if (creator.EnsureTableStructure(new SqlTable("Tiers",
					new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
					new SqlColumn("Name", MySqlDbType.VarChar) { Length = 12, Unique = true },
					new SqlColumn("CreditsRequired", MySqlDbType.Float),
					new SqlColumn("ShortName", MySqlDbType.VarChar) { Length = 6 },
					new SqlColumn("ChatColor", MySqlDbType.Text) { DefaultValue = null },
					new SqlColumn("Permissions", MySqlDbType.Text),
					new SqlColumn("ExperienceMultiplier", MySqlDbType.Float) { DefaultValue = "1.00" })))
			{
				TShock.Log.ConsoleInfo("CTRS: created table 'Tiers'");
			}
		}

		public Task<Tier> GetAsync(int id)
		{
			return Task.Run(() =>
			{
				Tier tier = _cache.Find(t => t.ID == id);
				if (tier != null)
					return tier;
				else
				{
					string query = "SELECT * FROM Tiers WHERE ID = @0;";
					using (var result = db.QueryReader(query, id))
					{
						if (result.Read())
						{
							tier = new Tier(id)
							{
								Name = result.Get<string>("Name"),
								CreditsRequired = result.Get<float>("CreditsRequired"),
								ShortName = result.Get<string>("ShortName"),
								ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
								Permissions = result.Get<string>("Permissions").Split(',').ToList(),
								ExperienceMultiplier = result.Get<float>("ExperienceMultiplier")
							};
							_cache.Add(tier);
							return tier;
						}
						else
							throw new TierNotFoundException(id);
					}
				}
			});
		}

		public Task<Tier> GetAsync(string name)
		{
			return Task.Run(() =>
			{
				Tier tier = _cache.Find(t => t.Name == name);
				if (tier != null)
					return tier;
				else
				{
					string query = "SELECT * FROM Tiers WHERE Name = @0;";
					using (var result = db.QueryReader(query, name))
					{
						if (result.Read())
						{
							tier = new Tier(result.Get<int>("ID"))
							{
								Name = result.Get<string>("Name"),
								CreditsRequired = result.Get<float>("CreditsRequired"),
								ShortName = result.Get<string>("ShortName"),
								ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
								Permissions = result.Get<string>("Permissions").Split(',').ToList(),
								ExperienceMultiplier = result.Get<float>("ExperienceMultiplier")
							};
							_cache.Add(tier);
							return tier;
						}
						else
							throw new TierNotFoundException(name);
					}
				}
			});
		}

		public Task<Tier> GetByCreditsAsync(float totalcredits)
		{
			return Task.Run(() =>
			{
				string query = "SELECT * FROM Tiers WHERE CreditsRequired >= @0 ORDER BY CreditsRequired LIMIT 1;";
				using (var result = db.QueryReader(query, totalcredits))
				{
					if (result.Read())
					{
						Tier tier = new Tier(result.Get<int>("ID"))
						{
							Name = result.Get<string>("Name"),
							CreditsRequired = result.Get<float>("CreditsRequired"),
							ShortName = result.Get<string>("ShortName"),
							ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
							Permissions = result.Get<string>("Permissions").Split(',').ToList(),
							ExperienceMultiplier = result.Get<float>("ExperienceMultiplier")
						};
						if (!_cache.Contains(tier))
							_cache.Add(tier);
						return tier;
					}
					throw new TierNotFoundException(null);
				}
			});
		}

		public Task<List<Tier>> GetAllAsync()
		{
			return Task.Run(() =>
			{
				List<Tier> list = new List<Tier>();
				string query = "SELECT * FROM Tiers;";
				using (var result = db.QueryReader(query))
				{
					while (result.Read())
					{
						list.Add(new Tier(result.Get<int>("ID"))
						{
							Name = result.Get<string>("Name"),
							CreditsRequired = result.Get<int>("CreditsRequired"),
							ShortName = result.Get<string>("ShortName"),
							ChatColor = Tools.ColorFromRGB(result.Get<string>("ChatColor")),
							Permissions = result.Get<string>("Permissions").Split(',').ToList(),
							ExperienceMultiplier = result.Get<float>("ExperienceMultiplier")
						});
					}
				}
				return list;
			});
		}

		public async Task UpgradeTier(Contributor contributor)
		{
			if ((contributor.Notifications & Notifications.TierUpdate) == Notifications.TierUpdate)
			{
				ContributorUpdates updates = 0;
				Tier tier = await GetByCreditsAsync(contributor.TotalCredits);
				if (contributor.Tier != tier.ID)
				{
					contributor.Tier = tier.ID;
					contributor.Notifications |= Notifications.NewTier;
					updates |= ContributorUpdates.Tier;
				}
				contributor.Notifications ^= Notifications.TierUpdate;
				updates |= ContributorUpdates.Notifications;

				if (!await CTRS.Contributors.UpdateAsync(contributor, updates))
					TShock.Log.ConsoleError("CTRS-DB: something went wrong while updating a contributor's notifications.");
			}
		}

		/// <summary>
		/// Reloads all tiers currently in cache, removing outdated ones.
		/// </summary>
		public async void Refresh()
		{
			if (_cache.Count == 0)
				return;

			List<Tier> tiers = await GetAllAsync();
			for (int i = 0; i < _cache.Count; i++)
			{
				if (!tiers.Contains(_cache[i]))
					_cache.RemoveAt(i);
				else
					_cache[i] = tiers.Find(t => t.ID == _cache[i].ID);
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
