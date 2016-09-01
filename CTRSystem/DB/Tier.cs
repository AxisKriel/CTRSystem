using System.Collections.Generic;

namespace CTRSystem.DB
{
	public class Tier
	{
		public int ID { get; private set; }

		public string Name { get; set; }

		public float CreditsRequired { get; set; }

		public string ShortName { get; set; }

		public Color? ChatColor { get; set; }

		public List<string> Permissions { get; set; }

		public float ExperienceMultiplier { get; set; }

		public Tier(int id)
		{
			ID = id;
		}

		public override bool Equals(object obj)
		{
			return ((Tier)obj).ID == ID;
		}

		public override int GetHashCode()
		{
			return ID;
		}
	}
}
