using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTRSystem.Configuration
{
	public class XenforoConfig
	{
		public string MySqlHost { get; set; } = "localhost:3306";
		
		public string MySqlDbName { get; set; } = "xenforo";

		public string MySqlUsername { get; set; } = "";

		public string MySqlPassword { get; set; } = "";

		public string XenAPIURI { get; set; } = "http://sbplanet.co/forums/api.php";

		public int[] ContributorForumGroupIDs { get; set; } = new[]
		{
			8/*		Silver Contributor	*/,
			9/*		Gold Contributor	*/,
			10/*	Platinum Contributor*/,
			5/*		Diamond Contributor*/,
			11/*	Stardust Contributor*/
		};
	}
}
