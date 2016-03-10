﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;
using System.Net;
using Newtonsoft.Json;
using CTRSystem.DB;
using System.IO;

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
		private Dictionary<int, Credentials> activePlayers = new Dictionary<int, Credentials>();

		public LoginManager()
		{

		}

		public bool AddPlayer(int userID)
		{
			if (activePlayers.ContainsKey(userID))
				return false;

			activePlayers.Add(userID, new Credentials());
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
			if (!activePlayers.ContainsKey(userID))
				return null;
			return activePlayers[userID];
		}

		public Credentials Get(TSPlayer player)
		{
			if (player == null || player.User == null)
				return null;

			return Get(player.User.ID);
		}

		public bool RemovePlayer(int userID)
		{
			return activePlayers.Remove(userID);
		}

		public bool RemovePlayer(TSPlayer player)
		{
			if (player == null || player.User == null)
				return false;

			return RemovePlayer(player.User.ID);
		}

		public bool Update(int userID, string username = null, string password = null)
		{
			if (!activePlayers.ContainsKey(userID))
				return false;

			if (username != null)
				activePlayers[userID].Username = username;
			if (password != null)
				activePlayers[userID].Password = password;
			return true;
		}

		public bool Update(TSPlayer player, string username = null, string password = null)
		{
			if (player == null || player.User == null)
				return false;

			return Update(player.User.ID, username, password);
		}

		public async Task<LMReturnCode> Authenticate(TSPlayer player, Contributor contributor)
		{
			Credentials c = Get(player);
			if (c == null)
				return LMReturnCode.UnloadedCredentials;

			WebClient client = new WebClient();
			client.Headers.Add("Accept-Language", " en-US,en;q=0.5");
			client.Headers.Add("Accept-Encoding", "gzip, deflate");
			client.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
			client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:46.0) Gecko/20100101 Firefox/46.0");
			client.Headers.Add("Content-Type", "application/json;charset=UTF-8");

			var sb = new StringBuilder();
			sb.Append(CTRS.Config.Xenforo.XenAPIURI ?? "http://sbplanet.co/forums/api.php");

			// REQUEST: api.php?action=authenticate&username=USERNAME&password=PASSWORD
			sb.Append("?action=authenticate");
			if (String.IsNullOrEmpty(c.Username))
			{
				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("AUTH ERROR: {Username} was null");
#endif
				#endregion
				return LMReturnCode.EmptyParameter;
			}
			sb.Append("&username=" + c.Username);
			if (String.IsNullOrEmpty(c.Password))
			{
				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("AUTH ERROR: {Password} was null");
#endif
				#endregion
				return LMReturnCode.EmptyParameter;
			}
			sb.Append("&password=" + c.Password);

			#region DEBUG
#if DEBUG
			TShock.Log.ConsoleInfo("REQUESTING: " + sb.ToString());
#endif
			#endregion
			string response = "";
			try
			{
				response = await client.DownloadStringTaskAsync(sb.ToString());
			}
			catch (WebException e)
			{
				using (WebResponse r = e.Response)
				{
					HttpWebResponse webr = (HttpWebResponse)r;
					using (Stream data = r.GetResponseStream())
					{
						using (var reader = new StreamReader(data))
						{
							response = reader.ReadToEnd();
						}
					}
				}
			}

			#region DEBUG
#if DEBUG
			TShock.Log.ConsoleInfo("RESPONSE: " + response);
#endif
			#endregion
			var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
			if (dict.ContainsKey("hash"))
			{
				// Store the hash and perform a second query to get the userID
				string hash = (string)dict["hash"];
				sb.Clear();
				sb.Append(CTRS.Config.Xenforo.XenAPIURI ?? "http://sbplanet.co/forums/api.php");

				// REQUEST: api.php?action=getUser&hash=USERNAME:HASH
				sb.Append("?action=getUser");
				sb.Append("&hash=");
				sb.Append(c.Username);
				sb.Append(':');
				sb.Append(hash);

				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("REQUESTING: " + sb.ToString());
#endif
				#endregion
				try
				{
					response = await client.DownloadStringTaskAsync(sb.ToString());
				}
				catch (WebException e)
				{
					using (WebResponse r = e.Response)
					{
						HttpWebResponse webr = (HttpWebResponse)r;
						using (Stream data = r.GetResponseStream())
						{
							using (var reader = new StreamReader(data))
							{
								response = reader.ReadToEnd();
							}
						}
					}
				}

				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("RESPONSE: " + response);
#endif
				#endregion
				dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
				if (!dict.ContainsKey("user_id"))
					return LMReturnCode.UserNotFound;
				else
				{
					contributor.XenforoID = Convert.ToInt32((long)dict["user_id"]);
					if (await CTRS.Contributors.UpdateAsync(contributor, ContributorUpdates.XenforoID))
					{
						RemovePlayer(contributor.UserID.Value);
						return LMReturnCode.Success;
					}
					else
						return LMReturnCode.DatabaseError;
				}
			}
			else
				return (LMReturnCode)Convert.ToInt32((long)dict["error"]);
		}
	}

	public class Credentials
	{
		public string Username { get; set; }

		public string Password { get; set; }
	}
}
