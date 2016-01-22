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
		private static ConditionalWeakTable<TSPlayer, TSData> data = new ConditionalWeakTable<TSPlayer, TSData>();

		public static bool? IsContributor(this TSPlayer player)
		{
			return data.GetOrCreateValue(player).Contributor;
		}

		public static void SetContributor(this TSPlayer player, bool value)
		{
			data.GetOrCreateValue(player).Contributor = value;
		}

		public static void WipeData()
		{
			data = new ConditionalWeakTable<TSPlayer, TSData>();
		}
	}

	public class TSData
	{
		public bool? Contributor { get; set; } = null;
	}
}
