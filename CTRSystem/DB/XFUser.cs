namespace CTRSystem.DB
{
	/// <summary>
	/// Contains data from a Xenforo User fetched from the database.
	/// </summary>
	public class XFUser
	{
		public int user_id;
		public string username;
		public float adcredit;

		public int UserID
		{
			get { return user_id; }
		}

		public string Username
		{
			get { return username; }
		}

		public float Credits
		{
			get { return adcredit; }
		}
	}
}
