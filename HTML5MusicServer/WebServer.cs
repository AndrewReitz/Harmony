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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace HTML5MusicServer
{
    public class WebServer
    {
        TcpListener _listener;
        const string _executingDirectory = Assembly.GetAssembly(typeof(WebServer)).Location;
        string _musicDirectory;
        string _javaScriptDir;
        string _skins;
        string _login_HTML;
        string _audioPlayer_HTML;
        string _username;
        string _password;        
        string _userHash = "E3C2D6B8-33B0-4C53-88AF-1A51261C59F7";

        /// <summary>
        /// Gets a value that indicates if the WebServer is running
        /// </summary>
        public bool IsListening
        {
            get { return _isListening; }
        }
        private volatile bool _isListening = false;

        public WebServer(string MusicDirectory, int port, string username, string password)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _musicDirectory = MusicDirectory;
            _javaScriptDir = Path.Combine(_executingDirectory, "js"); //GetCurrentDirectory needs to be changed
            _skins = Path.Combine(_executingDirectory, "skin");
            _audioPlayer_HTML = File.ReadAllText(Path.Combine(_executingDirectory, "audio_player.html"));
            _login_HTML = File.ReadAllText(Path.Combine(_executingDirectory, "login.html"));
            _username = username;
            _password = password;
        }

        /// <summary>
        /// Starts the webserver
        /// </summary>
        public void Start()
        {
            _isListening = true;

            Thread t = new Thread(new ParameterizedThreadStart(RunServer));
            t.Start(_listener);
        }

        /// <summary>
        /// Stops the server from listening
        /// </summary>
        public void Stop()
        {
            _isListening = false;
        }

        private void RunServer(object tcpListner)
        {
            TcpListener listener = (TcpListener)tcpListner;
            listener.Start();

            while (this._isListening)
            {
                listener.BeginAcceptTcpClient(new AsyncCallback(HandleRequest), listener);
            }

            listener.Stop();
        }

        private void HandleRequest(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(ar);

            byte[] rBuffer = new byte[4096];
            Stream clientStream = client.GetStream();
            int bytesRecieved = clientStream.Read(rBuffer, 0, rBuffer.Length);

            string recieved = System.Text.Encoding.UTF8.GetString(rBuffer, 0, bytesRecieved);
            Request request = new Request(recieved);

            StringBuilder responseBuilder;
            if (request.HttpMethod == Request.GET)
            {
                Console.WriteLine("GET REQUEST RECIEVE");
            }
            else if (request.HttpMethod == Request.POST)
            {
                Console.WriteLine("POST RECIEVE");
            }

            byte[] sBuffer = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n<html><body><p>TEST</p><form action=\"welcome.php\" method=\"post\">Name: <input type=\"text\" name=\"fname\" />Age: <input type=\"text\" name=\"age\" /><input type=\"submit\" /></form></body></html>\r\n");

            clientStream.Write(sBuffer, 0, sBuffer.Length);
            clientStream.Close();
            client.Close();
        }

        void ProcessRequest(object listenerContext)
        {
            HttpListenerContext context = (HttpListenerContext)listenerContext;

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.SendChunked = true;
            context.Response.KeepAlive = true;
            context.Response.ProtocolVersion = HttpVersion.Version11;
            //context.Response.AddHeader("Connection", "Keep-Alive");
            //context.Response.AddHeader("Keep-Alive", "timeout=15, max=100");
            //context.Response.Headers.Add("Content-Encoding: gzip");

            byte[] b = null;

            if (context.Request.HttpMethod == "POST")
            {
                string post_string = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                string[] split_post_string = post_string.Split('&');
                Dictionary<string, string> post_values = new Dictionary<string, string>();
                for (int i = 0; i < split_post_string.Length; i++)
                {
                    string[] values = split_post_string[i].Split('=');
                    if (values.Length == 2)
                    {
                        post_values.Add(values[0], values[1]);
                    }
                }

                for (int i = 0; i < post_values.Count; i++)
                {
                    if (post_values.ContainsKey("user") && post_values.ContainsKey("password"))
                    {
                        if (post_values["user"] == _username && post_values["password"] == _password)
                        {
                            Cookie c = new Cookie("ua", _userHash);
                            context.Response.Cookies.Add(c);
                            b = GetWebPage(_musicDirectory);
                        }
                        else
                        {
                            b = Encoding.UTF8.GetBytes(_login_HTML);
                        }
                    }
                    else
                    {
                        b = Encoding.UTF8.GetBytes(_login_HTML);
                    }
                }
            }
            else if (context.Request.Cookies["ua"] == null || context.Request.Cookies["ua"].Value != _userHash)
            {
                b = Encoding.UTF8.GetBytes(_login_HTML);
            }
            else
            {
                if (context.Request.RawUrl == "/")
                {
                    //check for authentication
                    b = GetWebPage(_musicDirectory);
                }
                else
                {
                    string filePath = Path.Combine(_musicDirectory, Uri.UnescapeDataString(context.Request.RawUrl.Replace("/", "\\").Remove(0, 1)));
                    if (context.Request.RawUrl.Contains("/skin/"))
                    {
                        b = GetFileBytes(Path.Combine(_skins, Path.GetFileName(context.Request.RawUrl)));
                    }
                    else if (context.Request.RawUrl.Contains("/js/"))
                    {
                        b = GetFileBytes(Path.Combine(_javaScriptDir, Path.GetFileName(context.Request.RawUrl)));
                    }
                    else if (File.Exists(filePath))
                    {
                        switch (Path.GetExtension(filePath))
                        {
                            case ".mp3": context.Response.ContentType = "audio/mpeg"; b = GetFileBytes(filePath); break;
                            case ".m4a": context.Response.ContentType = "audio/mp4"; b = GetFileBytes(filePath); break;
                            case ".flac": context.Response.ContentType = "audio/x-flac"; b = GetFileBytes(filePath); break; //try but doubt it will work
                            case ".mp4": context.Response.ContentType = "audio/mp4"; b = GetFileBytes(filePath); break;
                            case ".ogg": context.Response.ContentType = "audio/ogg"; b = GetFileBytes(filePath); break;
                            case ".wav": context.Response.ContentType = "audio/wav"; b = GetFileBytes(filePath); break;
                            default: b = NotFound(context); break;
                        }

                        context.Response.AddHeader("ETag", GetMD5(b).Replace("-", ""));
                        context.Response.AddHeader("Last-Modified", GetLastModifiedDate(filePath));
                    }
                    else if (Directory.Exists(filePath))
                    {
                        b = GetWebPage(filePath);
                    }
                    else
                    {
                        b = NotFound(context);
                    }
                }
            }

            int rangeBegin = 0;
            int rangeEnd = b.Length;
            string range = context.Request.Headers["Range"];

            if (range != null)
            {
                string[] temp = range.Replace("bytes=", "").Split('-');
                Int32.TryParse(temp[0], out rangeBegin);
                Int32.TryParse(temp[1], out rangeEnd);
                if (rangeEnd == 0)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
                    rangeEnd = b.Length;
                }

                context.Response.AddHeader("Content-Range", rangeBegin + "-" + (rangeEnd - rangeBegin) + "/" + rangeEnd + 1);
            }

            context.Response.ContentLength64 = b.Length;

            try
            {
                //add in gzip when this is running smoothly
                context.Response.OutputStream.Write(b, 0, b.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Adds all the music in the directory to the file and returns it's bytes
        /// </summary>
        /// <param name="directory">directory to get all the directory/music files</param>
        /// <returns>byte array of the generated webpage</returns>
        byte[] GetWebPage(string directory)
        {
            bool isMusic = false;

            //getting all the music and creating it in jason format to
            //to put into the file
            StringBuilder json_music = new StringBuilder();
            foreach (string file in Directory.GetFiles(directory))
            {
                switch (Path.GetExtension(file))
                {
                    //trying any and all, will put them in as type mp3...
                    case ".mp3":
                        json_music.Append(getMusic(file));
                        isMusic = true;
                        break;
                    case ".m4a":
                        json_music.Append(getMusic(file));
                        isMusic = true;
                        break;
                    case ".flac": //don't think this type is supported by jplayer
                        json_music.Append(getMusic(file));
                        isMusic = true;
                        break;
                    case ".mp4":
                        json_music.Append(getMusic(file));
                        isMusic = true;
                        break;
                    case ".ogg":
                        json_music.Append(getMusic(file));
                        isMusic = true;
                        break;
                    case ".wav":
                        json_music.Append(getMusic(file));
                        isMusic = true;
                        break;
                }
            }

            //getting all the directories
            StringBuilder directories = new StringBuilder();
            foreach (string dir in Directory.GetDirectories(directory))
            {
                directories.Append(
                    "<a href=\"" +
                    dir.Replace(_musicDirectory, "").Replace("\\", "/") +
                    "\">" + dir.Substring(dir.LastIndexOf("\\") + 1) + "</a><br>"
                    );
            }

            string returnString = "";
            if (isMusic)
            {
                returnString = _audioPlayer_HTML.Replace("##Audio_Here##", json_music.ToString());
                returnString = returnString.Replace("#Directories_Here#", directories.ToString());
            }
            else
            {
                returnString = "<!DOCTYPE html><head><title>HTML5 WebPlayer</title><meta http-equiv=\"Content-Type\"" +
                    "content=\"text/html; charset=iso-8859-1\" /></head><body>" + directories.ToString() + "</body></html>";
            }


            return Encoding.UTF8.GetBytes(returnString);
        }

        string getMusic(string file)
        {
            return "\t{\n\tname:\"" + Path.GetFileName(file) + "\",\n\tmp3:\"" + file.Replace(_musicDirectory, "").Replace("\\", "/") + "\"\n\t},";
        }

        byte[] NotFound(HttpListenerContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            XDocument doc = new XDocument(
                new XElement("html",
                    new XElement("head",
                        new XElement("title", "Error 404 (Not Found)")),
                    new XElement("body",
                        new XElement("h1", "404 file not found"))));

            return Encoding.UTF8.GetBytes("<!DOCTYPE html>" + doc.ToString());
        }

        byte[] GetFileBytes(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] returnBytes = new byte[fs.Length];
                fs.Read(returnBytes, 0, (int)fs.Length);
                return returnBytes;
            }
        }

        string GetMD5(byte[] bytes)
        {
            MD5 md5 = MD5.Create();

            StringBuilder md5Sb = new StringBuilder();
            foreach (byte b in md5.ComputeHash(bytes))
            {
                md5Sb.Append(b.ToString("x2"));
            }

            return md5Sb.ToString();
        }

        string GetLastModifiedDate(string filePath)
        {
            DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
            return string.Format("{0:ddd, dd MMM yyyy HH:mm:ss} GMT", lastWriteTimeUtc);
        }
    }
}
