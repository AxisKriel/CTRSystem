using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CTRSystem.DB;
using CTRSystem.Extensions;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;
using Config = CTRSystem.Configuration.ConfigFile;
using Texts = CTRSystem.Configuration.Texts;

/*
TODO LIST:
	Properly implement synchronous permissions (half-done, check if it works)							[x]
	Figure out why chat still doesn't work																[ ]
	Finish base Texts (Introduction, info) and test Info command										[ ]
	...
*/

namespace CTRSystem
{
	[ApiVersion(1, 22)]
	public class CTRS : TerrariaPlugin
	{
		private static DateTime lastRefresh;
		private static DateTime lastNotification;

		public override string Author
		{
			get { return "Enerdy"; }
		}

		public override string Description
		{
			get { return "Keeps track of server contributors and manages their privileges."; }
		}

		public override string Name
		{
			get { return "Contributions Track & Reward System"; }
		}

		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public CTRS(Main game) : base(game)
		{
			lastRefresh = DateTime.Now;
			lastNotification = DateTime.Now;
			Order = 20001;
		}

		public static Config Config { get; private set; }

		public static IDbConnection Db { get; private set; }

		public static ContributorManager Contributors { get; private set; }

		public static LoginManager CredentialHelper { get; private set; }

		public static TierManager Tiers { get; private set; }

		public static XenforoManager XenforoUsers { get; private set; }

		public static Version PublicVersion
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				//PlayerHooks.PlayerChat -= OnChat;
				GeneralHooks.ReloadEvent -= OnReload;
				PlayerHooks.PlayerLogout -= OnLogout;
				PlayerHooks.PlayerPermission -= OnPlayerPermission;
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.GameUpdate.Deregister(this, UpdateTiers);
				ServerApi.Hooks.GameUpdate.Deregister(this, UpdateNotifications);
				//ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
				ServerApi.Hooks.ServerChat.Deregister(this, OnChatSync);

