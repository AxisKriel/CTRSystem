﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTRSystem.DB
{
	[Flags]
	public enum ContributorUpdates
	{
		TotalCredits,
		LastDonation,
		Tier,
		ChatColor,
		Notifications,
		Settings
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
