﻿using System;
using System.Data.SqlClient;
using System.Threading;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("copydatabasebackup", "Copy the specified database backup from the source to the destination", AltName = "cdb", MaxArgs=0)]
    public class CopyDatabaseBackupTask : OpsTask
    {
        [Option("Connection string to the source database server", AltName = "s")]
        public string SourceConnectionString { get; set; }

        [Option("Connection string to the destination database server", AltName = "d")]        
        public string DestinationConnectionString { get; set; }

        [Option("Name of the backup file", AltName = "n")]
        public string BackupName { get; set; }

        public CopyDatabaseBackupTask()
        {
            // Get default from environment
            SourceConnectionString = Environment.GetEnvironmentVariable("NUGET_GALLERY_BACKUP_SOURCE_CONNECTION_STRING");
            DestinationConnectionString = Environment.GetEnvironmentVariable("NUGET_GALLERY_MAIN_CONNECTION_STRING");
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.RequiredOrEnv(SourceConnectionString, "SourceConnectionString", "NUGET_GALLERY_BACKUP_SOURCE_CONNECTION_STRING");
            ArgCheck.RequiredOrEnv(DestinationConnectionString, "SourceConnectionString", "NUGET_GALLERY_MAIN_CONNECTION_STRING");
            ArgCheck.Required(BackupName, "BackupName");
        }

        public override void ExecuteCommand()
        {
            using (var destinationConnection = new SqlConnection(Util.GetMasterConnectionString(DestinationConnectionString)))
            using (var destinationDbExecutor = new SqlExecutor(destinationConnection))
            {
                string sourceDbServerName = Util.GetDatabaseServerName(SourceConnectionString);
                string destinationDbServerName = Util.GetDatabaseServerName(DestinationConnectionString);
                
                destinationConnection.Open();

                var copyDbName = string.Format("CopyOf{0}", BackupName);
                
                var existingDatabaseBackup = Util.GetDatabase(
                    destinationDbExecutor,
                    copyDbName);
                
                if (existingDatabaseBackup != null && existingDatabaseBackup.State == Util.OnlineState)
                {
                    Log.Info("Skipping {0}. It already exists on {1} and is online.", copyDbName, destinationDbServerName);
                    return;
                }

                if (existingDatabaseBackup == null)
                {
                    StartBackupCopy(
                        destinationDbExecutor,
                        sourceDbServerName,
                        destinationDbServerName,
                        BackupName,
                        copyDbName);

                    Log.Trace("Waiting 15 minutes for copy of {0} from {1} to {2} to complete.", BackupName, sourceDbServerName, destinationDbServerName);
                    if (!WhatIf)
                    {
                        Thread.Sleep(15 * 60 * 1000);
                    }
                }

                WaitForBackupCopy(
                    destinationDbExecutor,
                    destinationDbServerName,
                    copyDbName);
            }
        }

        private void StartBackupCopy(
            IDbExecutor dbExecutor, 
            string sourceDbServerName,
            string destinationDbServerName,
            string sourceDbName,
            string copyDbName)
        {
            Log.Trace("Starting copy of {0} from {1} to {2}.", sourceDbName, sourceDbServerName, destinationDbServerName);
            if (!WhatIf)
            {
                var sql = string.Format("CREATE DATABASE {0} AS COPY OF {1}.{2}", copyDbName, sourceDbServerName, sourceDbName);
                dbExecutor.Execute(sql);
            }
            Log.Info("Copying {0} from {1} to {2}.", sourceDbName, sourceDbServerName, destinationDbServerName);
        }

        private void WaitForBackupCopy(
            SqlExecutor dbExecutor,
            string destinationDbServerName,
            string copyDbName)
        {
            var timeToGiveUp = DateTime.UtcNow.AddHours(1).AddSeconds(30);
            while (DateTime.UtcNow < timeToGiveUp)
            {
                if (WhatIf || Util.DatabaseExistsAndIsOnline(
                    dbExecutor,
                    copyDbName))
                {
                    Log.Info("Copied {0} to {1}.", copyDbName, destinationDbServerName);
                    return;
                }

                Log.Trace("Database {0} on {1} is not yet ready and online. Waiting for 5 minutes (will give up in {2} minutes).", copyDbName, destinationDbServerName, Math.Round(timeToGiveUp.Subtract(DateTime.UtcNow).TotalMinutes));
                Thread.Sleep(5 * 60 * 1000);
            }
        }
    }
}
