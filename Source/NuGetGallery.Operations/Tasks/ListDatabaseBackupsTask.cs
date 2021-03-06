﻿using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations
{
    [Command("listdatabasebackups", "List database backups at the specified database server", AltName = "ldb")]
    public class ListDatabaseBackupsTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            var dbServer = Util.GetDbServer(ConnectionString);
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString);

            Log.Info("Listing backups for server '{0}':", dbServer);
            
            using (var sqlConnection = new SqlConnection(masterConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var dbs = dbExecutor.Query<Database>(
                    "SELECT name FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                    new { state = Util.OnlineState });

                foreach(var db in dbs)
                {
                    var timestamp = Util.GetDatabaseNameTimestamp(db);
                    var date = Util.GetDateTimeFromTimestamp(timestamp);

                    Log.Info("{0} ({1})", timestamp, date);
                }
            }
        }
    }
}
