using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using System.Runtime.CompilerServices;

namespace CTRSystem.Extensions
{
	public static class TSPlayerExtensions
	{
		public static bool IsAuthenticated(this TSPlayer player)
		{
			if (!player.ContainsData(CTRSData.KEY))
				return false;

			return player.GetData<CTRSData>(CTRSData.KEY).IsAuthenticated;
		}

		public static void Authenticate(this TSPlayer player, bool logout = false)
		{
			// Possible future addition: Authenticate hook
			if (!player.ContainsData(CTRSData.KEY))
				player.SetData(CTRSData.KEY, new CTRSData(!logout));
			else
				player.GetData<CTRSData>(CTRSData.KEY).IsAuthenticated = !logout;
		}
	}

	public class CTRSData
	{
		public const string KEY  = "CTRSystem_Data";

		public bool IsAuthenticated { get; set; }

		public CTRSData()
		{

		}

		public CTRSData(bool authenticated) : this()
		{
			IsAuthenticated = authenticated;
		}
	}
}
