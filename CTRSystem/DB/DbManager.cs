using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Dapper;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;

namespace CTRSystem.DB
{
	public enum StorageType
	{
		SQLite,
		MySQL
	}

	public abstract class DbManager
	{
		protected CTRS Main;

		/// <summary>
		/// Used to open the connection to the database. If not set, the default connection string will be used.
		/// </summary>
		protected string ConnectionString { get; }

		protected StorageType DbType => Main.Config.StorageType.Equals("mysql", StringComparison.OrdinalIgnoreCase)
			? StorageType.MySQL
			: StorageType.SQLite;

		public DbManager(CTRS main)
		{
			Main = main;
		}

		public DbManager(CTRS main, string connectionString) : this(main)
		{
			ConnectionString = connectionString;
		}

		/// <summary>
		/// Gets the identity ('ID') of the last inserted row from the database.
		/// </summary>
		/// <returns>An integer with the requested identity.</returns>
		protected int GetLastInsertId()
		{
			var sb = new StringBuilder("SELECT ");

			if (DbType == StorageType.MySQL)
				sb.Append("LAST_INSERT_ID()");
			else
				sb.Append("last_insert_rowid()");

			using (var db = OpenConnection())
			{
				return db.Query<int>(sb.ToString()).SingleOrDefault();
			}
		}

		/// <summary>
		/// Opens a connection to the CTRSystem database so that queries may be executed.
		/// </summary>
		/// <remarks>
		/// Best used with a <see cref="using"/> statement.
		/// This will automatically close the connection afterwards.</remarks>
		/// <returns>The connection that was just made.</returns>
		protected IDbConnection OpenConnection()
		{
			IDbConnection connection;

			if (Main.Config.StorageType.Equals("mysql", StringComparison.OrdinalIgnoreCase))
			{
				string[] host = Main.Config.MySqlHost.Split(':');
				connection = new MySqlConnection()
				{
					ConnectionString = ConnectionString ?? String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
					host[0],
					host.Length == 1 ? "3306" : host[1],
					Main.Config.MySqlDbName,
					Main.Config.MySqlUsername,
					Main.Config.MySqlPassword)
				};
			}
			else
			{
				connection = new SqliteConnection(ConnectionString ?? String.Format("uri=file://{0},Version=3",
					Path.Combine(TShock.SavePath, "CTRSystem", "CTRS-Data.sqlite")));
			}

			connection.Open();
			return connection;
		}

		/// <summary>
		/// Prepares an SQL query for execution by removing unnecessary whitespace characters.
		/// </summary>
		/// <param name="query">The query string that requires formatting.</param>
		/// <returns>A string for which each line has been trimmed appropriately.</returns>
		protected string PrepareSql(string query)
		{
			var sb = new StringBuilder();

			int indentation = 0;
			string s;

			foreach (var line in query.Split('\n'))
			{
				if (!String.IsNullOrWhiteSpace(line))
				{
					s = line.Trim();

					if (s[s.Length - 1] == ')')
						indentation--;

					for (int i = 0; i < indentation; i++)
						sb.Append('\t');

					sb.AppendLine(s);
					if (s[s.Length - 1] == '(')
						indentation++;
				}
			}

			return sb.ToString();
		}
	}
}
