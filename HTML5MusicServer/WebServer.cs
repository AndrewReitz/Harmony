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
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using System.Xml.Linq;
using System.Security.Cryptography;

namespace HTML5MusicServer
{
    public class WebServer
    {
        HttpListener _listener;
        string _musicDirectory;
        string _javaScriptDir;
        string _skins;

        //it was just easier to do it this way instead of using XElement
        string _audioPlayer_HTML;

        public WebServer(string MusicDirectory, int port)
        {
            ThreadPool.SetMinThreads(50, 50);
            ThreadPool.SetMaxThreads(500, 1000);
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + port + "/");
            _musicDirectory = MusicDirectory;
            _javaScriptDir = Path.Combine(Directory.GetCurrentDirectory(), "js");
            _skins = Path.Combine(Directory.GetCurrentDirectory(), "skin");
            _audioPlayer_HTML = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "audio_player.html"));
        }

        /// <summary>
        /// Starts the webserver
        /// Can Throw HttpListenerException
        /// </summary>
        public void Start()
        {
            _listener.Start();

            Thread t = new Thread(Run);
            t.Start();
        }

        /// <summary>
        /// Run's the webserver, this is called by the start method
        /// Can Throw HttpListenerException
        /// </summary>
        private void Run()
        {
            while (true)
            {
                try
                {
                    HttpListenerContext request = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, request);
                }
                catch (HttpListenerException e)
                {
                    //error code 995 is the server was shutdown while in a request
                    if (e.ErrorCode != 995)
                    {
                        throw e;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (InvalidOperationException)
                {
                    //breaks out of the loop if stop was called
                    //there has to be a better way to handle this but
                    //i have yet to find one
                    break;
                }
            }
        }

        /// <summary>
        /// Stops the server from listening
        /// </summary>
        public void Stop()
        {
            _listener.Stop();
        }

        /// <summary>
        /// Check if the server is running or not
        /// </summary>
        /// <returns>True: server is running
        /// False: server is not running</returns>
        public bool IsListening()
        {
            return _listener.IsListening;
        }

        void ProcessRequest(object listenerContext)
        {
            HttpListenerContext context = (HttpListenerContext)listenerContext;

            //string msg = context.Request.HttpMethod + " " + context.Request.Url;
            //Console.WriteLine(msg);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.SendChunked = true;
            context.Response.KeepAlive = true;
            context.Response.ProtocolVersion = HttpVersion.Version11;
            context.Response.AddHeader("Connection", "Keep-Alive");
            context.Response.AddHeader("Keep-Alive", "timeout=15, max=100");
            //context.Response.Headers.Add("Content-Encoding: gzip");

            byte[] b = null;
            switch (context.Request.RawUrl)
            {
                case "/": b = GetWebPage(_musicDirectory); break;
                default:
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

                        context.Response.AddHeader("ETag", GetMD5(b));
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
                    break;
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
                    rangeEnd = b.Length;
                }

                context.Response.AddHeader("Content-Range", rangeBegin + "-" + (rangeEnd - rangeBegin) + "/" + rangeEnd);
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
                        break;
                    case ".m4a":
                        json_music.Append(getMusic(file));
                        break;
                    case ".flac": //don't think this type is supported by jplayer
                        json_music.Append(getMusic(file));
                        break;
                    case ".mp4":
                        json_music.Append(getMusic(file));
                        break;
                    case ".ogg":
                        json_music.Append(getMusic(file));
                        break;
                    case ".wav":
                        json_music.Append(getMusic(file));
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
                    "\">" + dir.Replace(_musicDirectory, "").Replace("\\", "/") + "</a><br>"
                    );
            }

            string returnString = _audioPlayer_HTML.Replace("##Audio_Here##", json_music.ToString());
            returnString = returnString.Replace("#Directories_Here#", directories.ToString());


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
