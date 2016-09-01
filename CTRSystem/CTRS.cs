using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using CTRSystem.DB;
using CTRSystem.Extensions;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Rests;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;
using Config = CTRSystem.Configuration.ConfigFile;
using Texts = CTRSystem.Configuration.Texts;

namespace CTRSystem
{
	[ApiVersion(1, 23)]
	public class CTRS : TerrariaPlugin
	{
		private static Timer _tierUpdateTimer;

		public override string Author => "Enerdy";

		public override string Description => "Keeps track of server contributors and manages their privileges.";

		public override string Name => $"Contributions Track & Reward System ({SubVersion})";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public string SubVersion => "Welcome to the Multiverse";

		public CTRS(Main game) : base(game)
		{
			Order = 20001;
		}

		public static Config Config { get; private set; }

		public static IDbConnection Db { get; private set; }

		public static ContributorManager Contributors { get; private set; }

		public static LoginManager CredentialHelper { get; private set; }

		public static TierManager Tiers { get; private set; }

		public static Timer[] Timers { get; private set; }

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
				PlayerHooks.PlayerPostLogin -= OnLogin;
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				//ServerApi.Hooks.GameUpdate.Deregister(this, UpdateTiers);
				//ServerApi.Hooks.GameUpdate.Deregister(this, UpdateNotifications);
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);

				if (SEconomyPlugin.Instance != null)
				{
					SEconomyPlugin.SEconomyLoaded -= SEconomyLoaded;
					SEconomyPlugin.SEconomyUnloaded -= SEconomyUnloaded;
				}

				_tierUpdateTimer.Stop();
				_tierUpdateTimer.Elapsed -= UpdateTiers;

				for (int i = 0; i < Main.maxNetPlayers; i++)
				{
					if (Timers[i] != null && Timers[i].Enabled)
						Timers[i].Stop();
				}
			}
		}

		public override void Initialize()
		{
			//PlayerHooks.PlayerChat += OnChat;
			GeneralHooks.ReloadEvent += OnReload;
			PlayerHooks.PlayerLogout += OnLogout;
			PlayerHooks.PlayerPermission += OnPlayerPermission;
			PlayerHooks.PlayerPostLogin += OnLogin;
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			//ServerApi.Hooks.GameUpdate.Register(this, UpdateTiers);
			//ServerApi.Hooks.GameUpdate.Register(this, UpdateNotifications);
			ServerApi.Hooks.ServerChat.Register(this, OnChat);

			if (SEconomyPlugin.Instance != null)
			{
				SEconomyPlugin.SEconomyLoaded += SEconomyLoaded;
				SEconomyPlugin.SEconomyUnloaded += SEconomyUnloaded;

				// Initial hooking, as SEconomyLoaded has already been called
				// Disabling async until a way of making it work with the display is found
				// SEconomyPlugin.Instance.RunningJournal.BankTransactionPending += MultiplyExpAsync;
				SEconomyPlugin.Instance.RunningJournal.BankTransactionPending += MultiplyExp;
			}
		}

		void OnChat(ServerChatEventArgs e)
		{
			if (e.Handled)
				return;

			// A quick check to reduce DB work when this feature isn't in use
			if (String.IsNullOrWhiteSpace(Config.ContributorChatFormat))
				return;

			var player = TShock.Players[e.Who];
			if (player == null || !player.Active || !player.IsLoggedIn || !player.IsAuthenticated())
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

				// Why are we hardcoding tiers if we're then fetching it here by totalcredits every time? (no longer force-fetching)
				//Tier tier = Tiers.GetByCredits(con.TotalCredits);
				Tier tier = Tiers.Get(con.Tier);

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
					#region DEBUG
#if DEBUG
					TShock.Log.ConsoleInfo("OnChat: Contributor color was null");
#endif
					#endregion
					// If the contributor doesn't have a custom chat color, check their tier
					color = tier.ChatColor;
					if (!color.HasValue)
					{
						// As neither the tier nor the contributor have a custom chat color, use the group's default
						color = new Color(player.Group.R, player.Group.G, player.Group.B);
					}
				}
				TShock.Utils.Broadcast(text, color.Value.R, color.Value.G, color.Value.B);
				#region
