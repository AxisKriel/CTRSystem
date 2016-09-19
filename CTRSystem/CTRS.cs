using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using CTRSystem.Configuration;
using CTRSystem.DB;
using CTRSystem.Extensions;
using DiscordBridge.Chat;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;

namespace CTRSystem
{
	[ApiVersion(1, 23)]
	public class CTRS : TerrariaPlugin
	{
		private Timer _tierUpdateTimer;

		public override string Author => "Enerdy";

		public override string Description => "Keeps track of server contributors and manages their privileges.";

		public override string Name => $"Contributions Track & Reward System";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public CTRS(Main game) : base(game)
		{
			Order = 20001;
		}

		public Configuration.ConfigFile Config { get; protected internal set; }

		public CommandManager Commands { get; protected internal set; }

		public ContributorManager Contributors { get; protected internal set; }

		public LoginManager CredentialHelper { get; protected internal set; }

		public RestManager Rests { get; protected internal set; }

		public TextFormatter Formatter { get; protected internal set; }

		public TierManager Tiers { get; protected internal set; }

		public XenforoManager XenforoUsers { get; protected internal set; }

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ChatHandler.PlayerChatting -= OnChat;
				GeneralHooks.ReloadEvent -= OnReload;
				PlayerHooks.PlayerLogout -= OnLogout;
				PlayerHooks.PlayerPermission -= OnPlayerPermission;
				PlayerHooks.PlayerPostLogin -= OnLogin;
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);

				if (SEconomyPlugin.Instance != null)
				{
					SEconomyPlugin.SEconomyLoaded -= SEconomyLoaded;
					SEconomyPlugin.SEconomyUnloaded -= SEconomyUnloaded;
				}

				_tierUpdateTimer.Stop();
				_tierUpdateTimer.Elapsed -= UpdateTiers;
			}
		}

		public override void Initialize()
		{
			ChatHandler.PlayerChatting += OnChat;
			GeneralHooks.ReloadEvent += OnReload;
			PlayerHooks.PlayerPermission += OnPlayerPermission;
			PlayerHooks.PlayerPostLogin += OnLogin;
			PlayerHooks.PlayerLogout += OnLogout;
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);

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

		void OnChat(object sender, PlayerChattingEventArgs e)
		{
			if (!e.Player.IsLoggedIn || !e.Player.ContainsData(Contributor.DataKey))
				return;

			// At this point, ChatAboveHeads is not supported, but it could be a thing in the future
			if (!TShock.Config.EnableChatAboveHeads)
			{
				Contributor con = e.Player.GetData<Contributor>(Contributor.DataKey);
				Tier tier = Tiers.Get(con.Tier);

				e.ChatFormatters.Add("contributorTier", String.Format(Config.PrefixFormat, tier.Name ?? ""));
				e.ChatFormatters.Add("contributorTierShort", String.Format(Config.PrefixFormat, tier.ShortName ?? ""));
				e.ChatFormatters.Add("contributorId", (con.XenforoId ?? -1).ToString());

				Color? color = con.ChatColor;
				if (!color.HasValue)
				{
					// If the contributor doesn't have a custom chat color, check their tier
					color = tier.ChatColor;
					if (!color.HasValue)
					{
						// As neither the tier nor the contributor have a custom chat color, use the group's default
						color = new Color(e.Player.Group.R, e.Player.Group.G, e.Player.Group.B);
					}
				}

				e.ColorFormatters.Add("Contributor", color);
			}
		}

		void OnInitialize(EventArgs e)
		{
			#region Config

			string path = Path.Combine(TShock.SavePath, "CTRSystem", "CTRS-Config.json");
			Config = Configuration.ConfigFile.Read(path);
			Formatter = new TextFormatter(this, Config);

			#endregion

			#region Commands

			Commands = new CommandManager(this);

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

			#endregion

			_tierUpdateTimer = new Timer(Config.TierRefreshMinutes * 60 * 1000);
			_tierUpdateTimer.Elapsed += UpdateTiers;
			_tierUpdateTimer.Start();

			Contributors = new ContributorManager(this);
			CredentialHelper = new LoginManager(this);
			Rests = new RestManager(this);
			Tiers = new TierManager(this);
			XenforoUsers = new XenforoManager(this);
		}

		async void OnLogin(PlayerPostLoginEventArgs e)
		{
			try
			{
				Contributor con = await Contributors.GetAsync(e.Player.User.ID);

				if (con != null)
				{
					// Start listening to events
					con.Listen(this, e.Player);

					// Store the contributor object
					e.Player.SetData(Contributor.DataKey, con);

					await con.UpdateNotifications();
				}
			}
			catch
			{
				// Catch any exception that is thrown here to prevent pointless error messages
			}
		}

		void OnLogout(PlayerLogoutEventArgs e)
		{
			if (e.Player.ContainsData(Contributor.DataKey))
			{
				// Remove the stored contributor object and stop listening to events
				// Note: TSPlayer.RemoveData(string) returns the removed object
				((Contributor)e.Player.RemoveData(Contributor.DataKey)).Unlisten();
			}
		}

		void OnPlayerPermission(PlayerPermissionEventArgs e)
		{
			// If the player isn't logged it, he's certainly not a contributor
			if (e.Player == null || !e.Player.IsLoggedIn || !e.Player.ContainsData(Contributor.DataKey))
				return;

			Contributor con = e.Player.GetData<Contributor>(Contributor.DataKey);
			#region DEBUG
#if DEBUG
			//TShock.Log.ConsoleInfo("[" + e.Player.Index + "] Checking permission for: " + e.Permission);
#endif
			#endregion

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
			Config = Configuration.ConfigFile.Read(path);

			if ((Config.TierRefreshMinutes * 60 * 1000) != _tierUpdateTimer.Interval)
			{
				_tierUpdateTimer.Interval = (Config.TierRefreshMinutes * 60 * 1000);
			}

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
			if (SEconomyPlugin.Instance != null
				&& e.ToAccount != null
				&& e.FromAccount != null
				&& e.FromAccount.IsSystemAccount
				&& (!e.Options.HasFlag(BankAccountTransferOptions.SuppressDefaultAnnounceMessages)))
			{
				// Find the player (note: could be troublesome if multiple login is enabled)
				var player = (from p in TShock.Players
							  where p.IsLoggedIn && p.User.Name == e.ToAccount.UserAccountName
							  && p.ContainsData(Contributor.DataKey)
							  select p).FirstOrDefault();

				if (player == null)
					return;

				Contributor con = player.GetData<Contributor>(Contributor.DataKey);

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

		#endregion

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
