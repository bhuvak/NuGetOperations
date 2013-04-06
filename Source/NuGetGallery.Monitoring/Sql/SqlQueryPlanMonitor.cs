using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.Sql
{
    public class SqlQueryPlanMonitor : SqlDatabaseMonitorBase
    {
        public static readonly double DefaultFailureThreshold = 0;
        public static readonly double DefaultDegradedThreshold = 0;

        // From: http://msdn.microsoft.com/en-us/library/windowsazure/ff394114.aspx
        private const string QueryPlanQuery = @"
            -- Monitor query plans
            SELECT
                highest_cpu_queries.max_worker_time, 
                highest_cpu_queries.total_worker_time / highest_cpu_queries.execution_count,
                q.[text] 
            FROM 
                (SELECT TOP 5  
                    qs.plan_handle,  
                    qs.max_worker_time,
                    qs.total_worker_time,
                    qs.execution_count
                 FROM 
                    sys.dm_exec_query_stats qs
                 WHERE qs.execution_count > 10 -- Leave out queries that only got executed a few times
                 ORDER BY qs.max_worker_time desc) AS highest_cpu_queries 
                 CROSS APPLY sys.dm_exec_sql_text(plan_handle) AS q 
            WHERE q.dbid = DB_ID(@DbName)
            ORDER BY highest_cpu_queries.max_worker_time desc";

        public double DegradedThreshold { get; set; }
        public double FailureThreshold { get; set; }

        public SqlQueryPlanMonitor(string name, string server, string database, string user, string password)
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
                var command = new SqlCommand(QueryPlanQuery, c);
                command.CommandType = CommandType.Text;
                command.Parameters.Add(new SqlParameter("@DbName", Database));

                // Execute the command
                var reader = await command.ExecuteReaderAsync(CancelToken);

                // Read the results
                while (reader.Read())
                {
                    long maxTimeUS = reader.GetInt64(0);
                    long avgTimeUS = reader.GetInt64(1);
                    string query = reader.GetString(2);

                    TimeSpan maxTime = TimeSpan.FromMilliseconds(0.001 * maxTimeUS);
                    TimeSpan avgTime = TimeSpan.FromMilliseconds(0.001 * avgTimeUS);
                    
                    QoS("Query Plan Max Time", success: true, value: (int)Math.Ceiling(maxTime.TotalMilliseconds), resource: query);
                    QoS("Query Plan Avg Time", success: true, value: (int)Math.Ceiling(avgTime.TotalMilliseconds), resource: query);
                }
            });
        }
    }
}
