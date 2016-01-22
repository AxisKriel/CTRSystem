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
	}
}
