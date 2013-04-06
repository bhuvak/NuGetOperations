using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Monitoring.Http
{
    public class HttpPingResult
    {
        public EventType Result { get; private set; }
        public Exception Exception { get; private set; }
        public string Message { get; private set; }
        public TimeSpan TimeToCompletion { get; private set; }
        public HttpPingTarget Target { get; private set; }

        public HttpPingResult(Exception ex, TimeSpan timeToCompletion, HttpPingTarget target)
        {
            Result = EventType.Failure;
            Exception = ex;
            TimeToCompletion = timeToCompletion;
            Target = target;
            Message = ex.Message;
        }

        public HttpPingResult(EventType result, string message, TimeSpan timeToCompletion, HttpPingTarget target)
        {
            Result = result;
            Message = message;
            TimeToCompletion = timeToCompletion;
            Target = target;
            Exception = null;
        }
    }
}
