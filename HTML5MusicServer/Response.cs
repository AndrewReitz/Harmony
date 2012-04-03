using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebServerTesting
{
    class Response
    {
        public static const string HTTP_PROTOCAL = "HTTP/1.1";

        public static const string SERVER = "Harmony Streaming Media Server"; //repace with os and get version number from assembly

        public static const string STATUS_CODE_OK = "200 OK";
        public static const string STATUS_CODE_BAD_REQUEST = "400 Bad Request";
        public static const string STATUS_CODE_NOT_FOUND = "404 Not Found";
        public static const string STATUS_CODE_NOT_MODIFIED = "304 Not Modified";
        public static const string STATUS_COSE_NOT_PARTIAL_CONTENT = "206 Partial Content";

        public string _ResponseStatus;
        public string ResponseStatus
        {
            set
            {
                _ResponseStatus = HTTP_PROTOCAL + value;
            }
        }
    }
}