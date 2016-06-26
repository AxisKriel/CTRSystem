using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RestRequestUI.Editors
{
	/// <summary>
	/// Interaction logic for TransactionEditor.xaml
	/// </summary>
	public partial class TransactionEditor : Window
	{
		private string originalUrl;
		
		public static string DefaultUrl => "ctrs/transaction?token=$token&user=$user&credits=$credits";

		public string SetUrl { get; set; }

		public TransactionEditor()
		{
			InitializeComponent();
		}

		public TransactionEditor(string oldUrl) : this()
		{
			originalUrl = oldUrl;
			urlBox.Text = oldUrl;
			dateCheck.IsChecked = ContainsDate(oldUrl);
		}

		public static bool ContainsDate(string url)
		{
			return url.Contains("$date");
		}

		string addDate(string url)
		{
			return url + "&date=$date";
		}

		string removeDate(string url)
		{
			return url.Replace("&date=$date", "");
		}

		private void resetButton_Click(object sender, RoutedEventArgs e)
		{
			urlBox.Text = DefaultUrl;
			dateCheck.IsChecked = ContainsDate(DefaultUrl);
		}

		private void okButton_Click(object sender, RoutedEventArgs e)
		{
			SetUrl = urlBox.Text;
			DialogResult = true;
		}

		private void dateCheck_Checked(object sender, RoutedEventArgs e)
		{
			if (!ContainsDate(urlBox.Text))
			{
				urlBox.Text = addDate(urlBox.Text);
			}
		}

		private void dateCheck_Unchecked(object sender, RoutedEventArgs e)
		{
			if (ContainsDate(urlBox.Text))
			{
				urlBox.Text = removeDate(urlBox.Text);
			}
		}
	}
}
