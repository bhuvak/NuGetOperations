using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.NuGet
{
    public class DownloadStatsMonitor : ApplicationMonitor
    {
        public Uri WebRoot { get; private set; }

        public DownloadStatsMonitor(string name, Uri webRoot) : base(name)
        {
            WebRoot = webRoot;
        }

        protected override async Task Invoke()
        {
            var client = HttpClientFactory.Create(new WebRequestHandler()
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache)
            });
            var target = new UriBuilder(WebRoot.AbsoluteUri) 
            {
                Path = "/stats/totals"
            }.Uri;
            var request = new HttpRequestMessage(HttpMethod.Get, target);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("NuGetMonitoring", typeof(DownloadStatsMonitor).Assembly.GetName().Version.ToString()));
            var response = await client.GetAsync(target);

            if (!response.IsSuccessStatusCode)
            {
                Failure("Error connecting to stats endpoint", target.AbsoluteUri);
            }

            var result = await response.Content.ReadAsAsync<NuGetStatsTotals>();
            int downloads = Int32.Parse(result.Downloads.Replace(",", ""));
            int uniques = Int32.Parse(result.UniquePackages.Replace(",", ""));
            int totals = Int32.Parse(result.TotalPackages.Replace(",", ""));

            QoS("Total Downloads", success: true, value: downloads);
            QoS("Unique Packages", success: true, value: uniques);
            QoS("Total Packages", success: true, value: totals);
        }
    }
}
