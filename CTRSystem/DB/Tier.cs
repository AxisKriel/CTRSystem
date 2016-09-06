using System;
using System.Collections.Generic;
using System.Linq;

namespace CTRSystem.DB
{
	public class Tier
	{
		public class DataModel
		{
			public int ID { get; set; }

			public string Name { get; set; }

			public float CreditsRequired { get; set; }

			public string ShortName { get; set; }

			public string ChatColor { get; set; }

			public string Permissions { get; set; }

			public float ExperienceMultiplier { get; set; }

			public static explicit operator Tier(DataModel model)
			{
				if (model == null)
					return null;

				return new Tier(model.ID)
				{
					Name = model.Name,
					CreditsRequired = model.CreditsRequired,
					ShortName = model.ShortName,
					ChatColor = Tools.ColorFromRGB(model.ChatColor),
					Permissions = model.Permissions.Split(',').ToList(),
					ExperienceMultiplier = model.ExperienceMultiplier
				};
			}

			public static explicit operator DataModel(Tier tier)
			{
				if (tier == null)
					return null;

				return new DataModel
				{
					ID = tier.ID,
					Name = tier.Name,
					CreditsRequired = tier.CreditsRequired,
					ShortName = tier.ShortName,
					ChatColor = Tools.ColorToRGB(tier.ChatColor),
					Permissions = String.Join(",", tier.Permissions),
					ExperienceMultiplier = tier.ExperienceMultiplier
				};
			}
		}

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

		/// <summary>
		/// Returns the database model version of this tier object.
		/// </summary>
		/// <returns>A <see cref="DataModel"/> containing all relevant information.</returns>
		public DataModel ToDataModel()
		{
			return (DataModel)this;
		}
	}
}
