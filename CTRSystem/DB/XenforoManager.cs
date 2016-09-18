using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;

namespace CTRSystem.DB
{
	public class XenforoManager : DbManager
	{
		private object syncLock = new object();

		public XenforoManager(CTRS main) : base(main)
		{
			// Make sure the tshock_id column exists
			try
			{
				using (var db = OpenConnection())
				{
					db.Execute("ALTER TABLE xf_user ADD tshock_id INT UNIQUE");
				}
			}
			catch (MySqlException)
			{
				// A duplicate column error is thrown if it already exists. Disregard.
			}
		}

		/// <inheritdoc/>
		protected override IDbConnection OpenConnection()
		{
			string[] host = Main.Config.Xenforo.MySqlHost.Split(':');
			return new MySqlConnection
			{
				ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
					host[0],
					host.Length == 1 ? "3306" : host[1],
					Main.Config.Xenforo.MySqlDbName,
					Main.Config.Xenforo.MySqlUsername,
					Main.Config.Xenforo.MySqlPassword)
			};
		}

		public Task<XFUser> GetAsync(int tshockID)
		{
			return Task.Run(() =>
			{
				string query = "SELECT user_id, username, adcredit FROM xf_user WHERE tshock_id = @TShockID";
				using (var db = OpenConnection())
				{
					return db.Query<XFUser>(query, new { TShockID = tshockID }).SingleOrDefault();
				}
			});
		}

		public async Task<bool> SetTShockID(int userID, int tshockID)
		{
			// Only remember the first authenticated account
			if (await GetAsync(tshockID) != null)
				return true;

			return await Task.Run(() =>
			{
				string query = "UPDATE xf_user SET tshock_id = @TShockID WHERE user_id = @UserID";
				using (var db = OpenConnection())
				{
					return (db.Execute(query, new { UserID = userID, TShockID = tshockID }) == 1);
				}
			});
		}

		public async Task<float> GetContributorCredits(Contributor contributor)
		{
			// Only works if the contributor has linked their Xenforo account to their TShock account
			if (!contributor.XenforoId.HasValue || contributor.Accounts.Count == 0)
				return 0f;

			// Note: Currently, Xenforo will only store the first account to successfully authenticate
			XFUser user = await GetAsync(contributor.Accounts[0]);
			return user.Credits;
		}
	}
}
