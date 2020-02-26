using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SubsNorm
{
    [Verb("normilize", HelpText = "Normilizes subtitles.")]
    class NormilizeOptions
    {
        [Option("in", Required = true, HelpText = "Input file")]
        public string Input { get; set; }

        [Option("out", HelpText = "Output file. By default = in")]
        public string Output { get; set; }

        [Option("symbols", Default = 42, HelpText = "Max symbols in line")]
        public int MaxSymbols { get; set; }
    }

    class NormilizeWorker
    {
        private readonly NormilizeOptions _options;
        private readonly List<string> _output = new List<string>();

        public NormilizeWorker(NormilizeOptions options)
        {
            _options = options;
        }

        private int CountSymbols(string text)
        {
            return text.Count(ch => char.IsLetterOrDigit(ch));
        }

        public async Task<int> StartAsync()
        {
            var lines = await System.IO.File.ReadAllLinesAsync(_options.Input);
            foreach (var line in lines)
            {
                if (line.StartsWith("Dialogue"))
                {
                    ProcessLine(line);
                }
                else
                {
                    _output.Add(line);
                }
            }
            var outFile = string.IsNullOrWhiteSpace(_options.Output) ? _options.Input : _options.Output;
            System.IO.File.WriteAllLines(outFile, _output);

            return 1;
        }

        class ScreenBuilder
        {
            private readonly List<string> _items = new List<string>();
            private string _lastItem = null;

            public void Add(string line)
            {
                if (_lastItem != null)
                {
                    _items.Add(_lastItem + "\\N" + line);
                    _lastItem = null;
                    return;
                }
                _lastItem = line;
            }

            public IEnumerable<string> Build()
            {
                if (_lastItem != null)
                {
                    _items.Add(_lastItem);
                    _lastItem = null;
                }
                return _items;
            }
        }

        void ProcessLine(string sub)
        {
            Regex subPattern = new Regex("Dialogue: 0,(?<hs>\\d+):(?<ms>\\d+):(?<ss>\\d+).(?<mss>\\d+),"
                + "(?<he>\\d+):(?<me>\\d+):(?<se>\\d+).(?<mse>\\d+),Default,,0,0,0,,(?<text>.+)");
            var match = subPattern.Match(sub);
            if (!match.Success)
            {
                Console.WriteLine("Failed to parse a line");
                return;
            }

            var text = match.Groups["text"].Value;
            var symbolsCount = CountSymbols(text);
            var lines = text.Split("\\N", StringSplitOptions.RemoveEmptyEntries);

            var startTime = new DateTime(2000, 1, 1, int.Parse(match.Groups["hs"].Value)
                , int.Parse(match.Groups["ms"].Value)
                , int.Parse(match.Groups["ss"].Value)
                , int.Parse(match.Groups["mss"].Value) * 10);
            var endTime = new DateTime(2000, 1, 1, int.Parse(match.Groups["he"].Value)
                , int.Parse(match.Groups["me"].Value)
                , int.Parse(match.Groups["se"].Value)
                , int.Parse(match.Groups["mse"].Value) * 10);
            
            var length = endTime - startTime;
            var symbolsPerSecond = symbolsCount / length.TotalSeconds;

            var builder = new ScreenBuilder();
            foreach (var item in lines)
            {
                foreach (var line in SplitToLimit(item, _options.MaxSymbols))
                {
                    builder.Add(line);
                }
            }

            foreach (var item in builder.Build())
            {
                int subtitleLength = CountSymbols(item);
                startTime = PrintSubtitle(startTime, symbolsPerSecond, item, subtitleLength);
            }
        }

        class LineBuilder
        {
            readonly List<string> _words = new List<string>();
            public int Limit { get; set; } = 42;

            public bool MergeTo(LineBuilder another)
            {
                if (another == null)
                {
                    return false;
                }
                var anotherLength = another._words.Sum(w => w.Count(ch => char.IsLetterOrDigit(ch)));
                var myLenth = _words.Sum(w => w.Count(ch => char.IsLetterOrDigit(ch)));
                if (anotherLength + myLenth > Limit)
                {
                    return false;
                }
                another._words.AddRange(_words);
                return true;
            }

            public bool Add(string word)
            {
                if (!_words.Any())
                {
                    _words.Add(word);
                    return true;
                }
                var length = _words.Sum(w => w.Count(ch => char.IsLetterOrDigit(ch)));
                if (length + word.Count(ch => char.IsLetterOrDigit(ch)) > Limit)
                {
                    return false;
                }
                if (GoodEnd(word) || length < (Limit * 2 / 3) || !GoodEnd(_words[^1]))
                {
                    _words.Add(word);
                    return true;
                }
                return false;
            }

            private bool GoodEnd(string word)
            {
                return char.IsPunctuation(word[^1]);
            }

            public string Build()
            {
                return string.Join(" ", _words);
            }

            public void Clear()
            {
                _words.Clear();
            }
        }

        private IEnumerable<string> SplitToLimit(string item, int limit)
        {
            int subtitleLength = CountSymbols(item);
            if (subtitleLength <= limit)
            {
                return new string[] { item };
            }
            var result = new List<string>();

            LineBuilder lastBuilder = null;
            var currentBuilder = new LineBuilder();

            var words = item.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var word in words)
            {
                if (!currentBuilder.Add(word))
                {
                    if (!currentBuilder.MergeTo(lastBuilder))
                    {
                        AddWordIfNeeded(result, lastBuilder);
                        lastBuilder = currentBuilder;
                    }
                    currentBuilder = new LineBuilder();
                    currentBuilder.Add(word);
                }
            }
            AddWordIfNeeded(result, lastBuilder);
            AddWordIfNeeded(result, currentBuilder);
            return result;
        }

        private static void AddWordIfNeeded(List<string> result, LineBuilder currentBuilder)
        {
            if (currentBuilder == null)
            {
                return;
            }
            var finalWord = currentBuilder.Build();
            if (!string.IsNullOrWhiteSpace(finalWord))
            {
                result.Add(finalWord);
            }
        }

        private DateTime PrintSubtitle(DateTime startTime, double symbolsPerSecond, string subtitleText, int subtitleLength)
        {
            var start = FormatDateTime(startTime);
            var endTime = startTime.AddSeconds((double)subtitleLength / symbolsPerSecond);
            var end = FormatDateTime(endTime);
            _output.Add($"Dialogue: 0,{start},{end},Default,,0,0,0,,{subtitleText}");
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
