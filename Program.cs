using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SubsNorm
{
    class Program
    {
        static List<string> output = new List<string>();
        static void ProcessLine(string sub)
        {
            Regex subPattern = new Regex("Dialogue: 0,(?<hs>\\d+):(?<ms>\\d+):(?<ss>\\d+).(?<mss>\\d+),"
                +"(?<he>\\d+):(?<me>\\d+):(?<se>\\d+).(?<mse>\\d+),Default,,0,0,0,,(?<text>.+)");
            var match = subPattern.Match(sub);
            if (!match.Success)
            {
                Console.WriteLine("Failed to parse a line");
                return;
            }

            var startTime = new DateTime(2000, 1, 1, int.Parse(match.Groups["hs"].Value)
                , int.Parse(match.Groups["ms"].Value)
                , int.Parse(match.Groups["ss"].Value)
                , int.Parse(match.Groups["mss"].Value) * 10);
            var endTime = new DateTime(2000, 1, 1, int.Parse(match.Groups["he"].Value)
                , int.Parse(match.Groups["me"].Value)
                , int.Parse(match.Groups["se"].Value)
                , int.Parse(match.Groups["mse"].Value) * 10);
            var text = match.Groups["text"].Value;
            var symbolsOnly = text.Replace("\\N", "").Count(ch => Char.IsLetterOrDigit(ch));
            var length = endTime - startTime;
            var symbolsPerSecond = symbolsOnly / length.TotalSeconds;

            var items = text.Split("\\N\\N");
            foreach (var item in items)
            {
                int subtitleLength = item.Replace("\\N", "").Count(ch => Char.IsLetterOrDigit(ch));
                startTime = PrintSubtitle(startTime, symbolsPerSecond, item, subtitleLength);
            }
        }

        static void Main(string[] args)
        {
            var sub = args[0];
            var lines = System.IO.File.ReadAllLines(args[0]);
            foreach (var line in lines)
            {
                if (line.Contains("\\N\\N"))
                {
                    ProcessLine(line);
                }
                else
                    output.Add(line);
            }
            System.IO.File.WriteAllLines("output.ass", output);
        }

        private static DateTime PrintSubtitle(DateTime startTime, double symbolsPerSecond, string subtitleText, int subtitleLength)
        {
            var start = FormatDateTime(startTime);
            var endTime = startTime.AddSeconds((double)subtitleLength / symbolsPerSecond);
            var end = FormatDateTime(endTime);
            output.Add($"Dialogue: 0,{start},{end},Default,,0,0,0,,{subtitleText}");
            return endTime;
        }

        private static string FormatMilliseconds(int ms)
        {
            return ms.ToString("000");
        }

        private static string FormatDateTime(DateTime dt)
        {
            return dt.ToString("H:mm:ss.") + FormatMilliseconds(dt.Millisecond);
        }
    }
}
