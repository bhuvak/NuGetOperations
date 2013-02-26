using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace NuGetGallery.Operations.Common
{
    static class ReportHelpers
    {
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

            MemoryStream stream = new MemoryStream();
            TextWriter writer = new StreamWriter(stream);
            writer.Write(jArray.ToString());
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        //TODO: consider replacing the above function with the follow everywhere

        public static Stream ToJson(string col1, string col2, string col3, List<Tuple<string, string, int>> rows)
        {
            JArray jArray = new JArray();

            foreach (Tuple<string, string, int> row in rows)
            {
                JObject jObject = new JObject();
                jObject.Add(col1, row.Item1);
                jObject.Add(col2, row.Item2);
                jObject.Add(col3, row.Item3);

                jArray.Add(jObject);
            }

            MemoryStream stream = new MemoryStream();
            TextWriter writer = new StreamWriter(stream);
            writer.Write(jArray.ToString());
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
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
