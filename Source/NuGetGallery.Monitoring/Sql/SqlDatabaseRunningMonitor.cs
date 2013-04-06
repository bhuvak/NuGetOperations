using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.Sql
{
    public class SqlDatabaseRunningMonitor : SqlDatabaseMonitorBase
    {
        public SqlDatabaseRunningMonitor(string name, string server, string database, string user, string password) : base(name, server, database, user, password) { }

        protected override Task Invoke()
        {
            // Just connect and time the connection
            return Connect(_ =>
            {
                Success("Connected to the SQL Database");
                QoS("Connected to the SQL Database", success: true, timeTaken: TimeToConnect);
            });
        }
    }
}
