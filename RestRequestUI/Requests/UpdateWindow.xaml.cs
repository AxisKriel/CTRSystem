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

namespace RestRequestUI.Requests
{
	/// <summary>
	/// Interaction logic for UpdateWindow.xaml
	/// </summary>
	public partial class UpdateWindow : Window
	{
		private string ip;
		private int port;
		private string token;
		private string requestUri;

		public UpdateWindow()
		{
			InitializeComponent();
		}

		public UpdateWindow(string ip, int port, string token, string uri) : this()
		{
			this.ip = ip;
			this.port = port;
			this.token = token;
			requestUri = uri;
		}

		private void nextButton_Click(object sender, RoutedEventArgs e)
		{
			(App.ActiveWindow = new OutputWindow(RequestType.Update, App.CompleteRequest(ip, port, requestUri, token: token))).ShowDialog();
			Close();
		}
	}
}
