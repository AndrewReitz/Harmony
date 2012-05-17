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
using System.Diagnostics;

namespace HTML5MusicServer
{
    public class WebServer
    {
        public static ManualResetEvent tcpClientConnected = new ManualResetEvent(false);
        TcpListener _listener;
        readonly string _executingDirectory = Path.GetDirectoryName(Assembly.GetAssembly(typeof(WebServer)).Location);
        readonly string _musicDirectory;
        //readonly string _login_HTML;
        readonly string _audioPlayer_HTML;
        readonly string _username;
        readonly string _password;
        //readonly string _userHash = "E3C2D6B8-33B0-4C53-88AF-1A51261C59F7";

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
            //if it's already listening don't spawn another thread
            if (!_isListening)
            {
                _isListening = true;

                Thread t = new Thread(new ParameterizedThreadStart(RunServer));
                t.Start(_listener);
            }
        }

        /// <summary>
        /// Stops the server from listening
        /// </summary>
        public void Stop()
        {
            if (_isListening)
            {
                _isListening = false;
                _listener.Stop();
            }
        }

        private void RunServer(object tcpListner)
        {
            TcpListener listener = (TcpListener)tcpListner;
            listener.Start();

            while (this._isListening)
            {
                TcpClient client = null;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch { /* listener was interupted by stop being called */ }

                if (client != null)
                {
                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleRequest));
                    clientThread.Start(client);
                }
            }
        }

        //Main loop of server
        private void HandleRequest(object client)
        {
            try
            {
                using (TcpClient tcpClient = (TcpClient)client)
                {
                    tcpClient.ReceiveTimeout = 15;
                    tcpClient.SendBufferSize = 4096; 
                    byte[] rBuffer = new byte[4096]; //read buffer
                    using (var clientStream = tcpClient.GetStream())
                    {
                        //TODO: Cache music files so they don't need to be read into memory again
                        //This will pretty much always be true but the Read function will throw an interupt if > 15ms
                        while (tcpClient.Connected)
                        {
                            int bytesRecieved = clientStream.Read(rBuffer, 0, rBuffer.Length);

                            string recieved = System.Text.Encoding.UTF8.GetString(rBuffer, 0, bytesRecieved);
                            if (!string.IsNullOrEmpty(recieved))
                            {
                                Request request = new Request(recieved);
                                Response response = new Response();
                                byte[] cBuffer = null; //bytes of the files used later to deliver data to the client

                                //TODO Clean this up
                                if (request.HttpMethod == Request.GET)
                                {
                                    string filePath;
                                    if (request.Url == "/")
                                    {
                                        cBuffer = GetWebPage(_musicDirectory, response);
                                    }
                                    //add more else ifs if more file's are added (hopefully no files confict with these names...)
                                    if (request.Url.Contains("/skin/") || request.Url.Contains("/js/") || request.Url.Contains("/pages/") || request.Url.Contains("/images/"))
                                    {
                                        cBuffer = GetFileBytes(Path.Combine(_executingDirectory, GetServerSidePath(request.Url)), request, response);
                                    }
                                    //not the pretiest but only asign filePath when needed
                                    else if (File.Exists(filePath = Path.Combine(_musicDirectory, GetServerSidePath(request.Url))))
                                    {
                                        //TODO: Move ContentType Handling to in the response class?
                                        switch (Path.GetExtension(filePath))
                                        {
                                            case ".mp3": response.ContentType = "audio/mpeg"; cBuffer = GetFileBytes(filePath, request, response); break;
                                            case ".m4a": response.ContentType = "audio/mp4"; cBuffer = GetFileBytes(filePath, request, response); break;
                                            case ".mp4": response.ContentType = "audio/mp4"; cBuffer = GetFileBytes(filePath, request, response); break;
                                            case ".ogg": response.ContentType = "audio/ogg"; cBuffer = GetFileBytes(filePath, request, response); break;
                                            case ".wav": response.ContentType = "audio/wav"; cBuffer = GetFileBytes(filePath, request, response); break;
                                            default: cBuffer = NotFound(response); break;
                                        }
                                    }
                                    else if (Directory.Exists(filePath))
                                    {
                                        cBuffer = GetWebPage(filePath, response);
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

                                response.SendResponse(clientStream, cBuffer);
                            }
                        }//end while loop
                    } //end client stream
                }
            }
            catch (IOException e)
            {
                //the client stream blocked to long and this was used to interupt it
                Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Adds all the music in the directory to the file and returns it's bytes
        /// </summary>
        /// <param name="directory">directory to get all the directory/music files</param>
        /// <returns>byte array of the generated webpage</returns>
        byte[] GetWebPage(string directory, Response response)
        {
            bool isMusic = false;

            //getting all the music and creating it in jason format to
            //to put into the file
            StringBuilder json_music = new StringBuilder();
            foreach (string file in Directory.GetFiles(directory))
            {
                switch (Path.GetExtension(file))
                {
                    //trying any and all, will put them in as type mp3... //move mime-types to file
                    case ".mp3":
                        json_music.Append(GetMusic(file));
                        isMusic = true;
                        break;
                    case ".m4a":
                        json_music.Append(GetMusic(file));
                        isMusic = true;
                        break;
                    case ".mp4":
                        json_music.Append(GetMusic(file));
                        isMusic = true;
                        break;
                    case ".ogg":
                        json_music.Append(GetMusic(file));
                        isMusic = true;
                        break;
                    case ".wav":
                        json_music.Append(GetMusic(file));
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
                //TODO: Replace with file
                returnString = "<!DOCTYPE html><head><title>HTML5 WebPlayer</title><meta http-equiv=\"Content-Type\"" +
                    "content=\"text/html; charset=iso-8859-1\" /></head><body>" + directories.ToString() + "</body></html>";
            }

            response.ContentType = "text/html";

            return Encoding.UTF8.GetBytes(returnString);
        }

        string GetMusic(string file)
        {
            return "\t{\n\tname:\"" + Path.GetFileName(file) + "\",\n\tmp3:\"" + file.Replace(_musicDirectory, "").Replace("\\", "/") + "\"\n\t},";
        }

        //TODO: Replace with file
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

        /// <summary>
        /// Gets the file bytes requested and handles the file properties of the 
        /// response such as LastModified date and Contenet-Type (TODO)
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        byte[] GetFileBytes(string filePath, Request request, Response response)
        {
            response.LastModified = this.GetLastModifiedDate(filePath);

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] returnBytes = new byte[fs.Length];
                fs.Read(returnBytes, 0, (int)fs.Length);

                //generate etag
                //TODO: Cache for this, also do only for media types
                //also set content types HERE!
                response.ETag = this.GetMD5(returnBytes);

                if (request.Headers.ContainsKey("Range"))
                {
                    response.ResponseStatus = Response.STATUS_COSE_PARTIAL_CONTENT;
                    string rangeHeader;
                    if ((rangeHeader = request.Headers["Range"]) != "bytes=0-")
                    {
                        string byteLengh = rangeHeader.Replace("bytes=", "");
                        response.ContentRange = string.Format("{0}/{1}", byteLengh, returnBytes.Length - 1);
                        string[] byteRanges = byteLengh.Split('-');
                        int begin = Int32.Parse(byteRanges[0]);
                        int end = Int32.Parse(byteRanges[1]);
                        int size = end - begin;
                        byte[] returnBytesSub = new byte[size];
                        for (int i = 0; i < size; i++)
                        {
                            returnBytesSub[i] = returnBytes[begin + 1];
                        }
                        return returnBytesSub;
                    }
                    else
                    {
                        response.ContentRange = string.Format("0-{0}/{1}", returnBytes.Length - 1, returnBytes.Length);
                        return returnBytes;
                    }
                }
                else
                {
                    return returnBytes;
                }
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

        /// <summary>
        /// Replaces "/", with "\" character to work on windows file system
        /// Uri unencodes anything that would have been url encoded and removes the first \ so that
        /// the url can be used with Path.Combine
        /// </summary>
        /// <param name="url">the requested url from the server</param>
        /// <returns>Url back url decoded and with "/" switched to "\"</returns>
        string GetServerSidePath(string url)
        {
            return Uri.UnescapeDataString(url).Replace("/", "\\").Remove(0, 1);
        }
    }
}
