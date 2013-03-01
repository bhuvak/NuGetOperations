using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("createwarehousereports", "Create warehouse reports", AltName = "cwrep")]
    public class CreateWarehouseReportsTask : DatabaseTask
    {
        private const string JsonContentType = "application/json";
        private const string PackageReportBaseName = "recentpopularity_";
        private const string PerMonth = "permonth";
        private const string RecentPopularity = "recentpopularity";
        private const string RecentPopularityDetail = "recentpopularitydetail";
        private const string RecentPackageUpdates = "recentpackageupdates";

        [Option("Connection string to the warehouse database", AltName = "wdb")]
        public string WarehouseConnectionString { get; set; }

        [Option("Connection string to the warehouse reports container", AltName = "wracc")]
        public CloudStorageAccount ReportStorage { get; set; }

        public CreateWarehouseReportsTask()
        {
            WarehouseConnectionString = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
            var reportCs = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_REPORTS_STORAGE");
            if (!String.IsNullOrWhiteSpace(reportCs))
            {
                ReportStorage = CloudStorageAccount.Parse(reportCs);
            }
        }

        public override void ExecuteCommand()
        {
            Log.Info("Generate reports begin");

            CreateReport_PerMonth();
            CreateReport_RecentPopularityDetail();
            CreateReport_RecentPopularity();

            CreateReport_NewPackages();

            //TODO: comment this line back in when we want the "ten thousand reports" live
            //CreateAllPerPackageReports();

            Log.Info("Generate reports end");
        }

        private void CreateReport_PerMonth()
        {
            Log.Info("CreateReport_PerMonth");

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_PerMonth.sql");

            CreateBlob(PerMonth + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void CreateReport_RecentPopularityDetail()
        {
            Log.Info("CreateReport_RecentPopularityDetail");

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularityDetail.sql");

            CreateBlob(RecentPopularityDetail + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void CreateReport_RecentPopularity()
        {
            Log.Info("CreateReport_RecentPopularity");

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularity.sql", 100);

            CreateBlob(RecentPopularity + ".json", JsonContentType, ReportHelpers.ToJson(report));

            CreatePerPackageReports(report);
        }

        private void CreateReport_NewPackages()
        {
            //  packageId, packageVersion, created
            List<Tuple<string, string, DateTime>> newPackages = GetNewPackages();

            //  packageId, packageVersion, created, downloads-so-far
            List<Tuple<string, string, DateTime, int>> report = new List<Tuple<string, string, DateTime, int>>();

            int top = 0;

            do
            {
                top += 500;
                report.Clear();

                IDictionary<string, int> topPackages = GetTopPackages(top);

                foreach (Tuple<string, string, DateTime> newPackage in newPackages)
                {
                    int downloads = 0;
                    if (topPackages.TryGetValue(newPackage.Item1, out downloads))
                    {
                        report.Add(new Tuple<string, string, DateTime, int>(newPackage.Item1, newPackage.Item2, newPackage.Item3, downloads));
                    }
                }

                report.Sort((x, y) => { return (x.Item4 < y.Item4 ? -1 : 1); });
                report.Reverse();

                Log.Info(string.Format("CreateReport_NewPackages {0} new packages, {1} in the top {2} most popular", newPackages.Count, report.Count, top));
            }
            while (report.Count < 10);

            Stream content = ReportHelpers.RecentPackageUpdatesToJson(report);

            CreateBlob(RecentPackageUpdates + ".json", JsonContentType, content);
        }

        private List<Tuple<string, string, DateTime>> GetNewPackages()
        {
            // Note: this query hits the Gallery NOT the Warehouse

            string sql = ResourceHelper.GetBatchFromSqlFile("NuGetGallery.Operations.Scripts.PackageReport_NewPackages.sql");

            List<Tuple<string, string, DateTime>> result = new List<Tuple<string, string, DateTime>>();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string packageId = reader.GetValue(0).ToString();
                    string packageVersion = reader.GetValue(1).ToString();
                    DateTime created = reader.GetDateTime(2);

                    result.Add(new Tuple<string, string, DateTime>(packageId, packageVersion, created));
                }
            }

            return result;
        }

        private IDictionary<string, int> GetTopPackages(int top)
        {
            IDictionary<string, int> result = new Dictionary<string, int>();

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularity.sql", top);

            foreach (string[] item in report.Item2)
            {
                result[item[0]] = int.Parse(item[1]);
            }

            return result;
        }

        private void CreatePerPackageReports(Tuple<string[], List<string[]>> report)
        {
            Log.Info(string.Format("CreatePerPackageReports (count = {0})", report.Item2.Count));

            int indexOfPackageId = 0;
            foreach (string column in report.Item1)
            {
                if (column == "PackageId")
                {
                    break;
                }
                indexOfPackageId++;
            }

            if (indexOfPackageId == report.Item1.Length)
            {
                throw new InvalidOperationException("expected PackageId in result");
            }

            foreach (string[] row in report.Item2)
            {
                string packageId = row[indexOfPackageId];
                WithRetry(() =>
                {
                    CreatePackageReport(packageId);
                });
            }
        }

        private void CreateAllPerPackageReports()
        {
            Log.Info("CreateAllPerPackageReports");

            DateTime before = DateTime.Now;

            IList<Tuple<string, int>> packageIds = GetPackageIds();

            Log.Info(string.Format("Creating {0} Reports", packageIds.Count));

            ConcurrentBag<Tuple<string, int>> bag = new ConcurrentBag<Tuple<string, int>>();

            foreach (Tuple<string, int> packageId in packageIds)
            {
                bag.Add(packageId);
            }

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = 4 };

            Parallel.ForEach(bag, options, packageId =>
            {
                WithRetry(() =>
                {
                    CreatePackageReport(packageId.Item1);
                    ConfirmExport(packageId);
                });
            });

            string msg = string.Format("CreateAllPerPackageReports complete {0} seconds", (DateTime.Now - before).TotalSeconds);

            Log.Info(msg);
        }

        private IList<Tuple<string, int>> GetPackageIds()
        {
            IList<Tuple<string, int>> packageIds = new List<Tuple<string, int>>();

            using (SqlConnection connection = new SqlConnection(WarehouseConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("GetPackagesForExport", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string packageId = reader.GetValue(0).ToString();
                    int dirtyCount = (int)reader.GetValue(1);

                    packageIds.Add(new Tuple<string, int>(packageId, dirtyCount));
                }
            }

            return packageIds;
        }

        private void CreatePackageReport(string packageId)
        {
            Log.Info(string.Format("CreatePackageReport for {0}", packageId));

            // All blob names use lower case identifiers in the NuGet Gallery Azure Blob Storage 

            string name = PackageReportBaseName + packageId.ToLowerInvariant();

            Tuple<string[], List<string[]>> report = ExecuteSql("NuGetGallery.Operations.Scripts.DownloadReport_RecentPopularityByPackage.sql", null, new Tuple<string, string>("@packageId", packageId));

            CreateBlob(name + ".json", JsonContentType, ReportHelpers.ToJson(report));
        }

        private void ConfirmExport(Tuple<string, int> packageId)
        {
            Log.Info(string.Format("ConfirmPackageExported for {0}", packageId.Item1));

            using (SqlConnection connection = new SqlConnection(WarehouseConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("ConfirmPackageExported", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60 * 5;
                command.Parameters.AddWithValue("PackageId", packageId.Item1);
                command.Parameters.AddWithValue("DirtyCount", packageId.Item2);

                command.ExecuteNonQuery();
            }
        }

        private Tuple<string[], List<string[]>> ExecuteSql(string filename, int? top = null, params Tuple<string, string>[] parameters)
        {
            string sql = ResourceHelper.GetBatchFromSqlFile(filename);

            List<string[]> rows = new List<string[]>();
            string[] columns;

            using (SqlConnection connection = new SqlConnection(WarehouseConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                if (top != null)
                {
                    command.Parameters.Add("@top", top);
                }

                foreach (Tuple<string, string> parameter in parameters)
                {
                    command.Parameters.AddWithValue(parameter.Item1, parameter.Item2);
                }

                SqlDataReader reader = command.ExecuteReader();

                columns = new string[reader.FieldCount];

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns[i] = reader.GetName(i);
                }

                while (reader.Read())
                {
                    string[] row = new string[reader.FieldCount];

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.GetValue(i).ToString();
                    }

                    rows.Add(row);
                }
            }

            return new Tuple<string[], List<string[]>>(columns, rows);
        }

        private Uri CreateBlob(string name, string contentType, Stream content)
        {
            CloudBlobClient blobClient = ReportStorage.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("popularity");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(name);

            blockBlob.Properties.ContentType = contentType;
            blockBlob.UploadFromStream(content);

            return blockBlob.Uri;
        }

        private void WithRetry(Action action)
        {
            int attempts = 10;

            while (attempts-- > 0)
            {
                try
                {
                    action();
                    break;
                }
                catch (Exception e)
                {
                    if (attempts == 1)
                    {
                        throw e;
                    }
                    else
                    {
                        SqlConnection.ClearAllPools();
                        Log.Info(string.Format("Retry attempts remaining {0}", attempts));
                        Thread.Sleep(20 * 1000);
                    }
                }
            }
        }
    }
}
