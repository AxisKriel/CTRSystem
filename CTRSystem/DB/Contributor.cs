using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTRSystem.DB
{
	public class Contributor
	{
		public int? UserID { get; set; }

		public int? WebID { get; set; }

		public int Credits { get; set; }

		public int TotalCredits { get; set; }

		/// <summary>
		/// The date of the last donation made by the contributor.
		/// Always convert to string in the sortable ("s") DateTime format.
		/// </summary>
		public DateTime LastDonation { get; set; }

		public int Tier { get; set; }

		public Color? ChatColor { get; set; }

		public Notifications Notifications { get; set; }

		public Settings Settings { get; set; }

		public Contributor(int? userID)
		{
			UserID = userID;
		}

		public Contributor(int userID, int credits) : this(userID)
		{
			Credits = credits;
		}
	}
}
