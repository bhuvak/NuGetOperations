using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Monitoring;
using NuGetGallery.Monitoring.Http;
using NuGetGallery.Monitoring.NuGet;
using NuGetGallery.Monitoring.Sql;

namespace Monitor
{
    class Program
    {
        static void Main(string[] args)
        {
            //RunHttpMonitor();
            //RunDatabaseMonitors();
            RunStatisticsMonitors();
        }

        private static void RunStatisticsMonitors()
        {
            ConsoleMonitorRunner.Run("stats", TimeSpan.FromHours(1),
                new DownloadStatsMonitor("totals", new Uri("http://www.nuget.org")));
        }

        private static void RunDatabaseMonitors()
        {
            ConsoleMonitorRunner.Run("sql", TimeSpan.FromMinutes(5),
                new SqlServerRunningMonitor("server", "servername", "username", "password"),
                new SqlDatabaseRunningMonitor("db", "servername", "NuGetGallery", "username", "password"),
                new SqlDatabaseSizeMonitor("size", "servername", "NuGetGallery", "username", "password"),
                new SqlIndexFragmentationMonitor("indexfrag", "servername", "NuGetGallery", "username", "password"),
                new SqlQueryPlanMonitor("query", "servername", "NuGetGallery", "username", "password"),
                new SqlPrincipalAgeMonitor("principals", "servername", "username", "password"));
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
