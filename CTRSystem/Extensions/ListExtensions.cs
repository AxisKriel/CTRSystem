using System;
using System.Collections.Generic;

namespace CTRSystem.Extensions
{
	public static class ListExtensions
	{
		public static bool HasPermission(this List<string> list, string permission)
		{
			if (String.IsNullOrWhiteSpace(permission) || list.Contains(permission))
				return true;

			string[] nodes = permission.Split('.');
			for (int i = nodes.Length - 1; i >= 0; i--)
			{
				nodes[i] = "*";
				if (list.Contains(String.Join(".", nodes, 0, i + 1)))
					return true;
			}
			return false;
		}
	}
}
