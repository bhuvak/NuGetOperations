using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class TraceEventReporter : IEventReporter
    {
        private static Dictionary<EventType, TraceEventType> _eventTypeMap = new Dictionary<EventType, TraceEventType>()
        {
            { EventType.Degraded, TraceEventType.Warning },
            { EventType.Failure, TraceEventType.Critical },
            { EventType.MonitorFailure, TraceEventType.Error },
            { EventType.Unhealthy, TraceEventType.Warning },
            { EventType.Success, TraceEventType.Information }
        };

        private object _consoleLock = new object();
        private TraceSource _source;

        public TraceEventReporter(TraceSource source)
        {
            _source = source;
        }

        public void Report(MonitoringEvent evt)
        {
            lock (_consoleLock)
            {
                switch (evt.Type)
                {
                    case EventType.QualityOfService:
                        HandleQoSEvent(evt);
                        break;
                    default:
                        HandleEvent(evt);
                        break;
                }
            }
        }

        private void HandleQoSEvent(MonitoringEvent evt)
        {
            _source.TraceEvent(TraceEventType.Information, id: 0, message: FormatQoSEvent((MonitoringQoSEvent)evt));
        }

        private string FormatEvent(MonitoringEvent evt)
        {
            return String.Format("{0}:{1}", evt.Resource, evt.Action);
        }

        private string FormatQoSEvent(MonitoringQoSEvent evt)
        {
            var qosMessage = FormatQoS(evt);
            return String.Format("{0}:{1}{2}", evt.Resource, evt.Action, String.IsNullOrEmpty(qosMessage) ? String.Empty : (" " + qosMessage));
        }

        private void HandleEvent(MonitoringEvent evt)
        {
            TraceEventType type;
            if (!_eventTypeMap.TryGetValue(evt.Type, out type))
            {
                type = TraceEventType.Verbose;
            }
            _source.TraceEvent(type, id: 0, message: FormatEvent(evt));
        }

        private string FormatQoS(MonitoringQoSEvent evt)
        {
            if (evt.Value is TimeSpan)
            {
                return String.Format("Time Taken: {0}", FormatTime((TimeSpan)evt.Value));
            }
            else if (evt.Value is int)
            {
                return String.Format("Value: {0}", (int)evt.Value);
            }
            return String.Empty;
        }

        private object FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds < 1.0)
            {
                return timeSpan.Milliseconds.ToString() + "ms";
            }
            else if (timeSpan.TotalMinutes < 1.0)
            {
                return Math.Round(timeSpan.TotalSeconds, 2).ToString() + "s";
            }
            else if (timeSpan.TotalHours < 1.0)
            {
                return Math.Round(timeSpan.TotalMinutes, 2).ToString() + "min";
            }
            else if (timeSpan.TotalDays < 1.0)
            {
                return Math.Round(timeSpan.TotalHours, 2).ToString() + "hrs";
            }
            return Math.Round(timeSpan.TotalDays, 2).ToString() + "d";
        }
    }
}
