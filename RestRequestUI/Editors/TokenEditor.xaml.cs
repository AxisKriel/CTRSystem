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
	/// Interaction logic for TokenEditor.xaml
	/// </summary>
	public partial class TokenEditor : Window
	{
		public string SetToken { get; set; }

		public TokenEditor()
		{
			InitializeComponent();
		}

		public TokenEditor(string oldToken) : this()
		{
			SetToken = oldToken;
			editBox.Text = oldToken;
			editBox.SelectAll();
		}

		private void okButton_Click(object sender, RoutedEventArgs e)
		{
			SetToken = editBox.Text;
			DialogResult = true;
			Close();
		}
	}
}
