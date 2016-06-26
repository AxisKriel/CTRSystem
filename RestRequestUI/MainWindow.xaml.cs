using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using RestRequestUI.Editors;
using RestRequestUI.Requests;

namespace RestRequestUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private CancellationTokenSource _canceller;
		private SolidColorBrush defaultBrush;
		private FocusLock _lock;

		public string serverIp
		{
			get { return labelServerIp.Content.ToString(); }
			set { labelServerIp.Content = value; }
		}

		public string token
		{
			get { return editToken_button.Content.ToString(); }
			set { editToken_button.Content = value; }
		}

		public string transactionUri
		{
			get { return editTransactionUri_button.Content.ToString(); }
			set { editTransactionUri_button.Content = value; }
		}

		public int transactionPort
		{
			get { return Int32.Parse(transactionPort_text.Text); }
			set { transactionPort_text.Text = value.ToString(); }
		}

		public string updateUri
		{
			get { return editUpdateUri_button.Content.ToString(); }
			set { editUpdateUri_button.Content = value; }
		}

		public int updatePort
		{
			get { return Int32.Parse(updatePort_text.Text); }
			set { updatePort_text.Text = value.ToString(); }
		}

		public MainWindow()
		{
			InitializeComponent();
			App.ActiveWindow = this;

			// Versioning
			Title += $" v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";

			// Just because this is useful
			defaultBrush = (SolidColorBrush)checkOnlineStatus_button.Foreground;

			// Set defaults
			serverIp = "sbplanet.co";
			token = "ctrest";

			transactionUri = TransactionEditor.DefaultUrl;
			transactionPort = 7878;

			updateUri = UpdateEditor.DefaultUrl;
			updatePort = 7879;
		}

		private void transactionStart_button_Click(object sender, RoutedEventArgs e)
		{
			using (_lock = new FocusLock(this))
				(App.ActiveWindow = new TransactionWindow(serverIp, transactionPort, token, transactionUri)).ShowDialog();
		}

		private void updateStart_button_Click(object sender, RoutedEventArgs e)
		{
			using (_lock = new FocusLock(this))
				(App.ActiveWindow = new UpdateWindow(serverIp, updatePort, token, updateUri)).ShowDialog();
		}

		private async void checkOnlineStatus_button_Click(object sender, RoutedEventArgs e)
		{
			// Begin server status check
			checkOnlineStatus_button.Foreground = defaultBrush;
			checkOnlineStatus_button.Content = "Checking...";
			string request = "/v2/server/status?token=$token";
			WebClient client = new WebClient();
			_canceller = new CancellationTokenSource(1000 * 10/* timeout: 10s */);
			string ip = serverIp;
			// This needs work as I should probably ask for the port itself instead of using the one from transaction
			int port = transactionPort;
			string token = this.token;
			bool online = false;
			try
			{
				online = await Task.Run(async () =>
				{
					string response = await client.DownloadStringTaskAsync(App.CompleteRequest(ip, port, request, token: token));
					if (!String.IsNullOrWhiteSpace(response))
					{
						var parsedResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
						if ((string)parsedResponse["status"] == "200")
						{
							return true;
						}
					}
					return false;
				}, _canceller.Token);
			}
			catch
			{
				// Continue below
			}

			if (online)
			{
				checkOnlineStatus_button.Foreground = new SolidColorBrush(Colors.Green);
				checkOnlineStatus_button.Content = "online";
			}
			else
			{
				checkOnlineStatus_button.Foreground = new SolidColorBrush(Colors.Red);
				checkOnlineStatus_button.Content = "offline";
			}
		}

		private void editToken_button_Click(object sender, RoutedEventArgs e)
		{
			var edit = new TokenEditor(token);
			using (_lock = new FocusLock(this))
				if ((App.ActiveWindow = edit).ShowDialog() == true)
					token = edit.SetToken;
		}

		private void editTransactionUri_button_Click(object sender, RoutedEventArgs e)
		{
			var edit = new TransactionEditor(transactionUri);
			using (_lock = new FocusLock(this))
				if ((App.ActiveWindow = edit).ShowDialog() == true)
					transactionUri = edit.SetUrl;
		}

		private void editUpdateUri_button_Click(object sender, RoutedEventArgs e)
		{
			var edit = new UpdateEditor(updateUri);
			using (_lock = new FocusLock(this))
				if ((App.ActiveWindow = edit).ShowDialog() == true)
					updateUri = edit.SetUrl;
		}
	}

	public class FocusLock : IDisposable
	{
		public Window Window { get; private set; }

		public FocusLock(Window window)
		{
			Window = window;

			Window.IsEnabled = false;
			Window.Focusable = false;
		}

		public void Dispose()
		{
			Window.IsEnabled = true;
			Window.Focusable = true;

			// Regain master focus
			App.ActiveWindow = Window;

			// Drop the attached window object
			Window = null;
		}
	}
}
