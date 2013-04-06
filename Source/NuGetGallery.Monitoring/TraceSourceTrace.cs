using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace NuGetGallery.Monitoring
{
    public class TraceSourceTrace : ITrace
    {
        private TraceSource _trace;
        
        public TraceSourceTrace(TraceSource trace)
        {
            _trace = trace;
        }

        public TraceSourceTrace(string name)
        {
            _trace = new TraceSource(name, SourceLevels.All);
            _trace.Listeners.Clear();
            _trace.Listeners.AddRange(System.Diagnostics.Trace.Listeners);
        }

        public void Trace(TraceEventType type, string message)
        {
            _trace.TraceEvent(type, id: 0, message: message);
        }
    }
}
