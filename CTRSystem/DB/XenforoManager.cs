using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using MySql.Data.MySqlClient;
using TShockAPI.DB;

namespace CTRSystem.DB
{
	public class XenforoManager
	{
		private IDbConnection db;
		private object synclock = new object();

		public XenforoManager(IDbConnection db)
		{
			this.db = db;

			// Make sure the tshock_id column exists
			try
			{
				db.Query("ALTER TABLE xf_user ADD tshock_id int UNIQUE;");
			}
			catch (MySqlException)
			{
				// A duplicate column error is thrown if it already exists. Disregard.
			}
		}

		public Task<XFUser> GetAsync(int tshockID)
		{
			return Task.Run(() =>
			{
				string query = "SELECT user_id, username, adcredit FROM xf_user WHERE tshock_id = @0;";
				using (var result = db.QueryReader(query, tshockID))
				{
					if (result.Read())
						return new XFUser()
						{
							user_id = result.Get<int>("user_id"),
							username = result.Get<string>("username"),
							adcredit = result.Get<float>("adcredit")
						};
					else
						return null;
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
				string query = "UPDATE xf_user SET tshock_id = @1 WHERE user_id = @0;";
				return (db.Query(query, userID, tshockID) == 1);
			});
		}

		public async Task<float> GetContributorCredits(Contributor contributor)
		{
			// Only works if the contributor has linked their Xenforo account to their TShock account
			if (!contributor.XenforoID.HasValue || contributor.Accounts.Count == 0)
				return 0f;

			// Note: Currently, Xenforo will only store the first account to successfully authenticate
			XFUser user = await GetAsync(contributor.Accounts[0]);
			return user.Credits;
		}
	}
}
