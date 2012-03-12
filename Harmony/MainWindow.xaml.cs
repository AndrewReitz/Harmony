/*
* This file is part of Harmony a C# HTML5 Streaming media server
*
* Copyright 2012 Andrew Reitz
*	
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>
*/

using System;
using System.Windows;
using System.Windows.Forms;
using HTML5MusicServer;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace Harmony
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WebServer _server;

        public MainWindow()
        {
            InitializeComponent();

            //check if vista or greater, if not appliction will not run correctly
            //close application if that is the case
            if (!(Environment.OSVersion.Version.Major >= 6))
            {
                System.Windows.MessageBox.Show("The operating system you are running this application on is not supported", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                this.Close();
            }
            else if (!IsElevated())
            {
                System.Windows.MessageBox.Show("Sorry, but in order for this application to run correctly it needs to be run as administrator",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Load();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a Folder that contains your music";
                dialog.ShowNewFolderButton = false;
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;

                if (!string.IsNullOrEmpty(this.textBoxBrowseContent.Text))
                {
                    dialog.SelectedPath = this.textBoxBrowseContent.Text;
                }
                else
                {
                    dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.textBoxBrowseContent.Text = dialog.SelectedPath;
                }
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errorMsg = new StringBuilder();
            string musicDir = this.textBoxBrowseContent.Text;
            string port = this.textBoxPort.Text;
            
            if (string.IsNullOrEmpty(musicDir))
            {
                errorMsg.Append("You are missing a music directory\n");
            }
            if (string.IsNullOrEmpty(port))
            {
                errorMsg.Append("You are missing a port number");
            }

            if (!string.IsNullOrEmpty(errorMsg.ToString()))
            {
                System.Windows.MessageBox.Show(errorMsg.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                errorMsg = new StringBuilder();
                if (Directory.Exists(musicDir))
                {
                    errorMsg.Append("Invalid Music Directory\n");
                }

                int portNum;
                if (Int32.TryParse(port, out portNum))
                {
                    errorMsg.Append("Invalid Port Number");
                }

                if (!string.IsNullOrEmpty(errorMsg.ToString()))
                {
                    //check if null for first run
                    if (_server == null || !_server.IsListening())
                    {
                        _server = new WebServer(musicDir, portNum);
                        _server.Start();
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show(errorMsg.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            if (_server != null && _server.IsListening())
            {
                _server.Stop();
            }
        }

        private void Load()
        {
            this.textBoxBrowseContent.Text = Properties.Settings.Default.musicPath;
            this.textBoxPort.Text = Properties.Settings.Default.port;
        }

        private void Save()
        {
            Properties.Settings.Default.musicPath = this.textBoxBrowseContent.Text;
            Properties.Settings.Default.port = this.textBoxPort.Text;
            Properties.Settings.Default.Save();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Save();
        }

        private bool IsElevated()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
