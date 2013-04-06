using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Monitoring;
using NuGetGallery.Monitoring.Http;

namespace Monitor
{
    class Program
    {
        static void Main(string[] args)
        {
            //Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));
            RunHttpMonitor();
        }

        private static void RunHttpMonitor()
        {
            ConsoleMonitorRunner.Run("HTTP", TimeSpan.Zero, new HttpMonitor(
                new HttpPingTarget(new Uri("http://nugetgallery-qa.cloudapp.net/")),
                new HttpPingTarget(new Uri("http://www.bing.com/"))));
        }
    }
}
