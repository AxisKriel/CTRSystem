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
using System.IO;

namespace CTRSystem
{
	public class AuthResult
	{
		public Contributor Contributor { get; private set; }

		public LMReturnCode Code { get; private set; }

		public AuthResult(LMReturnCode code)
		{
			Code = code;
		}

		public AuthResult(Contributor contributor) : this(LMReturnCode.Success)
		{
			Contributor = contributor;
		}
	}

	public enum LMReturnCode
	{
		Success = 0,
		EmptyParameter = 1,
		InsuficientParameters = 3,
		UserNotFound = 4,
		IncorrectData = 5,
		UnloadedCredentials = 6,
		DatabaseError = 7,
		UserNotAContributor = 100,
		AccountLimitReached = 101
	}

	public class LoginManager
	{
		/// <summary>
		/// Authenticates an <see cref="User"/> to a Xenforo forum account and returns the <see cref="Contributor"/>
		/// if the authentication succeeds.
		/// </summary>
		/// <param name="user">The Xenforo account name.</param>
		/// <param name="credentials">The Xenforo account password.</param>
		/// <returns>
		/// A task with the <see cref="AuthResult"/> and a contributor object. The contributor is only defined
		/// if the authentication succeeds.
		/// </returns>
		public async Task<AuthResult> Authenticate(User user, Credentials credentials)
		{
			if (credentials == null)
			{
				return new AuthResult(LMReturnCode.UnloadedCredentials);
			}

			var sb = new StringBuilder();
			sb.Append(CTRS.Config.Xenforo.XenAPIURI ?? "http://sbplanet.co/forums/api.php");

			// REQUEST: api.php?action=authenticate&username=USERNAME&password=PASSWORD
			sb.Append("?action=authenticate");
			if (String.IsNullOrEmpty(credentials.Username))
			{
				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("AUTH ERROR: {Username} was null");
#endif
				#endregion
				return new AuthResult(LMReturnCode.EmptyParameter);
			}
			sb.Append("&username=" + credentials.Username);
			if (String.IsNullOrEmpty(credentials.Password))
			{
				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("AUTH ERROR: {Password} was null");
#endif
				#endregion
				return new AuthResult(LMReturnCode.EmptyParameter);
			}
			sb.Append("&password=" + credentials.Password);

			#region DEBUG
#if DEBUG
			TShock.Log.ConsoleInfo("REQUESTING: " + sb.ToString());
#endif
			#endregion
			string response = "";
			using (WebClient client = new WebClient())
			{
				try
				{
					response = await client.DownloadStringTaskAsync(sb.ToString());
				}
				catch (WebException e)
				{
					using (HttpWebResponse r = (HttpWebResponse)e.Response)
					using (var reader = new StreamReader(r.GetResponseStream()))
					{
						response = reader.ReadToEnd();
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
				sb.Append(credentials.Username);
				sb.Append(':');
				sb.Append(hash);

				#region DEBUG
#if DEBUG
				TShock.Log.ConsoleInfo("REQUESTING: " + sb.ToString());
#endif
				#endregion
				using (WebClient client = new WebClient())
				{
					try
					{
						response = await client.DownloadStringTaskAsync(sb.ToString());
					}
					catch (WebException e)
					{
						using (HttpWebResponse r = (HttpWebResponse)e.Response)
						using (var reader = new StreamReader(r.GetResponseStream()))
						{
							response = reader.ReadToEnd();
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
					return new AuthResult(LMReturnCode.UserNotFound);
				else
				{
					// Check if the user is a contributor
					List<int> groups = new List<int>();
					if (dict.ContainsKey("user_group_id"))
					{
						groups.Add(Convert.ToInt32(dict["user_group_id"]));
					}
					if (dict.ContainsKey("secondary_group_ids"))
					{
						((string)dict["secondary_group_ids"]).Split(',').ForEach(s =>
						{
							if (!String.IsNullOrWhiteSpace(s))
								groups.Add(Convert.ToInt32(s));
						});
					}
					if (!CTRS.Config.Xenforo.ContributorForumGroupIDs.Intersect(groups).Any())
					{
						return new AuthResult(LMReturnCode.UserNotAContributor);
					}

					Contributor contributor =
						await CTRS.Contributors.GetByXenforoIdAsync(Convert.ToInt32(dict["user_id"]));
					if (contributor == null)
					{
						/* Attempt to find contributor by user ID in the event a transaction
						 * was logged for an unexistant contributor account */
						contributor = await CTRS.Contributors.GetAsync(user.ID);

						bool success = false;
						if (contributor == null)
						{
							// Add a new contributor
							contributor = new Contributor(user);
							contributor.XenforoId = Convert.ToInt32(dict["user_id"]);
							success = await CTRS.Contributors.AddAsync(contributor);
						}
						else
						{
							// Set XenforoID for an existing contributor
							contributor.XenforoId = Convert.ToInt32(dict["user_id"]);
							success = await CTRS.Contributors.UpdateAsync(contributor, ContributorUpdates.XenforoID);
						}

						if (success)
							return new AuthResult(contributor);
						else
							return new AuthResult(LMReturnCode.DatabaseError);
					}
					else
					{
						// Check account limit
						if (CTRS.Config.AccountLimit > 0 && contributor.Accounts.Count >= CTRS.Config.AccountLimit)
						{
							return new AuthResult(LMReturnCode.AccountLimitReached);
						}

						if (await CTRS.Contributors.AddAccountAsync(contributor.Id, user.ID))
						{
							contributor.Accounts.Add(user.ID);
							return new AuthResult(contributor);
						}
						else
							return new AuthResult(LMReturnCode.DatabaseError);
					}
				}
			}
			else
				return new AuthResult((LMReturnCode)Convert.ToInt32((long)dict["error"]));
		}
	}

	public class Credentials
	{
		public string Username { get; set; }

		public string Password { get; set; }

		public Credentials(string username, string password)
		{
			Username = username;
			Password = password;
		}
	}
}
