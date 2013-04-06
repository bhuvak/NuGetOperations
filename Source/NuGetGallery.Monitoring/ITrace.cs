using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace NuGetGallery.Monitoring
{
    public interface ITrace
    {
        void Trace(TraceEventType type, string message);
    }

    public static class TraceExtensions
    {
        public static void Info(this ITrace self, string format, params object[] args)
        {
            self.Trace(TraceEventType.Information, String.Format(format, args));
        }

        public static void Warning(this ITrace self, string format, params object[] args)
        {
            self.Trace(TraceEventType.Warning, String.Format(format, args));
        }

        public static void Critical(this ITrace self, string format, params object[] args)
        {
            self.Trace(TraceEventType.Critical, String.Format(format, args));
        }

        public static void Error(this ITrace self, string format, params object[] args)
        {
            self.Trace(TraceEventType.Error, String.Format(format, args));
        }

        public static void Verbose(this ITrace self, string format, params object[] args)
        {
            self.Trace(TraceEventType.Verbose, String.Format(format, args));
        }
    }
}
