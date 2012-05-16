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
        }

        private void buttonBrowseDirectory_Click(object sender, EventArgs e)
        {
            //no need to error check, if not a valid path it defualts to MyComputer
            _folderBrowserDialog.SelectedPath = textBoxMusicDirectory.Text;

            if (_folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                textBoxMusicDirectory.Text = _folderBrowserDialog.SelectedPath;
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            StringBuilder errorMessage = new StringBuilder();

            string musicDir = textBoxMusicDirectory.Text;
            string portString = textBoxPort.Text;

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
                _webServer.Start();

            }
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            _webServer.Stop();
        }
    }
}
