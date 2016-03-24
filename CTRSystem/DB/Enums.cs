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
		XenforoID = 1,
		TotalCredits = 2,
		LastDonation = 4,
		LastAmount = 8,
		Tier = 16,
		ChatColor = 32,
		Notifications = 64,
		Settings = 128
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

	}
}
