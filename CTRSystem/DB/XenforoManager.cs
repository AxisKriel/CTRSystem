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
		public XenforoManager(CTRS main) : base(main)
		{
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

		/// <summary>
		/// Fetches a <see cref="XFUser"/> object from the database.
		/// </summary>
		/// <param name="userID">The Xenforo user ID.</param>
		/// <returns>A xenforo user object containing essential data.</returns>
		public Task<XFUser> GetAsync(int userID)
		{
			return Task.Run(() =>
			{
				string query = "SELECT user_id, username, adcredit FROM xf_user WHERE user_id = @Id";
				using (var db = OpenConnection())
				{
					return db.QuerySingleOrDefault<XFUser>(query, new { Id = userID });
				}
			});
		}

		public async Task<float> GetContributorCredits(Contributor contributor)
		{
			// Only works if the contributor has linked their Xenforo account to their TShock account
			if (!contributor.XenforoId.HasValue)
				return 0f;

			XFUser user = await GetAsync(contributor.XenforoId.Value);
			return user.Credits;
		}
	}
}
