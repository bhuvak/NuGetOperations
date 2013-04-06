using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.Sql
{
    public class SqlIndexFragmentationMonitor : SqlDatabaseMonitorBase
    {
        public static readonly double DefaultFailureThreshold = 30.0;
        public static readonly double DefaultDegradedThreshold = 5.0;

        private const string FragmentationQuery = @"
            SELECT OBJECT_NAME(stats.object_id),
                   idx.name,
                   stats.avg_fragmentation_in_percent 
            FROM sys.dm_db_index_physical_stats(DB_ID(@DbName), NULL, NULL, NULL, NULL) AS stats
                INNER JOIN sys.indexes AS idx ON stats.object_id = idx.object_id AND stats.index_id = idx.index_id";

        public double DegradedThreshold { get; set; }
        public double FailureThreshold { get; set; }

        public SqlIndexFragmentationMonitor(string name, string server, string database, string user, string password)
            : base(name, server, database, user, password)
        {
            DegradedThreshold = DefaultDegradedThreshold;
            FailureThreshold = DefaultFailureThreshold;
        }

        protected override async Task Invoke()
        {
            await Connect(async c =>
            {
                // Calculate index fragmentation
                var command = new SqlCommand(FragmentationQuery, c);
                command.CommandType = CommandType.Text;
                command.Parameters.Add(new SqlParameter("@DbName", Database));

                // Execute the command
                var reader = await command.ExecuteReaderAsync(CancelToken);

                // Read the results
                while (reader.Read())
                {
                    string objectName = reader.GetString(0);
                    string indexName = reader.GetString(1);
                    double fragmentation = reader.GetDouble(2);

                    string resource = objectName + "." + indexName;

                    bool success = false;
                    if (fragmentation < DefaultDegradedThreshold)
                    {
                        success = true;
                        Success("Fragmentation is OK", resource);
                    }
                    else if (fragmentation < DefaultFailureThreshold)
                    {
                        Degraded("Fragmentation is Degraded", resource);
                    }
                    else
                    {
                        Failure("Fragmentation is Bad", resource);
                    }
                    QoS("Fragmentation", success, value: (int)Math.Ceiling(fragmentation), resource: resource);
                }
            });
        }
    }
}
