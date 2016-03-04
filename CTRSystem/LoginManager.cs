using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;
using System.Net;
using Newtonsoft.Json;
using CTRSystem.DB;

namespace CTRSystem
{
	public enum LMReturnCode
	{
		Success = 0,
		EmptyParameter = 1,
		InsuficientParameters = 3,
		UserNotFound = 4,
		IncorrectData = 5,
		UnloadedCredentials = 6,
		DatabaseError = 7
	}

	public class LoginManager
	{
		private List<Credentials> activePlayers = new List<Credentials>();

		public LoginManager()
		{

		}

		public bool AddPlayer(int userID)
		{
			if (activePlayers.Exists(c => c.ID == userID))
				return false;

			activePlayers.Add(new Credentials(userID));
			return true;
		}

		public bool AddPlayer(TSPlayer player)
		{
			if (player == null || player.User == null)
				return false;
			
			return AddPlayer(player.User.ID);
		}

		public Credentials Get(int userID)
		{
			return activePlayers.Find(c => c.ID == userID);
		}

		public Credentials Get(TSPlayer player)
		{
			if (player == null || player.User == null)
				return null;

			return Get(player.User.ID);
		}

		public bool RemovePlayer(int userID)
		{
			return activePlayers.RemoveAll(c => c.ID == userID) > 0;
		}

		public bool RemovePlayer(TSPlayer player)
		{
			if (player == null || player.User == null)
				return false;

			return RemovePlayer(player.User.ID);
		}

		public bool Update(int userID, string username = null, string password = null)
		{
			Credentials c = Get(userID);
			if (c == null)
				return false;

			if (username != null)
				c.Username = username;
			if (password != null)
				c.Password = password;
			return true;
		}

		public bool Update(TSPlayer player, string username = null, string password = null)
		{
			if (player == null || player.User == null)
				return false;

			return Update(player.User.ID);
		}

		public async Task<LMReturnCode> Authenticate(TSPlayer player, Contributor contributor)
		{
			Credentials c = Get(player);
			if (c == null)
				return LMReturnCode.UnloadedCredentials;

			WebClient client = new WebClient();
			var sb = new StringBuilder();
			sb.Append(CTRS.Config.Xenforo.XenAPIURI ?? "http://sbplanet.co/forums/api.php");

			// REQUEST: api.php?action=authenticate&username=USERNAME&password=PASSWORD
			sb.Append("?action=authenticate");
			if (String.IsNullOrEmpty(c.Username))
				return LMReturnCode.EmptyParameter;
			sb.Append("&username=" + c.Username);
			if (String.IsNullOrEmpty(c.Password))
				return LMReturnCode.EmptyParameter;
			sb.Append("&password=" + c.Password);

#if DEBUG
			TShock.Log.ConsoleInfo("REQUESTING: " + sb.ToString());
#endif
			string response = await client.DownloadStringTaskAsync(sb.ToString());
#if DEBUG
			TShock.Log.ConsoleInfo("RESPONSE: " + response);
#endif
			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
			if (dict.ContainsKey("hash"))
			{
				// Store the hash and perform a second query to get the userID
				string hash = dict["hash"];
				sb.Clear();
				sb.Append(CTRS.Config.Xenforo.XenAPIURI ?? "http://sbplanet.co/forums/api.php");

				// REQUEST: api.php?action=getUser&hash=USERNAME:HASH
				sb.Append("?action=getUser");
				sb.Append("&hash=");
				sb.Append(c.Username);
				sb.Append(':');
				sb.Append(hash);

#if DEBUG
				TShock.Log.ConsoleInfo("REQUESTING: " + sb.ToString());
#endif
				response = await client.DownloadStringTaskAsync(sb.ToString());
#if DEBUG
				TShock.Log.ConsoleInfo("RESPONSE: " + response);
#endif
				dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
				if (!dict.ContainsKey("user_id"))
					return LMReturnCode.UserNotFound;
				else
				{
					contributor.XenforoID = Int32.Parse(dict["user_id"]);
					if (await CTRS.Contributors.UpdateAsync(contributor, ContributorUpdates.XenforoID))
					{
						RemovePlayer(c.ID);
						return LMReturnCode.Success;
					}
					else
						return LMReturnCode.DatabaseError;
				}
			}
			else
				return (LMReturnCode)Int32.Parse(dict["error"]);
		}
	}

	public class Credentials
	{
		public int ID { get; set; }

		public string Username { get; set; }

		public string Password { get; set; }

		public Credentials()
		{

		}

		public Credentials(int userID) : this()
		{
			ID = userID;
		}
	}
}
