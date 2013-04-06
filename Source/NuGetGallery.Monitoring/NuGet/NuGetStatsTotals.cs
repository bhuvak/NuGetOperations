using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Monitoring.NuGet
{
    public class NuGetStatsTotals
    {
        public string Downloads { get; set; }
        public string UniquePackages { get; set; }
        public string TotalPackages { get; set; }
    }
}
