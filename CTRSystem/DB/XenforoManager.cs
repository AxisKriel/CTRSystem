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

		public async Task<float> GetContributorCredits(Contributor contributor)
		{
			// Only works if the contributor has linked their Xenforo account to their TShock account
			if (!contributor.XenforoID.HasValue || !contributor.UserID.HasValue)
				return 0f;

			XFUser user = await GetAsync(contributor.UserID.Value);
			return user.Credits;
		}
	}
}