				if (SEconomyPlugin.Instance != null)
				{
					SEconomyPlugin.SEconomyLoaded -= SEconomyLoaded;
					SEconomyPlugin.SEconomyUnloaded -= SEconomyUnloaded;
				}
			}
		}

		public override void Initialize()
		{
			//PlayerHooks.PlayerChat += OnChat;
			GeneralHooks.ReloadEvent += OnReload;
			PlayerHooks.PlayerLogout += OnLogout;
			PlayerHooks.PlayerPermission += OnPlayerPermission;
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.GameUpdate.Register(this, UpdateTiers);
			ServerApi.Hooks.GameUpdate.Register(this, UpdateNotifications);
			//ServerApi.Hooks.ServerChat.Register(this, OnChat);
			ServerApi.Hooks.ServerChat.Register(this, OnChatSync);

			if (SEconomyPlugin.Instance != null)
			{
				SEconomyPlugin.SEconomyLoaded += SEconomyLoaded;
				SEconomyPlugin.SEconomyUnloaded += SEconomyUnloaded;

				// Initial hooking, as SEconomyLoaded has already been called
				SEconomyPlugin.Instance.RunningJournal.BankTransactionPending += MultiplyExp;
			}
		}

		async void OnChat(ServerChatEventArgs e)
		{
			if (e.Handled)
				return;

			// A quick check to reduce DB work when this feature isn't in use
			if (String.IsNullOrWhiteSpace(Config.ContributorChatFormat) || Config.ContributorChatFormat == TShock.Config.ChatFormat)
				return;

			var player = TShock.Players[e.Who];
			if (player == null)
				return;

			if (e.Text.Length > 500)
				return;

			// If true, the message is a command, so we skip it
			if ((e.Text.StartsWith(TShockAPI.Commands.Specifier) || e.Text.StartsWith(TShockAPI.Commands.SilentSpecifier))
				&& !String.IsNullOrWhiteSpace(e.Text.Substring(1)))
				return;

			// Player needs to be able to talk, not be muted, and must be logged in
			if (!player.HasPermission(TShockAPI.Permissions.canchat) || player.mute || !player.IsLoggedIn)
				return;

			// At this point, ChatAboveHeads is not supported, but it could be a thing in the future
			if (!TShock.Config.EnableChatAboveHeads)
			{
				Contributor con = await Contributors.GetAsync(player.User.ID);
				if (con == null)
					return;

				Tier tier;
				try
				{
					tier = await Tiers.GetByCreditsAsync(con.TotalCredits);
				}
				catch (TierManager.TierNotFoundException)
				{
#if DEBUG
					TShock.Log.ConsoleError("OnChat: Tier fetching didn't return any tier");
#endif
					return;
				}

				/* Contributor chat format:
					{0} - group name
					{1} - group prefix
					{2} - player name
					{3} - group suffix
					{4} - message text
					{5} - tier shortname
					{6} - tier name
					{7} - webID

				 */
				var text = String.Format(Config.ContributorChatFormat, player.Group.Name, player.Group.Prefix, player.Name,
					player.Group.Suffix, e.Text, tier.ShortName ?? "", tier.Name ?? "", con.XenforoID ?? -1);
				PlayerHooks.OnPlayerChat(player, e.Text, ref text);
				Color? color = con.ChatColor;
				if (!color.HasValue)
				{
#if DEBUG
					TShock.Log.ConsoleInfo("OnChat: Color was null");
#endif
					color = new Color(player.Group.R, player.Group.G, player.Group.B);
				}
				TShock.Utils.Broadcast(text, color.Value.R, color.Value.G, color.Value.B);
#if DEBUG
				TShock.Log.ConsoleInfo("OnChat: Contributor was handled by CTRS");
#endif
				e.Handled = true;
			}
		}

		void OnChatSync(ServerChatEventArgs e)
		{
			if (e.Handled)
				return;

			// A quick check to reduce DB work when this feature isn't in use
			if (String.IsNullOrWhiteSpace(Config.ContributorChatFormat) || Config.ContributorChatFormat == TShock.Config.ChatFormat)
				return;

			var player = TShock.Players[e.Who];
			if (player == null)
				return;

			if (e.Text.Length > 500)
				return;

			// If true, the message is a command, so we skip it
			if ((e.Text.StartsWith(TShockAPI.Commands.Specifier) || e.Text.StartsWith(TShockAPI.Commands.SilentSpecifier))
				&& !String.IsNullOrWhiteSpace(e.Text.Substring(1)))
				return;

			// Player needs to be able to talk, not be muted, and must be logged in
			if (!player.HasPermission(TShockAPI.Permissions.canchat) || player.mute || !player.IsLoggedIn)
				return;

			// At this point, ChatAboveHeads is not supported, but it could be a thing in the future
			if (!TShock.Config.EnableChatAboveHeads)
			{
				Contributor con = Contributors.Get(player.User.ID);
				if (con == null)
					return;

				// Why are we hardcoding tiers if we're then fetching it here by totalcredits every time?
				Tier tier = Tiers.GetByCredits(con.TotalCredits);

				/* Contributor chat format:
					{0} - group name
					{1} - group prefix
					{2} - player name
					{3} - group suffix
					{4} - message text
					{5} - tier shortname
					{6} - tier name
					{7} - webID

				 */
				var text = String.Format(Config.ContributorChatFormat, player.Group.Name, player.Group.Prefix, player.Name,
					player.Group.Suffix, e.Text, tier.ShortName ?? "", tier.Name ?? "", con.XenforoID ?? -1);
				PlayerHooks.OnPlayerChat(player, e.Text, ref text);
				Color? color = con.ChatColor;
				if (!color.HasValue)
				{
#if DEBUG
					TShock.Log.ConsoleInfo("OnChat: Color was null");
#endif
					color = new Color(player.Group.R, player.Group.G, player.Group.B);
				}
				TShock.Utils.Broadcast(text, color.Value.R, color.Value.G, color.Value.B);
#if DEBUG
				TShock.Log.ConsoleInfo("OnChat: Contributor was handled by CTRS");
#endif
				e.Handled = true;
			}
		}

		void OnInitialize(EventArgs e)
		{
			#region Config

			string path = Path.Combine(TShock.SavePath, "CTRSystem", "CTRS-Config.json");
			Config = Config.Read(path);

			#endregion

			#region Commands

			Action<Command> Add = c =>
			{
				TShockAPI.Commands.ChatCommands.RemoveAll(c2 => c2.Names.Exists(s => c.Names.Contains(s)));
				TShockAPI.Commands.ChatCommands.Add(c);
			};

			Add(new Command(Permissions.Commands, Commands.Contributions,
				(new List<string>(Config.AdditionalCommandAliases) { "ctrs" }).ToArray())
			{
				HelpText = "Manages contributor settings. You must have contributed at least once before using this command."
			});

			Add(new Command(Permissions.Auth, Commands.Authenticate, "auth", "authenticate")
			{
				DoLog = false,
				HelpText = "Connects your Xenforo account to your TShock account. Generate an auth code first by visiting your user control panel."
			});

			#endregion

			#region DB

			if (Config.StorageType.Equals("mysql", StringComparison.OrdinalIgnoreCase))
			{
				string[] host = Config.MySqlHost.Split(':');
				Db = new MySqlConnection()
				{
					ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
					host[0],
					host.Length == 1 ? "3306" : host[1],
					Config.MySqlDbName,
					Config.MySqlUsername,
					Config.MySqlPassword)
				};
			}
			else if (Config.StorageType.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
				Db = new SqliteConnection(String.Format("uri=file://{0},Version=3",
					Path.Combine(TShock.SavePath, "CTRSystem", "CTRS-Data.sqlite")));
			else
				throw new InvalidOperationException("Invalid storage type!");

			string[] _host = Config.Xenforo.MySqlHost.Split(':');
			var xfdb = new MySqlConnection()
			{
				ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
					_host[0],
					_host.Length == 1 ? "3306" : _host[1],
					Config.Xenforo.MySqlDbName,
					Config.Xenforo.MySqlUsername,
					Config.Xenforo.MySqlPassword)
			};

			#endregion

			Contributors = new ContributorManager(Db);
			CredentialHelper = new LoginManager();
			Tiers = new TierManager(Db);
			XenforoUsers = new XenforoManager(xfdb);
		}

		void OnLogout(PlayerLogoutEventArgs e)
		{
			try
			{
				// Set the sync variable to false so that changes applied while the user was off can be applied
				Contributors.SetSync(e.Player.User.ID, false);
			}
			catch
			{
				// Catch any exception that is thrown here to prevent pointless error messages
			}

		}

		void OnPlayerPermission(PlayerPermissionEventArgs e)
		{
			// If the player isn't logged it, he's certainly not a contributor
			if (!e.Player.IsLoggedIn || e.Player.User == null)
				return;

			//Contributor con = await Contributors.GetAsync(e.Player.User.ID);
			Contributor con = Contributors.Get(e.Player.User.ID);
			if (con == null)
				return;

			// Check if a Tier Update is pending, and if it is, perform it before anything else
			// NOTE: If this occurs, there is a good chance that a delay will be noticeable
			//await Tiers.UpgradeTier(con);
			Task.Run(() => Tiers.UpgradeTier(con));

			//Tier tier = await Tiers.GetAsync(con.Tier);
			Tier tier = Tiers.Get(con.Tier);
			e.Handled = tier.Permissions.HasPermission(e.Permission);
			#region DEBUG
#if DEBUG
			if (e.Handled)
				TShock.Log.ConsoleInfo("OnPlayerPermission: Contributor had the permission");
#endif
			#endregion
		}

		void OnReload(ReloadEventArgs e)
		{
			string path = Path.Combine(TShock.SavePath, "CTRSystem", "CTRS-Config.json");
			Config = Config.Read(path);

			// Refetch all contributor data
			Task.Run(() => Contributors.LoadCache());

			// Refresh tier data
			Tiers.Refresh();
		}

		#region SEconomy

		void SEconomyLoaded(object sender, EventArgs e)
		{
			SEconomyPlugin.Instance.RunningJournal.BankTransactionPending += MultiplyExp;
		}

		void SEconomyUnloaded(object sender, EventArgs e)
		{
			SEconomyPlugin.Instance.RunningJournal.BankTransactionPending -= MultiplyExp;
		}

		void MultiplyExp(object sender, PendingTransactionEventArgs e)
		{
			// Should only work with system accounts as this will also make the sender pay more currency
			if (SEconomyPlugin.Instance != null && e.ToAccount != null && e.FromAccount != null && e.FromAccount.IsSystemAccount)
			{
				// Find the tshock user
				var user = TShock.Users.GetUserByName(e.ToAccount.UserAccountName);
				if (user == null)
					return;

				Contributor con = Contributors.Get(user.ID);
				if (con == null)
					return;

				// Get the tier, find the experience multiplier
				Tier tier = Tiers.Get(con.Tier);
				if (tier == null)
				{
					TShock.Log.ConsoleError($"CTRS: contributor {con.UserID.Value} has an invalid tier ID! ({con.Tier})");
					return;
				}

				// If TierUpdate is true, perform the update
				Task.Run(() => Tiers.UpgradeTier(con));

				if (tier.ExperienceMultiplier != 1f)
				{
					// Multiply the amount of currency gained by the experience multiplier
					e.Amount = new Money(Convert.ToInt64(e.Amount.Value * tier.ExperienceMultiplier));
				}
			}
		}

		async void MultiplyExpAsync(object sender, PendingTransactionEventArgs e)
		{
			// Should only work with system accounts as this will also make the sender pay more currency
			if (SEconomyPlugin.Instance != null && e.ToAccount != null && e.FromAccount != null && e.FromAccount.IsSystemAccount)
			{
				// Find the tshock user
				var user = TShock.Users.GetUserByName(e.ToAccount.UserAccountName);
				if (user == null)
					return;

				Contributor con = await Contributors.GetAsync(user.ID);
				if (con == null)
					return;

				// Get the tier, find the experience multiplier
				Tier tier = await Tiers.GetAsync(con.Tier);
				if (tier == null)
				{
					TShock.Log.ConsoleError($"CTRS: contributor {con.UserID.Value} has an invalid tier ID! ({con.Tier})");
					return;
				}

				// If TierUpdate is true, perform the update
				await Tiers.UpgradeTier(con);

				if (tier.ExperienceMultiplier != 1f)
				{
					// Multiply the amount of currency gained by the experience multiplier
					e.Amount = new Money(Convert.ToInt64(e.Amount.Value * tier.ExperienceMultiplier));
				}
			}
		}

		#endregion

		async void UpdateNotifications(EventArgs e)
		{
			if ((DateTime.Now - lastNotification).TotalSeconds >= Config.NotificationCheckSeconds)
			{
				lastNotification = DateTime.Now;
				foreach (TSPlayer player in TShock.Players.Where(p => p != null && p.Active && p.IsLoggedIn && p.User != null))
				{
					Contributor con = await Contributors.GetAsync(player.User.ID);
					if (con == null)
						return;

					ContributorUpdates updates = 0;
					await Tiers.UpgradeTier(con);

					if ((con.Notifications & Notifications.Introduction) != Notifications.Introduction)
					{
						// Do Introduction message
						foreach (string s in Texts.SplitIntoLines(Config.Texts.FormatIntroduction(player)))
						{
							player.SendInfoMessage(s);
						}
						con.Notifications |= Notifications.Introduction;
						updates |= ContributorUpdates.Notifications;
					}
					else if ((con.Notifications & Notifications.NewDonation) == Notifications.NewDonation)
					{
						// Do NewDonation message
						foreach (string s in Texts.SplitIntoLines(Config.Texts.FormatNewDonation(player)))
						{
							player.SendInfoMessage(s);
						}
						con.Notifications ^= Notifications.NewDonation;
						updates |= ContributorUpdates.Notifications;
					}
					else if ((con.Notifications & Notifications.NewTier) == Notifications.NewTier)
					{
						// Do Tier Rank Up message
						foreach (string s in Texts.SplitIntoLines(Config.Texts.FormatNewTier(player)))
						{
							player.SendInfoMessage(s);
						}
						con.Notifications ^= Notifications.NewTier;
						updates |= ContributorUpdates.Notifications;
					}

					if (!await Contributors.UpdateAsync(con, updates) && Config.LogDatabaseErrors)
						TShock.Log.ConsoleError("CTRS-DB: something went wrong while updating a contributor's notifications.");
				}
			}
		}

		void UpdateTiers(EventArgs e)
		{
			if ((DateTime.Now - lastRefresh).TotalMinutes >= Config.TierRefreshMinutes)
			{
				lastRefresh = DateTime.Now;
				Tiers.Refresh();
			}
		}
	}
}
