using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    /// <summary>
    /// A collection of monitors to run at a specific interval
    /// </summary>
    public class MonitorSet
    {
        /// <summary>
        /// Gets the name of this monitor set
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the time between invocations of the monitor operations
        /// </summary>
        public TimeSpan Period { get; private set; }

        /// <summary>
        /// Gets the monitors to run
        /// </summary>
        public IEnumerable<ApplicationMonitor> Monitors { get; private set; }

        public MonitorSet(string name, TimeSpan period, params ApplicationMonitor[] monitors) : this(name, period, (IEnumerable<ApplicationMonitor>)monitors) { }
        public MonitorSet(string name, TimeSpan period, IEnumerable<ApplicationMonitor> monitors)
        {
            Name = name;
            Period = period;
            Monitors = monitors;
        }

        /// <summary>
        /// Starts running the monitor.
        /// </summary>
        /// <param name="reporter">An object used to report status to the underlying monitoring infrastructure</param>
        /// <param name="cancelToken">A token used to cancel the monitoring operation</param>
        /// <returns>A task that completes when the monitor shuts down (i.e. cancelToken is cancelled)</returns>
        public virtual async Task Run(IEventReporter reporter, CancellationToken cancelToken)
        {
            // Create a worker thread for each monitor
            var threads = Monitors.Select(monitor => new MonitorThread(Period, this, reporter, monitor, cancelToken));

            // Run each thread until cancelled
            await TaskEx.WhenAll(threads.Select(t => t.Task));
        }
    }
}
