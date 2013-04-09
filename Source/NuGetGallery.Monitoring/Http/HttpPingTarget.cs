using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace NuGetGallery.Monitoring.Http
{
    public class HttpPingTarget
    {
        public Uri Url { get; private set; }
        public string Method { get; private set; }
        public HttpStatusCode? ExpectedStatusCode { get; private set; }
        public TimeSpan ExpectedTimeout { get; private set; }
        
        public HttpPingTarget(Uri url) : this(url, "GET") { }
        public HttpPingTarget(Uri url, string method) : this(url, method, TimeSpan.FromMilliseconds(500))
        {
        }

        public HttpPingTarget(Uri url, string method, HttpStatusCode expectedStatusCode)
            : this(url, method, expectedStatusCode, TimeSpan.FromMilliseconds(500))
        {
        }

        public HttpPingTarget(Uri url, string method, TimeSpan expectedTimeout)
        {
            Url = url;
            Method = method;
            ExpectedTimeout = expectedTimeout;
            ExpectedStatusCode = null;
        }

        public HttpPingTarget(Uri url, string method, HttpStatusCode expectedStatusCode, TimeSpan expectedTimeout)
            : this(url, method, expectedTimeout)
        {
            ExpectedStatusCode = expectedStatusCode;
        }
    }
}
