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
using System.Web;

namespace HTML5MusicServer
{
    public class Request
    {
        public const string POST = "POST";
        public const string GET = "GET";

        /// <summary>
        /// key value pairs of headers recieved from the client
        /// </summary>
        public Dictionary<string, string> Headers
        {
            get { return _headers; }
        }
        private Dictionary<string, string> _headers;

        /// <summary>
        /// Url resource requested by the client
        /// </summary>
        public string Url
        {
            get { return _url; }
        }
        private string _url;

        /// <summary>
        /// Http Method, GET POST, ect
        /// </summary>
        public string HttpMethod
        {
            get { return _httpMethod; }
        }
        private string _httpMethod;

        /// <summary>
        /// Key value pairs from the post string
        /// </summary>
        public Dictionary<string, string> PostValues
        {
            get { return _postValues; }
        }
        private Dictionary<string, string> _postValues;

        /// <summary>
        /// Key value paris for retreiving cookies
        /// </summary>
        public Dictionary<string, string> Cookies
        {
            get { return _cookies; }
        }
        private Dictionary<string, string> _cookies;

        /// <summary>
        /// Constructor: creates a request object with easy to acess values to the request
        /// from the client
        /// </summary>
        /// <param name="request">request recieved from client</param>
        public Request(string request)
        {
            //for some reason the the server sometimes recieves empty values
            //just making sure they don't get here
            if (!string.IsNullOrEmpty(request))
            {
                this.ProcessRequestType(request);
                this.ProcessHeaders(request);
            }
        }

        /// <summary>
        /// Parses out the type of request (GET POST ect.)
        /// and the requested file from the server placing in this classes HttpMethod and Url objects
        /// respectivly
        /// </summary>
        /// <param name="request">the request sent from the client</param>
        private void ProcessRequestType(string request)
        {
            string[] requestTemp = request.Substring(0, request.IndexOf('\n')).Split(' ');

            this._httpMethod = requestTemp[0].ToUpper();
            this._url = requestTemp[1];
            //don't care about http type just assuming 1.1
        }

        /// <summary>
        /// Parses out the headers from the client and places them into this objects Headers Dictionary
        /// if there is a post string the parse post string method is called
        /// </summary>
        /// <param name="request">request from the client</param>
        private void ProcessHeaders(string request)
        {
            string[] headersTemp = request.Substring(request.IndexOf('\n') + 1).Split('\n');

            Dictionary<string, string> headers = new Dictionary<string, string>();
            for (int i = 0; i < headersTemp.Length; i++)
            {
                string head = headersTemp[i];

                if (!string.IsNullOrWhiteSpace(head))
                {
                    string[] temp = head.Split(':');
                    headers.Add(temp[0].Trim(), temp[1].Trim());
                }
                else
                {
                    ProcessPost(headersTemp[++i]);
                }
            }

            this._headers = headers;
        }

        private void ProcessCookies(string cookieString)
        {
            //TODO: FILL COOKIES STUFF IN!
        }

        /// <summary>
        /// Parses the post string and puts the key, value pairs into this classes PostValue dictionary
        /// also un-encodes the strings that may have been in in hex form
        /// </summary>
        /// <param name="post_string"></param>
        private void ProcessPost(string post_string)
        {
            if (!string.IsNullOrEmpty(post_string))
            {
                Dictionary<string, string> post = new Dictionary<string, string>();

                string[] name_AND_value = post_string.Split('&');
                if (name_AND_value.Length > 1)
                {
                    foreach (string s in name_AND_value)
                    {
                        string[] name_OR_value = s.Split('=');
                        post.Add(HttpUtility.HtmlDecode(name_OR_value[0]), HttpUtility.HtmlDecode(name_OR_value[1]));
                    }
                }

                //if nothing was added there wasn't a post string and we somehow got here by accident
                //if that's the case would rather continue check that the post string is null and
                //not null or empty
                if (post.Keys.Count != 0)
                {
                    this._postValues = post;
                }
            }
        }
    }
}
