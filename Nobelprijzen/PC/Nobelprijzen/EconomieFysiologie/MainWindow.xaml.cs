﻿using MiDeRP;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Nobel
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		const int TimeoutBeforeStart = 4;
		MySqlConnection connection;
		BeamerWindow bw = new BeamerWindow();
		String comPort = "";
		SerialInterface Serial;
		Stopwatch sA, sB;
		String teamA, teamB;
		Boolean running = false;
		Boolean runningPM = false;
		int prematch = TimeoutBeforeStart;
		List<String> teams = new List<String>();
		FileInfo teamsPath = new FileInfo("teams.csv");
		FileInfo timesPath = new FileInfo("times.csv");
		Timer timer = new Timer(50);
		Timer timerPrematch = new Timer(1000);
		Timer timerDatabase = new Timer(1000);
		Action<String, long, bool, String, long, bool> updateSW;
		Action<int> updatePM;
		Action startAdt;
		Action updateDB;
		public MainWindow()
		{
			InitializeComponent();
			bw.Show();
			sA = new Stopwatch();
			sB = new Stopwatch();
			using (StreamReader r = teamsPath.OpenText())
			{
				teams.Clear();
				while (!r.EndOfStream)
				{
					teams.Add(r.ReadLine());
				}
			}
			foreach (String s in teams)
			{
				teamAComboBox.Items.Add(s);
				teamBComboBox.Items.Add(s);
			}
			updateSW = bw.updateStopwatch;
			updatePM = bw.updatePrematch;
			startAdt = startTimers;
			updateDB = updateDatabase;
			timer.Elapsed += timer_Elapsed;
			timerPrematch.Elapsed += timerPrematch_Elapsed;
			timerDatabase.Elapsed += timerDatabase_Elapsed;
			
		}

		void timerDatabase_Elapsed(object sender, ElapsedEventArgs e)
		{
			g.Dispatcher.BeginInvoke(DispatcherPriority.Send, updateDB);
		}

		void timerPrematch_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (runningPM)
			{
				prematch--;
				g.Dispatcher.BeginInvoke(DispatcherPriority.Send, updatePM, prematch);
				if (prematch == 0)
				{
					g.Dispatcher.BeginInvoke(DispatcherPriority.Send, startAdt);
				}
			}
		}

		void timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (running)
			{	
				g.Dispatcher.BeginInvoke(DispatcherPriority.Send, updateSW, teamA, sA.ElapsedMilliseconds, sA.IsRunning, teamB, sB.ElapsedMilliseconds, sB.IsRunning);				
			}
			
		}

		private void connectButton_Click(object sender, RoutedEventArgs e)
		{
			comPort = ((ComboBoxItem)comPortComboBox.SelectedItem).Content.ToString();
			if (comPort == String.Empty)
			{
				MessageBox.Show("Geen comport geselecteerd.");
				return;
			}
			Serial = new SerialInterface(comPort, 115200);
			Serial.OpenPort();
			if (Serial.IsOpen)
			{
				Serial.SerialDataEvent += Serial_SerialDataEvent;
				controlsFysiologieStackPanel.IsEnabled = true;
				connectButton.IsEnabled = false;
				comPortComboBox.IsEnabled = false;
			}
			else
			{
				controlsFysiologieStackPanel.IsEnabled = false;
				connectButton.IsEnabled = true;
				comPortComboBox.IsEnabled = true;
			}

		}

		void Serial_SerialDataEvent(object sender, SerialDataEventArgs e)
		{
			handleSerial(e.DataByte);			
		}

		private void comPortComboBox_DropDownOpened(object sender, EventArgs e)
		{
			string[] ports = SerialPort.GetPortNames();
			comPortComboBox.Items.Clear();
			foreach (string s in ports)
			{
				ComboBoxItem extra = new ComboBoxItem();
				extra.Content = s;
				if (comPort == s)
				{
					extra.IsSelected = true;
				}
				comPortComboBox.Items.Add(extra);
			}
		}

		private void startTimeButton_Click(object sender, RoutedEventArgs e)
		{
			teamA = teamAComboBox.SelectedItem.ToString();
			teamB = teamBComboBox.SelectedItem.ToString();
			if (teamA != String.Empty && teamB != String.Empty && teamA != teamB)
			{
				runningPM = true;
				prematch = TimeoutBeforeStart;
				timerPrematch.Start();				
				startTimeButton.IsEnabled = false;
				cancelButton.IsEnabled = false;
			}
			else
			{
				MessageBox.Show("Check selected teams.");
			}
		}
		public void handleSerial(Byte b)
		{
			if (g.Dispatcher.CheckAccess())
			{
				if (running)
				{
					if ((char)b== 'A')
					{
						sA.Stop();
					}
					else if ((char)b == 'B')
					{
						sB.Stop();
					}
					if (!sA.IsRunning && !sB.IsRunning)
					{
						running = false;
						timer.Stop();
						bw.showTimes(teamA, sA.ElapsedMilliseconds, teamB, sB.ElapsedMilliseconds);
						writeTimes(teamA, sA.ElapsedMilliseconds, teamB, sB.ElapsedMilliseconds);
						startTimeButton.IsEnabled = true;
						cancelButton.IsEnabled = false;
					}
				}				
			}
            else
            {
				Action<Byte> func = handleSerial;
				g.Dispatcher.Invoke(DispatcherPriority.Normal, func, b);
            }			
		}
		void writeTimes(String teamA, long msA, String teamB, long msB)
		{
			using (StreamWriter w = timesPath.AppendText())
			{
				w.WriteLine("\"{0}\";{1};\"{2}\";{3}",teamA,msA,teamB,msB);
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			bw._allowclosing = true;
			bw.Close();
			if (connection!=null)
			{
				connection.Close();
			}
		}

		private void cancelButton_Click(object sender, RoutedEventArgs e)
		{
			running = false;
			timer.Stop();
			Serial.SendByte((Byte)'r');
			startTimeButton.IsEnabled = true;
			cancelButton.IsEnabled = false;
			bw.clearTimes();
		}
		private void startTimers()
		{
			runningPM = false;
			sA.Reset();
			sB.Reset();
			sA.Start();
			sB.Start();
			timer.Start();
			Serial.SendByte((Byte)'r');
			Serial.SendByte((Byte)'s');
			runningPM = false;
			running = true;
			cancelButton.IsEnabled = true;
			timerPrematch.Stop();
		}

		private void mysqlConnectButton_Click(object sender, RoutedEventArgs e)
		{
			MySqlConnectionStringBuilder csb = new MySqlConnectionStringBuilder();

			if (String.IsNullOrEmpty(serverTextBox.Text))
			{
				MessageBox.Show("Enter server.");
				return;
			}
			if (String.IsNullOrEmpty(userTextBox.Text))
			{
				MessageBox.Show("Enter username.");
				return;
			}
			if (String.IsNullOrEmpty(passwordBox.Password))
			{
				MessageBox.Show("Enter password.");
				return;
			}
			if (String.IsNullOrEmpty(databaseTextBox.Text))
			{
				MessageBox.Show("Enter password.");
				return;
			}			
			csb.Server = serverTextBox.Text;
			csb.Database = databaseTextBox.Text;
			csb.UserID = userTextBox.Text;
			csb.Password = passwordBox.Password;
			try
			{
				connection = new MySqlConnection(csb.ToString());
				connection.Open();
				if (connection.State != System.Data.ConnectionState.Open)
				{
					MessageBox.Show("Not connected");
				}
				else
				{
					mysqlConnectButton.IsEnabled = false;
					timerDatabase.Start();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}
		void updateDatabase()
		{
			string query = "SELECT * FROM prijs LIMIT 10;";
			//Create Command
			MySqlCommand cmd = new MySqlCommand(query, connection);
			//Create a data reader and Execute the command
			MySqlDataReader dataReader = cmd.ExecuteReader();
			//Read the data and store them in the list
			barItemsListBox.Items.Clear();
			while (dataReader.Read())
			{
				barItemsListBox.Items.Add(new BarItem() { Name = dataReader["Prijs_Naam"].ToString(), Selected = false});
			}
			//close Data Reader
			dataReader.Close();
		}
	}
	class BarItem
	{
		public string Name;
		public bool Selected;
	}
}