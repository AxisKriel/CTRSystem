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
	/// Interaction logic for UpdateEditor.xaml
	/// </summary>
	public partial class UpdateEditor : Window
	{
		private string originalUrl;

		public static string DefaultUrl => "ctrs/update?token=$token";

		public string SetUrl { get; set; }

		public UpdateEditor()
		{
			InitializeComponent();
		}

		public UpdateEditor(string oldUrl) : this()
		{
			originalUrl = oldUrl;
			urlBox.Text = oldUrl;
		}

		private void resetButton_Click(object sender, RoutedEventArgs e)
		{
			urlBox.Text = DefaultUrl;
		}

		private void okButton_Click(object sender, RoutedEventArgs e)
		{
			SetUrl = urlBox.Text;
			DialogResult = true;
		}
	}
}
