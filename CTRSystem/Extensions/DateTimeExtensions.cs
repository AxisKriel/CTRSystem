using System;

namespace CTRSystem.Extensions
{
	public static class DateTimeExtensions
	{
		public static long ToUnixTime(this DateTime date)
		{
			DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return Convert.ToInt64((date.ToUniversalTime() - epoch).TotalSeconds);
		}
	}
}
