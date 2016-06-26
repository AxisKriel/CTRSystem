using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using Newtonsoft.Json;
using System.Threading;

namespace RestRequestUI
{
	public enum RequestType
	{
		Transaction = 1,
		Update = 2
	}

	/// <summary>
	/// Interaction logic for SendWindow.xaml
	/// </summary>
	public partial class OutputWindow : Window
	{
		public CancellationTokenSource _canceller;

		public MultilineTextBox Output { get; set; }

		public bool Resolved
		{
			get { return Dispatcher.Invoke(() => exitButton.Content.ToString().Equals("Exit", StringComparison.OrdinalIgnoreCase)); }
			set { Dispatcher.Invoke(() => exitButton.Content = value ? "Exit" : "Stop"); }
		}

		public OutputWindow()
		{
			InitializeComponent();

			Output = new MultilineTextBox();
			Output.LinesChanged += onLinesChanged;
		}

		public OutputWindow(RequestType request, string uri) : this()
		{
			// Will do this here to make it easier to write to the output window
			_canceller = new CancellationTokenSource(1000 * 10/* timeout = 10s */);
			_canceller.Token.Register(() => { Output.AddLine(Resolved ? "Request stopped." : "Request timed out."); Resolved = true; });
			Task.Run(() => ProcessRequest(uri, Output), _canceller.Token);
		}

		void onLinesChanged(MultilineTextBox.ChangedEventArgs e)
		{
			Dispatcher.Invoke(() => outputBox.Text = e.FullText);
		}

		public async Task ProcessRequest(string uri, MultilineTextBox output = null)
		{
			if (output != null)
				output.AddLine($"Accessing '{uri}'...");

			try
			{
				WebClient client = new WebClient();
				string response = await client.DownloadStringTaskAsync(uri);
				var parsedResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
				if (output != null)
				{
					output.AddLine("Status: " + parsedResponse["status"]);
					if (parsedResponse.ContainsKey("response"))
						output.AddLine("Response: " + parsedResponse["response"]);
					else
					{
						output.AddLine("Error: " + parsedResponse["error"]);
						Resolved = true;
						_canceller.Cancel();
					}
				}
				Resolved = true;
			}
			catch (Exception ex)
			{
				if (output != null)
					output.AddLine($"ERROR: Exception thrown during access: {ex.Message}");
				Resolved = true;
				_canceller.Cancel();
			}
		}

		private void exitButton_Click(object sender, RoutedEventArgs e)
		{
			if (Resolved)
			{
				Close();
			}
			else
			{
				Resolved = true;
				_canceller.Cancel();
			}
		}
	}

	public class MultilineTextBox
	{
		public class ChangedEventArgs
		{
			public string Text { get; set; }

			public string FullText { get; set; }

			public ChangedEventArgs(string text, string full = null)
			{
				Text = text;
				FullText = full;
			}
		}

		private StringBuilder _content;

		public delegate void LinesChangedDelegate(ChangedEventArgs args);

		public event LinesChangedDelegate LinesChanged;

		public string Text
		{
			get { return _content.ToString(); }
			set
			{
				_content.Clear();
				_content.Append(value);
				LinesChanged.Invoke(new ChangedEventArgs(value, _content.ToString()));
			}
		}

		public MultilineTextBox()
		{
			_content = new StringBuilder();
		}

		public void AddLine(string line)
		{
			if (_content.Length > 0)
				_content.Append("\n");
			_content.Append(line);
			LinesChanged.Invoke(new ChangedEventArgs(line, _content.ToString()));
		}

		public void Clear()
		{
			_content.Clear();
			LinesChanged.Invoke(new ChangedEventArgs("", ""));
		}

		public List<string> GetLines()
		{
			return _content.ToString().Split('\n').ToList();
		}
	}
}
