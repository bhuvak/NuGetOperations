using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.Http
{
    public class HttpMonitor : ApplicationMonitor
    {
        public static readonly TimeSpan DefaultHardTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets the URL to the target to ping.
        /// </summary>
        public HttpPingTarget Target { get; private set; }

        /// <summary>
        /// Gets the URL to a known-good site that can be used to test network connectivity issues
        /// </summary>
        public HttpPingTarget KnownGoodSite { get; private set; }

        /// <summary>
        /// Gets or sets the timeout to abort the entire request after. Default is 1 minute (<see cref="DefaultHardTimeout"/>).
        /// </summary>
        public TimeSpan HardTimeout { get; set; }

        protected override string DefaultResourceName
        {
            get
            {
                return Target.Url.AbsoluteUri;
            }
        }

        public HttpMonitor(string name, HttpPingTarget target)
            : this(name, target, null)
        {
        }

        public HttpMonitor(string name, HttpPingTarget target, HttpPingTarget knownGoodSite) : base(name)
        {
            Target = target;
            KnownGoodSite = knownGoodSite;
            HardTimeout = DefaultHardTimeout;
        }

        protected override async Task Invoke()
        {
            FlushDnsCache();
            
            // Ping both the target and the known good
            Task<HttpPingResult> target = Ping(Target, p => ReportUnhealthy(p));
            Task<HttpPingResult> knownGood = Ping(KnownGoodSite);
            var completed = await TaskEx.WhenAny(target, knownGood);

            // Known good finished first? Wait for target.
            if(completed == knownGood) 
            {
                await target;
            }
            
            // Don't care about known good, target succeeded!
            if (completed == target && completed.Result.Result == EventType.Success)
            {
                ReportSuccess(completed.Result);
            }
            // Either target failed or hasn't finished yet
            else
            {
                // Wait for everything
                await TaskEx.WhenAll(target, knownGood);
                if (target.Result.Result == EventType.Success)
                {
                    ReportSuccess(target.Result);
                }
                else if (knownGood.Result.Result == EventType.Success)
                {
                    ReportFailure(target.Result);
                }
                else
                {
                    MonitorFailure("Known Good Site Failed.");
                }
            }
        }

        private void ReportFailure(HttpPingResult result)
        {
            Failure(result.Message);
            QoS(result.Message, success: false, timeTaken: result.TimeToCompletion);
        }

        private void ReportSuccess(HttpPingResult result)
        {
            Success(result.Message);
            QoS(result.Message, success: true, timeTaken: result.TimeToCompletion);
        }

        private void ReportUnhealthy(HttpPingTarget target)
        {
            Unhealthy("Expected Timeout Elapsed. This may indicate a failure, but the site is not known to be down yet.");
        }

        private async Task<HttpPingResult> Ping(HttpPingTarget target, Action<HttpPingTarget> reportUnhealthy = null)
        {
            // Give me a ping Vasily. One ping only.
            var requestResult = await Time(() =>
            {
                var client = new HttpClient(new WebRequestHandler() { 
                    CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache) 
                });
                var request = new HttpRequestMessage(new HttpMethod(target.Method), target.Url);
                
                // Wait for the request OR the expected timeout
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                Trace.Verbose("-> HTTP {0} {1}", target.Method, target.Url.AbsoluteUri);
                var requestTask = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, tokenSource.Token);
                if (requestTask.Wait(target.ExpectedTimeout))
                {
                    return TaskEx.FromResult(CheckRequest(target, requestTask));
                }
                Trace.Warning("Soft timeout fired: {0}ms", target.ExpectedTimeout.TotalMilliseconds);

                // Intermediate timeout expired. Report unhealthy and keep waiting
                if(reportUnhealthy != null) {
                    reportUnhealthy(target);
                }
                
                // Still want a timeout.
                try
                {
                    if (requestTask.Wait(HardTimeout))
                    {
                        return TaskEx.FromResult(CheckRequest(target, requestTask));
                    }
                }
                catch (Exception ex)
                {
                    return TaskEx.FromResult(Tuple.Create(
                        String.Format("Request failed due to exception: {0}", ex.GetBaseException()),
                        EventType.Failure));
                }
                tokenSource.Cancel();
                string message = String.Format("Hard timeout fired: {0}", HardTimeout.ToString());
                Trace.Error(message);
                return TaskEx.FromResult(Tuple.Create(message, EventType.Failure));
            });

            if (!requestResult.IsSuccess)
            {
                return new HttpPingResult(requestResult.Exception, requestResult.Time, target);
            }
            else
            {
                return new HttpPingResult(requestResult.Result.Item2, requestResult.Result.Item1, requestResult.Time, target);
            }
        }

        private Tuple<string, EventType> CheckRequest(HttpPingTarget target, Task<HttpResponseMessage> requestTask)
        {
            // We should be completed at this point...
            Debug.Assert(requestTask.IsCompleted);
            var response = requestTask.Result;
            Trace.Verbose("<- HTTP {0} {1}", (int)response.StatusCode, response.ReasonPhrase);
                    
            var message = String.Format("HTTP {0} {1}", (int)response.StatusCode, response.ReasonPhrase);

            if (target.ExpectedStatusCode != null)
            {
                return response.StatusCode == target.ExpectedStatusCode.Value ?
                    Tuple.Create(message, EventType.Success) :
                    Tuple.Create(message, EventType.Failure);
            }
            else
            {
                // Don't count 3XX as success since we need to follow redirects... (should already be doing that though)
                return response.IsSuccessStatusCode ?
                    Tuple.Create(message, EventType.Success) :
                    Tuple.Create(message, EventType.Failure);
            }
        }

        private void FlushDnsCache()
        {
            NativeMethods.DnsFlushResolverCache();
        }
    }
}