#if DEBUG
				TShock.Log.ConsoleInfo("OnChat: Contributor was handled by CTRS");
#endif
				#endregion
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

			Add(new Command(Permissions.Admin, Commands.CAdmin, "cadmin")
			{
				HelpText = "Perform administrative actions across the Contributions Track & Reward System."
			});

			Add(new Command(Permissions.Commands, Commands.Contributions,
				new List<string>(Config.AdditionalCommandAliases) { "ctrs" }.ToArray())
			{
				HelpText = "Manages contributor settings. You must have contributed at least once before using this command."
			});

			Add(new Command(Permissions.Auth, Commands.Authenticate, "auth", "authenticate")
			{
				DoLog = false,
				HelpText = "Connects your Xenforo account to your TShock account. Enter your forum credentials OR generate an auth code first by visiting your user control panel."
			});

			TShock.RestApi.Register(new SecureRestCommand("/ctrs/transaction", Commands.RestNewTransaction, Permissions.RestTransaction));
			TShock.RestApi.Register(new SecureRestCommand("/ctrs/update", Commands.RestUpdateContributors, Permissions.RestTransaction));

			TShock.RestApi.Register(new SecureRestCommand("/ctrs/v2/transaction/{user_id}", Commands.RestNewTransactionV2, Permissions.RestTransaction));

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

			_tierUpdateTimer = new Timer(Config.TierRefreshMinutes * 60 * 1000);
			_tierUpdateTimer.Elapsed += UpdateTiers;
			_tierUpdateTimer.Start();

			Contributors = new ContributorManager(Db);
			CredentialHelper = new LoginManager();
			Tiers = new TierManager(Db);
			Timers = new Timer[Main.maxNetPlayers];
			XenforoUsers = new XenforoManager(xfdb);
		}

		async void OnLogin(PlayerPostLoginEventArgs e)
		{
			try
			{
				// Fetches the contributor from the database, updating the cache as needed
				Contributor con = await Contributors.GetAsync(e.Player.User.ID);
				// Timer Setup
				if (con != null)
				{
					e.Player.Authenticate();

					// Start the timer
					con.Initialize(e.Player.Index);
				}
			}
			catch
			{
				// Catch any exception that is thrown here to prevent pointless error messages
			}
		}

		void OnLogout(PlayerLogoutEventArgs e)
		{
			try
			{
				if (Timers[e.Player.Index] != null)
				{
					Timers[e.Player.Index].Stop();
					Timers[e.Player.Index] = null;
				}
				e.Player.Authenticate(true);
			}
			catch
			{
				// Catch any exception that is thrown here to prevent pointless error messages
			}

		}

		void OnPlayerPermission(PlayerPermissionEventArgs e)
		{
			// If the player isn't logged it, he's certainly not a contributor
			if (e.Player == null || !e.Player.IsLoggedIn || e.Player.User == null || !e.Player.IsAuthenticated())
				return;

			//Contributor con = await Contributors.GetAsync(e.Player.User.ID);
			Contributor con = Contributors.Get(e.Player.User.ID);
			if (con == null)
				return;

			#region DEBUG
#if DEBUG
			//TShock.Log.ConsoleInfo("[" + e.Player.Index + "] Checking permission for: " + e.Permission);
#endif
			#endregion

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

			if ((Config.TierRefreshMinutes * 60 * 1000) != _tierUpdateTimer.Interval)
			{
				_tierUpdateTimer.Interval = (Config.TierRefreshMinutes * 60 * 1000);
			}

			// Refetch all contributor data
			Task.Run(() => Contributors.LoadCache());

			// Refresh tier data
			Tiers.Refresh();
		}

		#region SEconomy

		void SEconomyLoaded(object sender, EventArgs e)
		{
			// Disabling async until a way of making it work with the display is found
			//SEconomyPlugin.Instance.RunningJournal.BankTransactionPending += MultiplyExpAsync;
			SEconomyPlugin.Instance.RunningJournal.BankTransactionPending += MultiplyExp;
		}

		void SEconomyUnloaded(object sender, EventArgs e)
		{
			// Disabling async until a way of making it work with the display is found
			//SEconomyPlugin.Instance.RunningJournal.BankTransactionPending -= MultiplyExpAsync;
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
					TShock.Log.ConsoleError($"CTRS: contributor {con.Accounts[0]} has an invalid tier ID! ({con.Tier})");
					return;
				}

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

				// Shouldn't have to do the db-check due to the on-login one
				//Contributor con = await Contributors.GetAsync(user.ID);
				Contributor con = Contributors.Get(user.ID);
				if (con == null)
					return;

				// Get the tier, find the experience multiplier
				Tier tier = await Tiers.GetAsync(con.Tier);
				if (tier == null)
				{
					TShock.Log.ConsoleError($"CTRS: contributor {con.Accounts[0]} has an invalid tier ID! ({con.Tier})");
					return;
				}

				// If TierUpdate is true, perform the update
				// TODO: Just like on Player Permission, might be better to scrap those and just do a generalized check every x seconds
				//await Tiers.UpgradeTier(con);

				if (tier.ExperienceMultiplier != 1f)
				{
					// Multiply the amount of currency gained by the experience multiplier
					e.Amount = new Money(Convert.ToInt64(e.Amount.Value * tier.ExperienceMultiplier));
				}
			}
		}

		#endregion

		internal static async Task UpdateNotifications(TSPlayer player, Contributor con)
		{
			//if ((DateTime.Now - lastNotification).TotalSeconds >= Config.NotificationCheckSeconds)
			//{
			//	lastNotification = DateTime.Now;
			//foreach (TSPlayer player in TShock.Players.Where(p => p != null && p.Active && p.IsLoggedIn && p.User != null))
			//{
			//Contributor con = await Contributors.GetAsync(player.User.ID);
			//Contributor con = Contributors.Get(player.User.ID);
			if (player == null || con == null)
				return;

			ContributorUpdates updates = 0;
			await Tiers.UpgradeTier(con);

			if ((con.Notifications & Notifications.Introduction) != Notifications.Introduction)
			{
				// Do Introduction message
				foreach (string s in Texts.SplitIntoLines(Config.Texts.FormatIntroduction(player, con)))
				{
					player.SendInfoMessage(s);
				}
				con.Notifications |= Notifications.Introduction;
				updates |= ContributorUpdates.Notifications;
			}
			else if ((con.Notifications & Notifications.NewDonation) == Notifications.NewDonation)
			{
				// Do NewDonation message
				foreach (string s in Texts.SplitIntoLines(Config.Texts.FormatNewDonation(player, con, con.LastAmount)))
				{
					player.SendInfoMessage(s);
				}
				con.Notifications ^= Notifications.NewDonation;
				updates |= ContributorUpdates.Notifications;
			}
			else if ((con.Notifications & Notifications.NewTier) == Notifications.NewTier)
			{
				// Do Tier Rank Up message
				foreach (string s in Texts.SplitIntoLines(Config.Texts.FormatNewTier(player, con, Tiers.Get(con.Tier))))
				{
					player.SendInfoMessage(s);
				}
				con.Notifications ^= Notifications.NewTier;
				updates |= ContributorUpdates.Notifications;
			}

			if (!await Contributors.UpdateAsync(con, updates) && Config.LogDatabaseErrors)
				TShock.Log.ConsoleError("CTRS-DB: something went wrong while updating a contributor's notifications.");
			//}
			//}
		}

		void UpdateTiers(object sender, ElapsedEventArgs e)
		{
			try
			{
				Tiers.Refresh();
				TShock.Log.ConsoleInfo("CTRS: Refreshed tiers.");
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError("CTRS: Exception thrown during tier refresh. Check logs for details.");
				TShock.Log.Error(ex.ToString());
			}
		}
	}
}
