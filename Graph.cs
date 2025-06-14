using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fake_language_encoder
{
    #region Domain primitives
    public enum Language
    {
        English,
        Russian
    }

    public static class Punctuation
    {
        private static readonly HashSet<string> Tokens =
        [
            " ", ",", "-", ".", "`", "'", "#", "!", "?", "...", "..", "..?", "..!", ".?!",
            "’", ":", "=", "+", "—", ";", "(", ")", "[", "]", "{", "}", "*", "\"", "\t",
            "\n", "\r"
        ];

        public static bool IsToken(string value)
            => Tokens.Contains(value) || int.TryParse(value, out _);
    }
    #endregion
    #region Graph
    /// <summary>
    /// Weighted directed graph where vertices are <see cref="char"/>s.
    /// </summary>
    public sealed class CharGraph
    {
        private readonly Dictionary<char, Dictionary<char, int>> _edges = new();
        private readonly Random _random;

        public CharGraph(Random? random = null) => _random = random ?? Random.Shared;
        public IReadOnlyDictionary<char, IReadOnlyDictionary<char, int>> Adjacency
            => _edges.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyDictionary<char, int>)kvp.Value);
        public void AddEdge(char from, char to)
        {
            if (!_edges.TryGetValue(from, out var neighbours))
            {
                neighbours = new Dictionary<char, int>();
                _edges[from] = neighbours;
            }

            neighbours[to] = neighbours.TryGetValue(to, out var weight) ? weight + 1 : 1;
        }

        public char? GetWeightedRandomNeighbour(char vertex)
        {
            if (!_edges.TryGetValue(vertex, out var neighbours) || neighbours.Count == 0)
                return null;

            var total = neighbours.Values.Sum();
            var threshold = _random.Next(total);
            var cumulative = 0;

            foreach (var (next, weight) in neighbours)
            {
                cumulative += weight;
                if (cumulative > threshold)
                    return next;
            }
            return null;
        }
    }
    #endregion
    #region Syllable generation
    public interface ISyllableGenerator
    {
        string Generate(int maxLength, IReadOnlyDictionary<string, string>? reserved = null);
    }
    sealed class CounterSyllableGenerator : ISyllableGenerator
    {
        private int _counter;
        public string Generate(int max, IReadOnlyDictionary<string, string>? _) =>
            (++_counter).ToString();
    }
    /// <summary>
    /// Generates pseudo‑syllables using a first‑order Markov chain built on <see cref="CharGraph"/>.
    /// </summary>
    public sealed class MarkovSyllableGenerator : ISyllableGenerator
    {
        private readonly CharGraph _graph;
        private readonly Random _random;

        public MarkovSyllableGenerator(CharGraph graph, Random? random = null)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _random = random ?? Random.Shared;
        }

        public string Generate(int maxLength = 4, IReadOnlyDictionary<string, string>? reserved = null)
        {
            if (_graph.Adjacency.Count == 0) return string.Empty;

            var vertices = _graph.Adjacency.Keys.ToArray();
            var sb = new StringBuilder();

            char current = vertices[_random.Next(vertices.Length)];
            sb.Append(current);

            for (var i = 1; i < maxLength; i++)
            {
                var next = _graph.GetWeightedRandomNeighbour(current);
                if (next is null) break;

                sb.Append(next.Value);
                current = next.Value;
            }

            var result = sb.ToString();
            return reserved is not null && reserved.Values.Any(x => x == result)
                   ? Generate(maxLength, reserved) // collision, try again
                   : result;
        }
    }
    #endregion


    #region Dictionary persistence
    /// <summary>Abstraction for dictionary storage.</summary>
    public interface IDictionaryProvider
    {
        Task<IDictionary<string, string>> ReadAsync(string name);
        Task WriteAsync(string name, IDictionary<string, string> dictionary);
    }

    public sealed class JsonDictionaryProvider : IDictionaryProvider
    {
        private readonly string _baseDirectory;
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = false
        };

        public JsonDictionaryProvider(string baseDirectory)
        {
            _baseDirectory = Directory.CreateDirectory(baseDirectory).FullName;
        }

        public async Task<IDictionary<string, string>> ReadAsync(string name)
        {
            var path = Path.Combine(_baseDirectory, $"{name}.json");
            if (!File.Exists(path)) return new Dictionary<string, string>();

            await using var stream = File.OpenRead(path);
            var dict = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream)
                       ?? new Dictionary<string, string>();
            return dict;
        }

        public async Task WriteAsync(string name, IDictionary<string, string> dictionary)
        {
            var path = Path.Combine(_baseDirectory, $"{name}.json");
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, dictionary, _serializerOptions);
        }
    }
    #endregion

    #region Dictionary services
    public interface IUniversalDictionary
    {
        Task<string?> TryGetAsync(string word);
        Task AddOrUpdateAsync(string word, string replacement);
    }

    public sealed class UniversalDictionary : IUniversalDictionary
    {
        private const string ResourceName = "UniversalFakeDictionary";
        private readonly IDictionaryProvider _provider;

        public UniversalDictionary(IDictionaryProvider provider) => _provider = provider;

        public async Task<string?> TryGetAsync(string word)
        {
            var dict = await _provider.ReadAsync(ResourceName);
            return dict.TryGetValue(word, out var replacement) ? replacement : null;
        }

        public async Task AddOrUpdateAsync(string word, string replacement)
        {
            var dict = await _provider.ReadAsync(ResourceName);
            dict[word] = replacement;
            await _provider.WriteAsync(ResourceName, dict);
        }
    }

    public sealed class FakeDictionaryService
    {
        private readonly IDictionaryProvider _provider;
        private readonly ISyllableGenerator _syllableGenerator;
        private readonly IUniversalDictionary _universal;
        private readonly IPA _ipa; // external phonetic converter

        public FakeDictionaryService(IDictionaryProvider provider,
                                     ISyllableGenerator syllableGenerator,
                                     IUniversalDictionary universal,
                                     IPA ipa)
        {
            _provider = provider;
            _syllableGenerator = syllableGenerator;
            _universal = universal;
            _ipa = ipa;
        }

        public async Task<IDictionary<string, string>> GetOrCreateAsync(Language language, bool rewrite = false)
        {
            var key = $"{language}_FakeDictionary";
            if (!rewrite)
            {
                var cached = await _provider.ReadAsync(key);
                if (cached.Count > 0) return cached;
            }

            var lines = await LoadSourceDictionaryAsync(language);
            var fakeDictionary = new Dictionary<string, string>(capacity: lines.Length);

            foreach (var word in lines)
            {
                if (fakeDictionary.ContainsKey(word)) continue;

                var len = GetTargetLength(fakeDictionary.Count, word.Length);
                string candidate;

                do
                {
                    candidate = _ipa.ToEnglish(_syllableGenerator.Generate(len, fakeDictionary));
                }
                while (fakeDictionary.ContainsValue(candidate) || candidate.Length < 3);

                fakeDictionary[word] = candidate;
            }

            await _provider.WriteAsync(key, fakeDictionary);
            return fakeDictionary;
        }

        private static int GetTargetLength(int count, int sourceLength) => count switch
        {
            <= 100 => 3,
            <= 1_000 => sourceLength < 6 ? 4 : 6,
            <= 10_000 => sourceLength < 7 ? 5 : 7,
            <= 50_000 => 8,
            _ => 10
        };

        private static async Task<string[]> LoadSourceDictionaryAsync(Language lang)
        {
            var fileName = $"{lang}_DictionarySorted.txt";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", fileName);
            var content = await File.ReadAllTextAsync(path, Encoding.UTF8);
            return content.Split('\n').Select(l => l.Split('\t')[0]).ToArray();
        }
    }
    #endregion

    #region Text cipher
    public sealed class TextCipher
    {
        private readonly FakeDictionaryService _dictionaryService;
        private readonly IUniversalDictionary _universal;
        private readonly IPA _ipa; // external dependency

        public TextCipher(FakeDictionaryService dictionaryService, IUniversalDictionary universal, IPA ipa)
        {
            _dictionaryService = dictionaryService;
            _universal = universal;
            _ipa = ipa;
        }
        public TextCipher(Language language, string? baseDir = null)
        {
            baseDir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            var provider = new JsonDictionaryProvider(baseDir);
            var universal = new UniversalDictionary(provider);
            var ipa = new IPA();
            var graph = GraphBuilder.BuildAsync(language, ipa).GetAwaiter().GetResult();
            var generator = new MarkovSyllableGenerator(graph);
            var service = new FakeDictionaryService(provider, generator, universal, ipa);

            _dictionaryService = service;
            _universal = universal;
            _ipa = ipa;
        }
        public async Task<string> EncryptAsync(string text, Language language = Language.English, bool rewriteDictionary = false)
        {
            var dictionary = await _dictionaryService.GetOrCreateAsync(language, rewriteDictionary);
            var tokens = Regex.Split(text, @"([\s\p{P}])");
            var sb = new StringBuilder(tokens.Length);
            
            foreach (var token in tokens)
            {
                if (token.Length == 0)
                {
                    sb.Append(token);
                    continue;
                }

                var lower = token.ToLowerInvariant();
                sb.Append(await ConvertWordAsync(lower, dictionary));
            }
            return sb.ToString();
        }

        public async Task<string> DecryptAsync(string text, Language language = Language.English)
        {
            var dictionary = await _dictionaryService.GetOrCreateAsync(language);
            var tokens = Regex.Split(text, @"([\s\p{P}])");

            var sb = new StringBuilder(tokens.Length);

            foreach (var token in tokens)
            {
                if (token.Length == 0)
                {
                    sb.Append(token);
                    continue;
                }

                var lower = token.ToLowerInvariant();
                sb.Append(await ConvertBackAsync(lower, dictionary));
            }
            return sb.ToString();
        }

        #region Word conversions
        private static readonly IReadOnlyList<string> RussianSuffixes =
        [
            "ость","ение","ание","ик","чик","ок","ек","ыш","ка","ица","ник","тель",
            "ист","изм","ство","ина","ый","ий","ой","ев","ов","ин","ан","ян","еньк",
            "оват","ать","ять","ить","еть","оть","нуть","овать","l","ла","lo","ли",
            "ть","ся","ась","ись","лось","лись","о","е","a","у","и","ски","цки",
            "ому","ему","ы","ю","ам","ям","ах","ях","ом","ем","ей","ь","ая","яя",
            "ое","ее","ым","им","ой","их","ых","ут","ют","ат","ят"
        ];

        private static readonly IReadOnlyList<string> EnglishSuffixes =
        [
            "s","ed","ing","er","es","ly","ment","ness","ful","less","est"
        ];

        private static readonly IReadOnlyList<string> Suffixes =
            EnglishSuffixes.Concat(RussianSuffixes).ToArray();

        private async Task<string> ConvertWordAsync(string word, IDictionary<string, string> dict)
        {
            if (string.IsNullOrEmpty(word)) return word;
            if (dict.TryGetValue(word, out var direct)) return direct;
            if (Punctuation.IsToken(word)) return word;

            var universal = await _universal.TryGetAsync(word);
            if (universal is not null) return universal;
            //nothing cached, build a new fake word
            var len = Math.Max(3, word.Length);
            string newWord;
            const int MaxAttempts = 100;
            var attempts = 0;
            do
            {
                if (++attempts > MaxAttempts) 
                    throw new InvalidOperationException($"Cannot generate unique fake for '{word}' after {MaxAttempts} tries.");
                
                newWord = _ipa.ToEnglish(dict.Values.Count == 0
                                         ? word[..Math.Min(len, word.Length)]
                                         : dict.Values.First());
            }
            while (dict.Values.Any(x => x==newWord));

            await _universal.AddOrUpdateAsync(word, newWord);
            return newWord;
        }

        private async Task<string> ConvertBackAsync(string fake, IDictionary<string, string> dict)
        {
            var original = dict.FirstOrDefault(kvp => kvp.Value == fake).Key;
            if (!string.IsNullOrEmpty(original)) return original;

            var universal = await _universal.TryGetAsync(fake);
            return universal ?? fake;
        }
        #endregion
    }
    #endregion

    #region Graph builder
    public static class GraphBuilder
    {
        public static async Task<CharGraph> BuildAsync(Language language, IPA ipa, bool rebuild = false)
        {
            var dataPath = language switch
            {
                Language.Russian => $"{Language.Russian}_DictionaryIPA.txt",
                _ => $"{Language.English}_IPASyllables.txt"
            };

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", dataPath);
            var outFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", $"{language}_Graph.json");

            if (File.Exists(outFile) && !rebuild)
            {
                var json = await File.ReadAllTextAsync(outFile);
                var adjacency = JsonSerializer.Deserialize<Dictionary<char, Dictionary<char, int>>>(json)
                                ?? [];
                var graph = new CharGraph();
                foreach (var (from, neighbours) in adjacency)
                {
                    foreach (var (to, weight) in neighbours)
                    {
                        for (var i = 0; i < weight; i++)
                            graph.AddEdge(from, to);
                    }
                }
                return graph;
            }

            var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
            var syllables = language == Language.Russian
                ? content.Split('\n').Select(l => l.Split('\t')[1])
                : ipa.CMUtoIPA(content);

            var result = new CharGraph();

            foreach (var syllable in syllables)
            {
                for (var i = 0; i < syllable.Length - 1; i++)
                    result.AddEdge(syllable[i], syllable[i + 1]);
            }

            var serialised = JsonSerializer.Serialize(result.Adjacency);
            await File.WriteAllTextAsync(outFile, serialised);
            return result;
        }
    }
    #endregion
}
