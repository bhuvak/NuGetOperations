using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.Sql
{
    public class SqlPrincipalAgeMonitor : SqlDatabaseMonitorBase
    {
        public static readonly TimeSpan DefaultFailureThreshold = TimeSpan.FromDays(30);
        public static readonly TimeSpan DefaultDegradedThreshold = TimeSpan.FromDays(15);

        // See http://msdn.microsoft.com/en-us/library/ms188786.aspx
        private const string PrincipalsQuery = @"SELECT p.name, p.modify_date FROM sys.sql_logins p";

        public TimeSpan DegradedThreshold { get; set; }
        public TimeSpan FailureThreshold { get; set; }

        public SqlPrincipalAgeMonitor(string name, string server, string user, string password)
            : base(name, server, "master", user, password)
        {
            DegradedThreshold = DefaultDegradedThreshold;
            FailureThreshold = DefaultFailureThreshold;
        }

        protected override async Task Invoke()
        {
            await Connect(async c =>
            {
                // Calculate index fragmentation
                var command = new SqlCommand(PrincipalsQuery, c);
                command.CommandType = CommandType.Text;
                command.Parameters.Add(new SqlParameter("@DbName", Database));

                // Execute the command
                var reader = await command.ExecuteReaderAsync(CancelToken);

                // Read the results
                while (reader.Read())
                {
                    string principalName = reader.GetString(0);
                    DateTime lastModified = reader.GetDateTime(1);

                    var span = DateTime.UtcNow - lastModified;
                    bool success = false;
                    if (span < DefaultDegradedThreshold)
                    {
                        success = true;
                        Success("Principal has not yet expired.", principalName);
                    }
                    else if (span < DefaultFailureThreshold)
                    {
                        Degraded("Principal is nearing expiry. Update the password soon.", principalName);
                    }
                    else
                    {
                        Failure("Principal has expired! Update the password.", principalName);
                    }
                    QoS("Age", success, span, principalName);
                }
            });
        }
    }
}
