using System;
using System.Collections.Generic;
using System.Linq;

namespace OtomatikMetinGenisletici.Services
{
    /// <summary>
    /// Hızlı prefix araması için Trie (Prefix Tree) veri yapısı
    /// O(n) karmaşıklığını O(log n)'e düşürür
    /// </summary>
    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; set; }
        public bool IsEndOfWord { get; set; }
        public List<string> Words { get; set; } // Bu node'da biten kelimeler
        public int Frequency { get; set; }
        
        public TrieNode()
        {
            Children = new Dictionary<char, TrieNode>();
            Words = new List<string>();
            IsEndOfWord = false;
            Frequency = 0;
        }
    }

    public class FastTrie
    {
        private readonly TrieNode _root;
        private readonly object _lockObject = new object();

        public FastTrie()
        {
            _root = new TrieNode();
        }

        /// <summary>
        /// Kelimeyi Trie'ye ekler - O(m) karmaşıklığı (m = kelime uzunluğu)
        /// </summary>
        public void Insert(string word, int frequency = 1)
        {
            if (string.IsNullOrEmpty(word)) return;

            lock (_lockObject)
            {
                var current = _root;
                var lowerWord = word.ToLowerInvariant();

                foreach (char c in lowerWord)
                {
                    if (!current.Children.ContainsKey(c))
                    {
                        current.Children[c] = new TrieNode();
                    }
                    current = current.Children[c];
                }

                current.IsEndOfWord = true;
                current.Frequency += frequency;
                
                // Orijinal kelimeyi de sakla (case-sensitive)
                if (!current.Words.Contains(word))
                {
                    current.Words.Add(word);
                }
            }
        }

        /// <summary>
        /// Prefix ile başlayan tüm kelimeleri bulur - O(p + n) karmaşıklığı
        /// p = prefix uzunluğu, n = bulunan kelime sayısı
        /// </summary>
        public List<(string word, int frequency)> SearchByPrefix(string prefix, int maxResults = 10)
        {
            if (string.IsNullOrEmpty(prefix)) return new List<(string, int)>();

            lock (_lockObject)
            {
                var results = new List<(string word, int frequency)>();
                var lowerPrefix = prefix.ToLowerInvariant();
                
                // Prefix'e kadar git
                var current = _root;
                foreach (char c in lowerPrefix)
                {
                    if (!current.Children.ContainsKey(c))
                    {
                        return results; // Prefix bulunamadı
                    }
                    current = current.Children[c];
                }

                // Bu node'dan itibaren tüm kelimeleri topla
                CollectWords(current, results, maxResults);
                
                // Frekansa göre sırala
                return results.OrderByDescending(x => x.frequency)
                             .Take(maxResults)
                             .ToList();
            }
        }

        /// <summary>
        /// Verilen node'dan başlayarak tüm kelimeleri toplar
        /// </summary>
        private void CollectWords(TrieNode node, List<(string word, int frequency)> results, int maxResults)
        {
            if (results.Count >= maxResults) return;

            if (node.IsEndOfWord)
            {
                foreach (var word in node.Words)
                {
                    results.Add((word, node.Frequency));
                    if (results.Count >= maxResults) return;
                }
            }

            foreach (var child in node.Children.Values)
            {
                CollectWords(child, results, maxResults);
                if (results.Count >= maxResults) return;
            }
        }

        /// <summary>
        /// Kelime var mı kontrol eder - O(m) karmaşıklığı
        /// </summary>
        public bool Contains(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;

            lock (_lockObject)
            {
                var current = _root;
                var lowerWord = word.ToLowerInvariant();

                foreach (char c in lowerWord)
                {
                    if (!current.Children.ContainsKey(c))
                    {
                        return false;
                    }
                    current = current.Children[c];
                }

                return current.IsEndOfWord;
            }
        }

        /// <summary>
        /// Trie'deki toplam kelime sayısını döndürür
        /// </summary>
        public int GetWordCount()
        {
            lock (_lockObject)
            {
                return CountWords(_root);
            }
        }

        private int CountWords(TrieNode node)
        {
            int count = node.IsEndOfWord ? node.Words.Count : 0;
            
            foreach (var child in node.Children.Values)
            {
                count += CountWords(child);
            }
            
            return count;
        }

        /// <summary>
        /// Trie'yi temizler
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _root.Children.Clear();
                _root.IsEndOfWord = false;
                _root.Words.Clear();
                _root.Frequency = 0;
            }
        }

        /// <summary>
        /// Bellek kullanımını optimize eder
        /// </summary>
        public void OptimizeMemory()
        {
            lock (_lockObject)
            {
                OptimizeNode(_root);
            }
        }

        private void OptimizeNode(TrieNode node)
        {
            // Kullanılmayan node'ları temizle
            var keysToRemove = new List<char>();
            
            foreach (var kvp in node.Children)
            {
                OptimizeNode(kvp.Value);
                
                // Eğer child node boşsa ve kelime bitirmiyorsa, sil
                if (!kvp.Value.IsEndOfWord && kvp.Value.Children.Count == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                node.Children.Remove(key);
            }
        }
    }
}
