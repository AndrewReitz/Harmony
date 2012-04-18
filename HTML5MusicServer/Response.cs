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

namespace HTML5MusicServer
{
    class Response
    {
        public const string HTTP_PROTOCAL = "HTTP/1.1";

        public const string SERVER = "Harmony Streaming Media Server"; //repace with os and get version number from assembly

        public const string STATUS_CODE_OK = "200 OK";
        public const string STATUS_CODE_BAD_REQUEST = "400 Bad Request";
        public const string STATUS_CODE_NOT_FOUND = "404 Not Found";
        public const string STATUS_CODE_NOT_MODIFIED = "304 Not Modified";
        public const string STATUS_COSE_NOT_PARTIAL_CONTENT = "206 Partial Content";

        private string _ResponseStatus = STATUS_CODE_OK;
        public string ResponseStatus
        {
            set
            {
                _ResponseStatus = HTTP_PROTOCAL + value;
            }
        }

        private  string _ContentEncoding;
        public string ContentEncoding
        {
            set
            {
                _ContentEncoding = "Content-Encoding: " + value;
            }
        }

        private string _ContentType;
        public string ContentType
        {
            set
            {
                _ContentType = "Content-Type: " + value;
            }
        }

        private string _LastModified;
        public string LastModified
        {
            set
            {
                _LastModified = "Last-Modified: " + string.Format("{0:R}", DateTime.Now);
            }
        }
    }
}