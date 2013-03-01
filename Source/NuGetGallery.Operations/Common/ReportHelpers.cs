using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace NuGetGallery.Operations.Common
{
    static class ReportHelpers
    {
        private static Stream ToStream(JToken jToken)
        {
            MemoryStream stream = new MemoryStream();
            TextWriter writer = new StreamWriter(stream);
            writer.Write(jToken.ToString());
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static Stream ToJson(Tuple<string[], List<string[]>> report)
        {
            JArray jArray = new JArray();

            foreach (string[] row in report.Item2)
            {
                JObject jObject = new JObject();

                for (int i = 0; i < report.Item1.Length; i++)
                {
                    jObject.Add(report.Item1[i], row[i]);
                }

                jArray.Add(jObject);
            }

            return ToStream(jArray);
        }

        public static Stream RecentPackageUpdatesToJson(List<Tuple<string, string, DateTime, int>> rows)
        {
            JArray jArray = new JArray();

            foreach (Tuple<string, string, DateTime, int> row in rows)
            {
                JObject jObject = new JObject();
                jObject.Add("PackageId", row.Item1);
                jObject.Add("PackageVersion", row.Item2);
                jObject.Add("Created", row.Item3);
                // the last column here was just for ordering, we don't want it in the report because the number is confusing

                jArray.Add(jObject);
            }

            return ToStream(jArray);
        }

        public static void Dump(Stream json)
        {
            StreamReader reader = new StreamReader(json);
            string s = reader.ReadToEnd();

            object parsedJson = JsonConvert.DeserializeObject(s);
            string prettyJson = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);

            Console.WriteLine(prettyJson);
        }
    }
}
