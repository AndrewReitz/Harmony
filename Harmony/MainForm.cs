using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HTML5MusicServer;

namespace Harmony
{
    public partial class MainForm : Form
    {
        WebServer _server = new WebServer(@"H:\Music", 2525, "temp", "password");
        public MainForm()
        {
            InitializeComponent();
            _server.Start();
        }
    }
}
