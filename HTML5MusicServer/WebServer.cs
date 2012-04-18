﻿/*
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
        readonly string _executingDirectory = Path.GetDirectoryName(Assembly.GetAssembly(typeof(WebServer)).Location);
        readonly string _musicDirectory;
        readonly string _javaScriptDir;
        readonly string _skins;
        readonly string _login_HTML;
        readonly string _audioPlayer_HTML;
        readonly string _username;
        readonly string _password;
        readonly string _userHash = "E3C2D6B8-33B0-4C53-88AF-1A51261C59F7";

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
            //_login_HTML = File.ReadAllText(Path.Combine(_executingDirectory, "login.html")); CURRENTLY NO LOGINPAGE
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
            Response response = new Response();
            byte[] cBuffer = null; //bytes of the files used later to deliver data to the client

            if (request.HttpMethod == Request.GET)
            {
                string filePath;
                if (request.Url == "/")
                {
                    cBuffer = GetWebPage(_musicDirectory);
                }
                //add more else ifs if more file's are added
                else if (request.Url.Contains("/skin/"))
                {
                    cBuffer = GetFileBytes(Path.Combine(_skins, Path.GetFileName(request.Url)));
                }
                else if (request.Url.Contains("/js/"))
                {
                    cBuffer = GetFileBytes(Path.Combine(_javaScriptDir, Path.GetFileName(request.Url)));
                }
                //not the pretiest but only asign filePath when needed
                else if (File.Exists(filePath = Path.Combine(_musicDirectory, Uri.UnescapeDataString(request.Url.Replace("/", "\\").Remove(0, 1)))))
                {
                    switch (Path.GetExtension(filePath))
                    {
                        case ".mp3": response.ContentType = "audio/mpeg"; cBuffer = GetFileBytes(filePath); break;
                        case ".m4a": response.ContentType = "audio/mp4"; cBuffer = GetFileBytes(filePath); break;
                        case ".flac": response.ContentType = "audio/x-flac"; cBuffer = GetFileBytes(filePath); break; //try but doubt it will work
                        case ".mp4": response.ContentType = "audio/mp4"; cBuffer = GetFileBytes(filePath); break;
                        case ".ogg": response.ContentType = "audio/ogg"; cBuffer = GetFileBytes(filePath); break;
                        case ".wav": response.ContentType = "audio/wav"; cBuffer = GetFileBytes(filePath); break;
                        default: cBuffer = NotFound(response); break;
                    }
                }
                else if (Directory.Exists(filePath))
                {
                    cBuffer = GetWebPage(filePath);
                }
                else
                {
                    cBuffer = NotFound(response);
                }

            }
            else if (request.HttpMethod == Request.POST)
            {
                Console.WriteLine("POST RECIEVE");
            }
            byte[] hBuffer = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n");

            clientStream.Write(hBuffer, 0, hBuffer.Length);
            clientStream.Write(cBuffer, 0, cBuffer.Length);
            clientStream.Close();
            client.Close();
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
        
        byte[] NotFound(Response response)
        {
            response.ResponseStatus = Response.STATUS_CODE_NOT_FOUND;
            //rewire to actually use a file for customization
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
