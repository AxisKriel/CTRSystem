using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTRSystem.DB
{
	[Flags]
	public enum ContributorUpdates
	{
		None = 0,
		Accounts = 1,
		XenforoID = 2,
		TotalCredits = 4,
		LastDonation = 8,
		LastAmount = 16,
		Tier = 32,
		ChatColor = 64,
		Notifications = 128,
		Settings = 256
	}

	[Flags]
	public enum Notifications
	{
		Introduction = 1,
		TierUpdate = 2,
		NewDonation = 4,
		NewTier = 8
	}

	[Flags]
	public enum Settings
	{
		CanChangeColor = 1
	}
}
