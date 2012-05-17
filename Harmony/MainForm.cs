using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HTML5MusicServer;
using System.IO;

namespace Harmony
{
    public partial class MainForm : Form
    {
        //init to random crap in case user wants to click the stop button before starting
        WebServer _webServer = new WebServer("", 5555, "", "");
        FolderBrowserDialog _folderBrowserDialog = new FolderBrowserDialog();

        public MainForm()
        {
            InitializeComponent();

            _folderBrowserDialog.Description = "Select your music directory";
            _folderBrowserDialog.ShowNewFolderButton = false;
            _folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;

            LoadSettings();
        }

        private void buttonBrowseDirectory_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.textBoxMusicDirectory.Text))
            {
                _folderBrowserDialog.SelectedPath = this.textBoxMusicDirectory.Text;
            }
            else
            {
                _folderBrowserDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }

            if (_folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                this.textBoxMusicDirectory.Text = _folderBrowserDialog.SelectedPath;
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            StringBuilder errorMessage = new StringBuilder();

            string musicDir = this.textBoxMusicDirectory.Text;
            string portString = this.textBoxPort.Text;

            //validate directory
            if (string.IsNullOrEmpty(musicDir))
            {
                errorMessage.AppendLine("Please Provid a Music Directory");
            }
            else if (!Directory.Exists(musicDir))
            {
                errorMessage.AppendLine("That is not a valid Directory");
            }

            //validate port
            int port = 5555;
            if (string.IsNullOrEmpty(portString) || !Int32.TryParse(portString, out port))
            {
                errorMessage.AppendLine("Please provide a valid port number");
            }

            //display error message or start server
            if (errorMessage.Length != 0)
            {
                MessageBox.Show(errorMessage.ToString(), "Incorrect Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                //TODO: move into one call and then can get rid of constructor every time called
                _webServer = new WebServer(musicDir, port, "test", "test");
                try
                {
                    _webServer.Start();
                }
                catch (Exception ex)
                {
#if DEBUG
                    MessageBox.Show(ex.Message);
#else
                    MessageBox.Show("Error trying to start server, do you have another server already running?", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
                }

            }
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            _webServer.Stop();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _webServer.Stop();
            SaveSettings();
        }

        private void LoadSettings()
        {
            this.textBoxMusicDirectory.Text = Properties.Settings.Default.musicPath;
            this.textBoxPort.Text = Properties.Settings.Default.port;
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.musicPath = this.textBoxMusicDirectory.Text;
            Properties.Settings.Default.port = this.textBoxPort.Text;
            Properties.Settings.Default.Save();
        }
    }
}
