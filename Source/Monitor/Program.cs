using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Monitoring;
using NuGetGallery.Monitoring.Http;
using NuGetGallery.Monitoring.Sql;

namespace Monitor
{
    class Program
    {
        static void Main(string[] args)
        {
            //RunHttpMonitor();
            RunDatabaseMonitors();
        }

        private static void RunDatabaseMonitors()
        {

        }

        private static void RunHttpMonitor()
        {
            var knownGood = new HttpPingTarget(new Uri("http://www.bing.com/"));
            ConsoleMonitorRunner.Run("http", TimeSpan.Zero,
                new HttpMonitor(
                    "nuget.org",
                    new HttpPingTarget(new Uri("http://www.nuget.org/")),
                    knownGood),
                new HttpMonitor(
                    "nuget.org/api/v2/Packages",
                    new HttpPingTarget(new Uri("http://www.nuget.org/api/v2/Packages")),
                    knownGood));
        }
    }
}
