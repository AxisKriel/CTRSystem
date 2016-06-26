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
using RestRequestUI.Extensions;
using Xceed.Wpf.Toolkit;
using RestRequestUI.Editors;

namespace RestRequestUI.Requests
{
	/// <summary>
	/// Interaction logic for TransactionWindow.xaml
	/// </summary>
	public partial class TransactionWindow : Window
	{
		private string ip;
		private int port;
		private string token;
		private string requestUri;

		public TransactionWindow()
		{
			InitializeComponent();
		}

		public TransactionWindow(string ip, int port, string token, string uri) : this()
		{
			this.ip = ip;
			this.port = port;
			this.token = token;
			requestUri = uri;
			
			// Enable/disable date panel accordingly
			datePanel.IsEnabled = TransactionEditor.ContainsDate(uri);
		}

		private void nextButton_Click(object sender, RoutedEventArgs e)
		{
			(App.ActiveWindow = new OutputWindow(RequestType.Transaction, App.CompleteRequest(ip, port, requestUri,
				userBox.Text, creditBox.Text, datePicker.Value?.ToUniversalTime().ToUnixTime().ToString(), token))).ShowDialog();
			Close();
		}
	}
}
