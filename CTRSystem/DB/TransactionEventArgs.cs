using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTRSystem.DB
{
	public class TransactionEventArgs : EventArgs
	{
		public int ContributorId { get; private set; }

		public float Credits { get; private set; }

		public DateTime? Date { get; private set; }

		/// <summary>
		/// May be set by a receiver to stop notifications from being set.
		/// </summary>
		public bool SuppressNotifications { get; set; }

		public TransactionEventArgs(int contributorId, float credits, DateTime? date)
		{
			ContributorId = contributorId;
			Credits = credits;
			Date = date;
		}
	}
}
