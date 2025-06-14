using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fake_language_encoder
{
    public class IPA
    {

        private Dictionary<string, string> dictionary;

        
        public IPA()
        {
            dictionary = new Dictionary<string, string>();
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", $"{Language.English}_DictionaryIPA.txt");
            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                var parts = line.Split(',', 2);
                if (parts.Length == 2)
                {
                    dictionary[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }
        public IPA(Dictionary<string, string> dictionary) =>
            this.dictionary = dictionary;

        public string ToIPA(string text)
        {
            var builder = new StringBuilder();
            var words = Regex.Split(text, @"([\s\p{P}])");

            foreach (var match in words)
            {
                var lower = match.ToLower();
                builder.Append(dictionary.TryGetValue(lower, out var ipa) ? ipa : lower);
            }
            return builder.ToString();
        }

        public string ToEnglish(string ipaText)
        {
            var ipaDict = new Dictionary<string, string>();
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", $"{Language.English}_DictionaryIPA.txt");

            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                var parts = line.Split(',', 2);
                if (parts.Length == 2 && !ipaDict.ContainsKey(parts[1].Trim()))
                {
                    ipaDict[parts[1].Trim()] = parts[0].Trim();
                }
            }

            var ipaToEnglish = new Dictionary<string, string>();
            string filePath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", $"{Language.English}_IPASounds.txt");

            foreach (var line in File.ReadLines(filePath2))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    ipaToEnglish[parts[0].Trim()] = parts[1].Trim();
                }
            }

            var readableText = new StringBuilder();
            var words = Regex.Split(ipaText, @"([\s\p{P}])");

            foreach (var word in words)
            {
                var lower = word.ToLower();
                if (ipaDict.TryGetValue(lower, out var englishWord))
                {
                    readableText.Append(englishWord.Replace(".", "").Replace("(1)", ""));
                }
                else if (!string.IsNullOrWhiteSpace(lower))
                {
                    var wordS = lower.Replace("'", "");
                    foreach (var kvp in ipaToEnglish)
                    {
                        wordS = wordS.Replace(kvp.Key, kvp.Value);
                    }
                    readableText.Append(wordS.Replace(" ", ""));
                }
                readableText.Append(" ");
            }

            return readableText.ToString().Trim();
        }
        public string[] CMUtoIPA(string text)
        {
            var list = new List<string>();
            var lines = text.Split("\n");
            foreach (var line in lines)
            {
                var pairs = line.Split("  ");
                if (pairs.Length > 1)
                {
                    var syllables = pairs[1].Split(" . ");
                    foreach (var si in syllables) { list.Add(si); }
                }
            }
            var IPASyllables = ARBoIPA.ARBtoIPAConvert(list.ToArray());
            Console.WriteLine("Converted");
            return IPASyllables;
        }
    }
    public static class ARBoIPA
    {
        public static Dictionary<string, string> APRoIPA = new Dictionary<string, string>()
        {
            {"AA", "ɑ" }, // ɑ or ɒ
            {"AE", "æ"},
            {"AH", "ʌ" },
            {"AO", "ɔ" },
            {"AW", "aʊ" },
            {"AX", "əɹ" }, // ɚ
            {"AXR", "ə" },
            {"AY", "aɪ" },
            {"EH", "ɛ" },
            {"ER", "ɛɹ" }, // ɝ
            {"EY", "eɪ" },
            {"IH", "ɪ" },
            {"IX", "ɨ" },
            {"IY", "i" },
            {"OW", "oʊ" },
            {"OY", "ɔɪ" },
            {"UH", "ʊ" },
            {"UW", "u" },
            {"UX", "ʉ" },
            {"B", "b" },
            {"CH", "tʃ" },
            {"D", "d" },
            {"DH", "ð" },
            {"DX", "ɾ" },
            {"EL", "l̩" },
            {"EM", "m̩" },
            {"EN", "n̩" },
            {"F", "f" },
            {"G", "ɡ" },
            {"HH", "h" },
            {"H", "h" },
            {"JH", "dʒ"},
            {"K", "k" },
            {"L", "l" },
            {"M", "m" },
            {"N", "n" },
            {"NG", "ŋ" },
            {"NX", "ɾ̃" },
            {"P", "p" },
            {"Q", "ʔ" },
            {"R", "ɹ" },
            {"S", "s" },
            {"SH", "ʃ" },
            {"T", "t" },
            {"TH", "θ" },
            {"V", "v" },
            {"W", "w" },
            {"WH", "ʍ" },
            {"Y", "j" },
            {"Z", "z" },
            {"ZH", "ʒ" }
        };
        public static string[] ARBtoIPAConvert(string[] syllables)
        {
            var list = new List<string>();
            foreach (string s in syllables)
            {
                StringBuilder builder = new StringBuilder();
                var pairs = s.Split(" ");
                foreach (var pair in pairs)
                {
                    bool matchFound = false;
                    foreach (var ipaSymbol in APRoIPA.Keys)
                    {
                        if (pair.StartsWith(ipaSymbol))
                        {
                            builder.Append(APRoIPA[ipaSymbol]);
                            matchFound = true;
                            break;
                        }
                    }

                    if (!matchFound)
                    {
                        builder.Append(pair);
                    }
                }

                list.Add(builder.ToString());
            }
            return list.ToArray();
        }
    }
}
