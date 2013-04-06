using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ColorPair = System.Tuple<System.ConsoleColor, System.ConsoleColor?>;

namespace NuGetGallery.Monitoring
{
    public class SnazzyConsoleTarget : TraceListener
    {
        private static readonly Dictionary<TraceEventType, ColorPair> ColorTable = new Dictionary<TraceEventType, ColorPair>()
        {
            { TraceEventType.Error, new ColorPair(ConsoleColor.Red, null) },
            { TraceEventType.Critical, new ColorPair(ConsoleColor.White, ConsoleColor.Red) },
            { TraceEventType.Information, new ColorPair(ConsoleColor.Green, null) },
            { TraceEventType.Verbose, new ColorPair(ConsoleColor.DarkGray, null) },
            { TraceEventType.Warning, new ColorPair(ConsoleColor.Black, ConsoleColor.Yellow) }
        };

        private static readonly Dictionary<TraceEventType, string> LevelNames = new Dictionary<TraceEventType, string>() {
            { TraceEventType.Error, "error" },
            { TraceEventType.Critical, "fatal" },
            { TraceEventType.Information, "info" },
            { TraceEventType.Verbose, "trace" },
            { TraceEventType.Warning, "warn" },
        };

        private static readonly int LevelLength = LevelNames.Values.Max(s => s.Length);

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            TraceEvent(eventCache, source, eventType, id, String.Format(format, args));
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            message = String.Format(
                "({2})[{0}] {1}",
                DateTime.UtcNow.ToString("HH:mm:ss.ff"),
                message,
                source);

            var oldForeground = Console.ForegroundColor;
            var oldBackground = Console.BackgroundColor;

            // Get us to the start of a line
            if (Console.CursorLeft > 0)
            {
                Console.WriteLine();
            }

            // Get Color Pair colors
            ColorPair pair;
            if (!ColorTable.TryGetValue(eventType, out pair))
            {
                pair = new ColorPair(Console.ForegroundColor, Console.BackgroundColor);
            }
            
            // Get level string
            string levelName;
            if (!LevelNames.TryGetValue(eventType, out levelName))
            {
                levelName = eventType.ToString();
            }
            levelName = levelName.PadRight(LevelLength).Substring(0, LevelLength);

            // Break the message in to lines as necessary
            var existingLines = message.Split(new string[] {Environment.NewLine}, StringSplitOptions.None);
            var lines = new List<string>();
            foreach (var existingLine in existingLines)
            {
                var prefix = levelName + ": ";
                if (!String.IsNullOrEmpty(source))
                {
                    prefix += "(" + source + ")";
                }
                var fullMessage = prefix + existingLine;
                var maxWidth = Console.BufferWidth - 2;
                var currentLine = existingLine;
                while (fullMessage.Length > maxWidth)
                {
                    int end = maxWidth - prefix.Length;
                    int spaceIndex = currentLine.LastIndexOf(' ', Math.Min(end, message.Length - 1));
                    if (spaceIndex < 10)
                    {
                        spaceIndex = end;
                    }
                    lines.Add(currentLine.Substring(0, spaceIndex).Trim());
                    currentLine = currentLine.Substring(spaceIndex).Trim();
                    fullMessage = prefix + currentLine;
                }
                lines.Add(currentLine);
            }

            // Write lines
            bool first = true;
            foreach (var line in lines.Where(l => !String.IsNullOrWhiteSpace(l)))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Console.WriteLine();
                }

                // Write Level
                Console.ForegroundColor = pair.Item1;
                if (pair.Item2.HasValue)
                {
                    Console.BackgroundColor = pair.Item2.Value;
                }
                Console.Write(levelName);

                // Write the message using the default foreground color, but the specified background color
                // UNLESS: The background color has been changed. In which case the foreground color applies here too
                var foreground = pair.Item2.HasValue
                                        ? pair.Item1
                                        : oldForeground;
                Console.ForegroundColor = foreground;
                Console.Write(": " + line);
            }
            Console.ForegroundColor = oldForeground;
            Console.BackgroundColor = oldBackground;
            Console.WriteLine();
        }

        public override void Write(string message)
        {
            TraceEvent(null, null, TraceEventType.Verbose, id: 0, message: message); 
        }

        public override void WriteLine(string message)
        {
            TraceEvent(null, null, TraceEventType.Verbose, id: 0, message: message + Environment.NewLine);
        }
    }
}
