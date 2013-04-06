using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class MonitorThread
    {
        private ITrace _trace;

        public MonitorSet Set { get; private set; }
        public ApplicationMonitor Monitor { get; private set; }
        public IEventReporter Reporter { get; private set; }
        public CancellationToken CancelToken { get; private set; }
        public TimeSpan Period { get; private set; }
        public Task Task { get; private set; }
        
        public MonitorThread(TimeSpan period, MonitorSet set, IEventReporter reporter, ApplicationMonitor monitor, CancellationToken cancelToken)
        {
            Set = set;
            Reporter = reporter;
            Monitor = monitor;
            CancelToken = cancelToken;
            Period = period;

            _trace = new TraceSourceTrace(set.Name + ":" + monitor.Name);

            Task = Task.Factory.StartNew(Run, CancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private void Run()
        {
            while (!CancelToken.IsCancellationRequested)
            {
                _trace.Info("Cycle Started");
                try
                {
                    Monitor.Invoke(
                        reporter: Reporter,
                        cancelToken: CancelToken,
                        trace: _trace).Wait();
                } 
                catch (Exception ex)
                {
                    _trace.Error("Unhandled Monitor Exception: \n{0}", ex);
                }
                _trace.Info("Cycle Complete, waiting for {0}", Period);
                TaskEx.Delay(Period).Wait();
            }
        }
    }
}
