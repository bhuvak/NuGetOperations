using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("countblobs", "Counts blobs in the specified storage account", AltName = "cb", MaxArgs = 0)]
    public class CountBlobsTask : OpsTask
    {
        [Option("A connection string for the storage account to count blobs in", AltName = "s")]
        public CloudStorageAccount StorageAccount { get; set; }

        [Option("A path prefix to apply in order to filter blobs to count (format: container/folder/subfolder/...). Required if -All not set", AltName="p")]
        public string Prefix { get; set; }

        [Option("Set this switch to count EVERY blob in the account", AltName = "A")]
        public bool All { get; set; }

        public override void ValidateArguments()
        {
            ArgCheck.Required(StorageAccount, "StorageAccount");
        }

        public override void ExecuteCommand()
        {
            if (!All && String.IsNullOrEmpty(Prefix))
            {
                Log.Error("You must either specify a prefix with -Prefix or explicitly indicate that you want to count ALL blobs by passing -All");
                return;
            }

            Log.Info("Counting {1} in {0}", StorageAccount.Credentials.AccountName, All ? "All Blobs" : ("Blobs starting with '" + Prefix + "'"));

            var client = StorageAccount.CreateCloudBlobClient();
            ulong totalSize = 0;
            ulong pageBlobCount = 0;
            ulong blockBlobCount = 0;
            ulong blobCount = 0;
            ulong? max = null;
            Uri maxUrl = null;
            ulong? min = null;
            Uri minUrl = null;

            IterateSegmented(client, blob =>
            {
                var blockBlob = blob as CloudBlockBlob;
                ulong? thisBlobSize = null;
                if (blockBlob != null)
                {
                    thisBlobSize = (ulong)blockBlob.Properties.Length;
                    blockBlobCount++;
                }
                else
                {
                    var pageBlob = blob as CloudPageBlob;
                    if (pageBlob != null)
                    {
                        thisBlobSize = (ulong)pageBlob.Properties.Length;
                        pageBlobCount++;
                    }
                }
                if (thisBlobSize != null)
                {
                    blobCount++;
                    totalSize += thisBlobSize.Value;

                    if (max == null || thisBlobSize > max.Value)
                    {
                        max = thisBlobSize;
                        maxUrl = blob.Uri;
                    }
                    if (min == null || thisBlobSize < min.Value)
                    {
                        min = thisBlobSize;
                        minUrl = blob.Uri;
                    }
                }
            });

            // Write the data
            Log.Info("Count Complete.");
            Log.Info("Total Blob Size: {0}", FormatSize(totalSize));
            Log.Info("Total Blob Count: {0}", blobCount);
            Log.Info("    {0} Block Blobs", blockBlobCount);
            Log.Info("    {0} Page Blobs", pageBlobCount);
            Log.Info("Average Size: {0}/blob", FormatSize(totalSize / blobCount));
            Log.Info("Largest Blob: {0} @ {1}", maxUrl.AbsolutePath, FormatSize(max.Value));
            Log.Info("Smallest Blob: {0} @ {1}", minUrl.AbsolutePath, FormatSize(min.Value));
        }

        private void IterateSegmented(CloudBlobClient client, Action<IListBlobItem> action)
        {
            BlobContinuationToken token = null;
            ulong processCount = 0;
            do
            {
                var segment = client.ListBlobsSegmented(Prefix, token);
                token = segment.ContinuationToken;

                foreach (var item in segment.Results)
                {
                    action(item);
                    processCount++;
                    if (processCount > 0 && (processCount % 10000) == 0)
                    {
                        Log.Info("Processed {0} blobs...", processCount);
                    }
                }
            } while (token != null);
        }

        const ulong MaxBytes = 1024;
        const ulong MaxKB = MaxBytes * 1024;
        const ulong MaxMB = MaxKB * 1024;
        const ulong MaxGB = MaxMB * 1024;
        const ulong MaxTB = MaxGB * 1024;
        private string FormatSize(double size)
        {
            // Try Bytes
            if (size <= MaxBytes)
            {
                return String.Format("{0:D} bytes", size);
            }
            else if (size <= MaxKB)
            {
                return String.Format("{0:F2}KB", size / MaxBytes);
            }
            else if (size <= MaxMB)
            {
                return String.Format("{0:F2}MB", size / MaxKB);
            }
            else if (size <= MaxGB)
            {
                return String.Format("{0:F2}GB", size / MaxMB);
            }
            else if (size <= MaxTB)
            {
                return String.Format("{0:F2}TB", size / MaxGB);
            }
            else
            {
                return String.Format("{0:F2}PB", size / MaxTB);
            }
        }
    }
}
