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
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a Folder that contains your music";
                dialog.ShowNewFolderButton = false;
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;

                if (!string.IsNullOrEmpty(this.textBlockBrowseContent.Text))
                {
                    dialog.SelectedPath = this.textBlockBrowseContent.Text;
                }
                else
                {
                    dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.textBlockBrowseContent.Text = dialog.SelectedPath;
                }
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            string errorMsg = "";
            string musicDir = this.textBlockBrowseContent.Text;
            string port = this.textBoxPort.Text;

            if (string.IsNullOrEmpty(musicDir))
            {
                errorMsg = "You are missing a music directory\n";
            }
            if (string.IsNullOrEmpty(port))
            {
                errorMsg += "You are missing a port number";
            }

            if (!string.IsNullOrEmpty(errorMsg))
            {
                System.Windows.MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                errorMsg = "";
                if (Directory.Exists(musicDir))
                {
                    errorMsg = "Invalid Music Directory\n";
                }

                int portNum;
                if (Int32.TryParse(port, out portNum))
                {
                    errorMsg += "Invalid Port Number";
                }

                if (!string.IsNullOrEmpty(errorMsg))
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
                    System.Windows.MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
