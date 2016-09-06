using System;

namespace CTRSystem.DB
{
	public delegate void TransactionHandler(TransactionEventArgs args);

	public class ContributorUpdateEventArgs : EventArgs
	{
		public int ContributorId { get; private set; }

		public ContributorUpdates Updates { get; private set; }

		public int XenforoId { get; private set; }

		public float TotalCredits { get; private set; }

		public DateTime? LastDonation { get; private set; }

		public float LastAmount { get; private set; }

		public int Tier { get; private set; }

		public Color? ChatColor { get; private set; }

		public Notifications Notifications { get; private set; }

		public Settings Settings { get; private set; }

		public ContributorUpdateEventArgs(Contributor contributor, ContributorUpdates updates)
		{
			ContributorId = contributor.Id;
			Updates = updates;

			if ((Updates & ContributorUpdates.XenforoID) == ContributorUpdates.XenforoID)
				XenforoId = contributor.XenforoId.Value;
			if ((Updates & ContributorUpdates.TotalCredits) == ContributorUpdates.TotalCredits)
				TotalCredits = contributor.TotalCredits;
			if ((Updates & ContributorUpdates.LastDonation) == ContributorUpdates.LastDonation)
				LastDonation = contributor.LastDonation;
			if ((Updates & ContributorUpdates.LastAmount) == ContributorUpdates.LastAmount)
				LastAmount = contributor.LastAmount;
			if ((Updates & ContributorUpdates.Tier) == ContributorUpdates.Tier)
				Tier = contributor.Tier;
			if ((Updates & ContributorUpdates.ChatColor) == ContributorUpdates.ChatColor)
				ChatColor = contributor.ChatColor;
			if ((Updates & ContributorUpdates.Notifications) == ContributorUpdates.Notifications)
				Notifications = contributor.Notifications;
			if ((Updates & ContributorUpdates.Settings) == ContributorUpdates.Settings)
				Settings = contributor.Settings;
		}
	}
}
