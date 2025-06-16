using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Services
{
    public class TextLearningEngine
    {
        private readonly string _dataFilePath;
        private readonly object _lockObject = new();
        private LearningData _learningData;
        private readonly Timer _saveTimer;
        private bool _hasUnsavedChanges;

        public TextLearningEngine(string dataFilePath = "learning_data.json")
        {
            try
            {
                Console.WriteLine("[DEBUG] TextLearningEngine constructor başlıyor...");

                _dataFilePath = dataFilePath;
                _learningData = new LearningData();

                Console.WriteLine("[DEBUG] Timer oluşturuluyor...");
                // Her 30 saniyede bir otomatik kaydet
                _saveTimer = new Timer(AutoSave, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

                Console.WriteLine("[DEBUG] Öğrenme verileri yükleniyor...");
                LoadLearningData();

                Console.WriteLine("[DEBUG] TextLearningEngine constructor tamamlandı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TextLearningEngine constructor hatası: {ex}");
                throw;
            }
        }

        private readonly HashSet<string> _learnedSentences = new();

        public async Task LearnFromTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            {
                Console.WriteLine($"[LEARNING] Metin çok kısa, öğrenme atlandı: '{text}'");
                return;
            }

            // Normalize edilmiş cümle kontrolü
            var normalizedText = text.Trim().ToLowerInvariant();
            Console.WriteLine($"[LEARNING] Normalized text: '{normalizedText}'");
            Console.WriteLine($"[LEARNING] Learned sentences count: {_learnedSentences.Count}");

            if (_learnedSentences.Contains(normalizedText))
            {
                Console.WriteLine($"[LEARNING] *** CÜMLE ZATEN ÖĞRENİLMİŞ, ATLANIYOR: '{text}' ***");
                return;
            }

            Console.WriteLine($"[LEARNING] *** YENİ CÜMLE BULUNDU, ÖĞRENİLECEK: '{text}' ***");

            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    Console.WriteLine($"[LEARNING] YENİ CÜMLE öğreniliyor: '{text}'");
                    var words = PreprocessText(text);
                    Console.WriteLine($"[LEARNING] {words.Count} kelime işleniyor: [{string.Join(", ", words)}]");

                    if (words.Count < 2)
                    {
                        Console.WriteLine($"[LEARNING] Yetersiz kelime sayısı, öğrenme atlandı");
                        return;
                    }

                    UpdateWordFrequencies(words);
                    UpdateNGrams(words);
                    UpdateCompletionPrefixes(words);

                    // Öğrenilen cümleyi kaydet
                    _learnedSentences.Add(normalizedText);

                    _learningData.TotalWordsLearned += words.Count;
                    _learningData.LastUpdated = DateTime.Now;
                    _hasUnsavedChanges = true;

                    Console.WriteLine($"[LEARNING] Öğrenme tamamlandı. Toplam kelime: {_learningData.TotalWordsLearned}");
                }
            });
        }

        public async Task<List<SmartSuggestion>> GetSuggestionsAsync(string context, int maxSuggestions = 5)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    Console.WriteLine($"[SUGGESTIONS] *** ÖNERİ ARANMAYA BAŞLANIYOR ***");
                    Console.WriteLine($"[SUGGESTIONS] Context: '{context}'");
                    Console.WriteLine($"[SUGGESTIONS] Max suggestions: {maxSuggestions}");
                    Console.WriteLine($"[SUGGESTIONS] Toplam öğrenilen kelime: {_learningData.WordFrequencies.Count}");
                    Console.WriteLine($"[SUGGESTIONS] Toplam bigram: {_learningData.Bigrams.Count}");
                    Console.WriteLine($"[SUGGESTIONS] Toplam trigram: {_learningData.Trigrams.Count}");

                    var suggestions = new List<SmartSuggestion>();
                    var words = PreprocessText(context);

                    if (words.Count == 0)
                    {
                        Console.WriteLine($"[SUGGESTIONS] *** KELIME BULUNAMADI, BOŞ DÖNDÜRÜLÜYOR ***");
                        return suggestions;
                    }

                    Console.WriteLine($"[SUGGESTIONS] {words.Count} kelime işleniyor: [{string.Join(", ", words)}]");

                    // Öğrenilen verileri logla
                    Console.WriteLine($"[SUGGESTIONS] İlk 10 kelime frekansı:");
                    foreach (var wf in _learningData.WordFrequencies.Take(10))
                    {
                        Console.WriteLine($"[SUGGESTIONS]   - '{wf.Key}': {wf.Value}");
                    }

                    Console.WriteLine($"[SUGGESTIONS] İlk 10 bigram:");
                    foreach (var bg in _learningData.Bigrams.Take(10))
                    {
                        Console.WriteLine($"[SUGGESTIONS]   - '{bg.Key}': {bg.Value}");
                    }

                    // 1. Kelime tamamlama önerileri (sadece son kelime yarım ise)
                    var lastWord = words.Last();
                    if (!string.IsNullOrEmpty(lastWord) && lastWord.Length >= 2)
                    {
                        var completions = GetWordCompletions(lastWord, maxSuggestions);
                        suggestions.AddRange(completions);
                        Console.WriteLine($"[SUGGESTIONS] {completions.Count} kelime tamamlama önerisi bulundu");
                    }

                    // 2. Sonraki kelime önerileri (context'e göre)
                    var nextWords = GetNextWordPredictions(words, maxSuggestions);
                    suggestions.AddRange(nextWords);
                    Console.WriteLine($"[SUGGESTIONS] {nextWords.Count} sonraki kelime önerisi bulundu");

                    // 3. Cümle devamı önerileri (bigram/trigram bazlı)
                    var sentenceContinuations = GetSentenceContinuations(words, maxSuggestions);
                    suggestions.AddRange(sentenceContinuations);
                    Console.WriteLine($"[SUGGESTIONS] {sentenceContinuations.Count} cümle devamı önerisi bulundu");

                    // 4. Öğrenilmiş kalıp önerileri
                    var learnedPatterns = GetLearnedPatterns(words, maxSuggestions);
                    suggestions.AddRange(learnedPatterns);
                    Console.WriteLine($"[SUGGESTIONS] {learnedPatterns.Count} öğrenilmiş kalıp önerisi bulundu");

                    // Önerileri skorla ve sırala
                    var finalSuggestions = suggestions
                        .GroupBy(s => s.Text.ToLowerInvariant())
                        .Select(g => g.OrderByDescending(s => s.Confidence).First())
                        .OrderByDescending(s => s.Confidence)
                        .Take(maxSuggestions)
                        .ToList();

                    Console.WriteLine($"[SUGGESTIONS] Toplam {finalSuggestions.Count} öneri döndürülüyor");
                    foreach (var suggestion in finalSuggestions)
                    {
                        Console.WriteLine($"[SUGGESTIONS] - {suggestion.Text} ({suggestion.Confidence:P0}) [{suggestion.Type}]");
                    }

                    return finalSuggestions;
                }
            });
        }

        private List<string> PreprocessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            // Noktalama işaretlerini ayır
            text = Regex.Replace(text, @"([.!?,:;])", " $1 ");
            
            // Çoklu boşlukları tek boşluğa çevir
            text = Regex.Replace(text, @"\s+", " ");
            
            // Kelimeleri ayır ve temizle
            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                      .Select(w => w.Trim().ToLowerInvariant())
                      .Where(w => !string.IsNullOrEmpty(w))
                      .ToList();
        }

        private void UpdateWordFrequencies(List<string> words)
        {
            foreach (var word in words)
            {
                if (word.Length >= 2) // Çok kısa kelimeleri atla
                {
                    _learningData.WordFrequencies.AddOrUpdate(word, 1, (key, oldValue) => oldValue + 1);
                }
            }
        }

        private void UpdateNGrams(List<string> words)
        {
            // Bigrams
            for (int i = 0; i < words.Count - 1; i++)
            {
                var bigram = $"{words[i]} {words[i + 1]}";
                _learningData.Bigrams.AddOrUpdate(bigram, 1, (key, oldValue) => oldValue + 1);
            }

            // Trigrams
            for (int i = 0; i < words.Count - 2; i++)
            {
                var trigram = $"{words[i]} {words[i + 1]} {words[i + 2]}";
                _learningData.Trigrams.AddOrUpdate(trigram, 1, (key, oldValue) => oldValue + 1);
            }
        }

        private void UpdateCompletionPrefixes(List<string> words)
        {
            foreach (var word in words)
            {
                if (word.Length > 2)
                {
                    for (int i = 1; i < Math.Min(word.Length, 8); i++)
                    {
                        var prefix = word.Substring(0, i);
                        _learningData.CompletionPrefixes.AddOrUpdate(
                            prefix,
                            new List<string> { word },
                            (key, oldList) =>
                            {
                                if (!oldList.Contains(word))
                                    oldList.Add(word);
                                return oldList;
                            });
                    }
                }
            }
        }

        private List<SmartSuggestion> GetWordCompletions(string prefix, int maxSuggestions)
        {
            var suggestions = new List<SmartSuggestion>();

            if (_learningData.CompletionPrefixes.TryGetValue(prefix, out var completions))
            {
                foreach (var completion in completions.Take(maxSuggestions))
                {
                    if (completion != prefix && completion.StartsWith(prefix))
                    {
                        var frequency = _learningData.WordFrequencies.GetValueOrDefault(completion, 0);
                        var confidence = Math.Min(0.9, frequency / 100.0 + 0.1);

                        suggestions.Add(new SmartSuggestion
                        {
                            Text = completion,
                            Confidence = confidence,
                            Context = prefix,
                            Frequency = frequency,
                            Type = SuggestionType.WordCompletion,
                            LastUsed = DateTime.Now
                        });
                    }
                }
            }

            return suggestions;
        }

        private List<SmartSuggestion> GetNextWordPredictions(List<string> words, int maxSuggestions)
        {
            var suggestions = new List<SmartSuggestion>();

            if (words.Count >= 2)
            {
                // Trigram tabanlı tahminler
                var bigramKey = $"{words[words.Count - 2]} {words[words.Count - 1]}";
                var trigramCandidates = _learningData.Trigrams
                    .Where(kvp => kvp.Key.StartsWith(bigramKey + " "))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(maxSuggestions);

                foreach (var trigram in trigramCandidates)
                {
                    var parts = trigram.Key.Split(' ');
                    if (parts.Length == 3)
                    {
                        var nextWord = parts[2];
                        var confidence = Math.Min(0.95, trigram.Value / 50.0 + 0.2);

                        suggestions.Add(new SmartSuggestion
                        {
                            Text = nextWord,
                            Confidence = confidence,
                            Context = bigramKey,
                            Frequency = trigram.Value,
                            Type = SuggestionType.NextWord,
                            LastUsed = DateTime.Now
                        });
                    }
                }
            }

            if (words.Count >= 1 && suggestions.Count < maxSuggestions)
            {
                // Bigram tabanlı tahminler
                var lastWord = words.Last();
                var bigramCandidates = _learningData.Bigrams
                    .Where(kvp => kvp.Key.StartsWith(lastWord + " "))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(maxSuggestions - suggestions.Count);

                foreach (var bigram in bigramCandidates)
                {
                    var parts = bigram.Key.Split(' ');
                    if (parts.Length == 2)
                    {
                        var nextWord = parts[1];
                        if (!suggestions.Any(s => s.Text == nextWord))
                        {
                            var confidence = Math.Min(0.8, bigram.Value / 30.0 + 0.1);

                            suggestions.Add(new SmartSuggestion
                            {
                                Text = nextWord,
                                Confidence = confidence,
                                Context = lastWord,
                                Frequency = bigram.Value,
                                Type = SuggestionType.NextWord,
                                LastUsed = DateTime.Now
                            });
                        }
                    }
                }
            }

            return suggestions;
        }

        private List<SmartSuggestion> GetSentenceContinuations(List<string> words, int maxSuggestions)
        {
            var suggestions = new List<SmartSuggestion>();

            if (words.Count == 0) return suggestions;

            // Son 1-3 kelimeye göre devam önerileri
            for (int i = Math.Max(0, words.Count - 3); i < words.Count; i++)
            {
                var context = string.Join(" ", words.Skip(i));

                // Bigram'larda ara
                foreach (var bigram in _learningData.Bigrams)
                {
                    if (bigram.Key.StartsWith(context + " ", StringComparison.OrdinalIgnoreCase))
                    {
                        var nextWord = bigram.Key.Substring(context.Length + 1);
                        if (!string.IsNullOrEmpty(nextWord) && !nextWord.Contains(' '))
                        {
                            suggestions.Add(new SmartSuggestion
                            {
                                Text = nextWord,
                                Type = SuggestionType.NextWord,
                                Confidence = Math.Min(0.85, bigram.Value / 50.0),
                                Frequency = bigram.Value,
                                Context = context,
                                LastUsed = DateTime.Now
                            });
                        }
                    }
                }

                // Trigram'larda ara
                foreach (var trigram in _learningData.Trigrams)
                {
                    if (trigram.Key.StartsWith(context + " ", StringComparison.OrdinalIgnoreCase))
                    {
                        var remainingWords = trigram.Key.Substring(context.Length + 1);
                        if (!string.IsNullOrEmpty(remainingWords))
                        {
                            var nextWord = remainingWords.Split(' ').First();
                            suggestions.Add(new SmartSuggestion
                            {
                                Text = nextWord,
                                Type = SuggestionType.NextWord,
                                Confidence = Math.Min(0.9, trigram.Value / 30.0),
                                Frequency = trigram.Value,
                                Context = context,
                                LastUsed = DateTime.Now
                            });
                        }
                    }
                }
            }

            return suggestions.Take(maxSuggestions).ToList();
        }

        private List<SmartSuggestion> GetLearnedPatterns(List<string> words, int maxSuggestions)
        {
            var suggestions = new List<SmartSuggestion>();

            // Kullanıcı düzeltmelerinden öneriler
            if (words.Count > 0)
            {
                var lastWord = words.Last();
                if (_learningData.UserCorrections.TryGetValue(lastWord, out var corrections))
                {
                    foreach (var correction in corrections.Take(maxSuggestions))
                    {
                        if (correction != lastWord)
                        {
                            suggestions.Add(new SmartSuggestion
                            {
                                Text = correction,
                                Confidence = 0.7,
                                Context = lastWord,
                                Frequency = 1,
                                Type = SuggestionType.Learned,
                                LastUsed = DateTime.Now
                            });
                        }
                    }
                }
            }

            return suggestions;
        }

        public async Task AcceptSuggestionAsync(SmartSuggestion suggestion, string context)
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    _learningData.TotalSuggestionsAccepted++;
                    
                    // Kabul edilen öneriyi güçlendir
                    _learningData.WordFrequencies.AddOrUpdate(
                        suggestion.Text, 
                        1, 
                        (key, oldValue) => oldValue + 2);

                    _hasUnsavedChanges = true;
                }
            });
        }

        public async Task RejectSuggestionAsync(SmartSuggestion suggestion, string context)
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    _learningData.TotalSuggestionsRejected++;
                    _hasUnsavedChanges = true;
                }
            });
        }

        public LearningStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                var mostCommonWords = _learningData.WordFrequencies
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(20)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList();

                return new LearningStatistics
                {
                    TotalUniqueWords = _learningData.WordFrequencies.Count,
                    TotalWordCount = _learningData.WordFrequencies.Values.Sum(),
                    TotalBigrams = _learningData.Bigrams.Count,
                    TotalTrigrams = _learningData.Trigrams.Count,
                    CompletionPrefixes = _learningData.CompletionPrefixes.Count,
                    UserCorrections = _learningData.UserCorrections.Count,
                    MostCommonWords = mostCommonWords,
                    LastLearningSession = _learningData.LastUpdated
                };
            }
        }

        public async Task<DetailedStatistics> GetDetailedStatisticsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    return new DetailedStatistics
                    {
                        TotalUniqueWords = _learningData.WordFrequencies.Count,
                        TotalWordCount = _learningData.WordFrequencies.Values.Sum(),
                        TotalBigrams = _learningData.Bigrams.Count,
                        TotalTrigrams = _learningData.Trigrams.Count,
                        CompletionPrefixes = _learningData.CompletionPrefixes.Count,
                        UserCorrections = _learningData.UserCorrections.Count,
                        MostCommonWords = _learningData.WordFrequencies
                            .OrderByDescending(kvp => kvp.Value)
                            .Take(20)
                            .Select(kvp => (kvp.Key, kvp.Value))
                            .ToList(),
                        AveragePredictionTime = 0.0,
                        AccuracyScore = 0.75,
                        LastLearningSession = _learningData.LastUpdated,
                        TotalLearningTime = TimeSpan.FromMinutes(30),
                        TotalSuggestionsGiven = _learningData.TotalSuggestionsGiven,
                        TotalSuggestionsAccepted = _learningData.TotalSuggestionsAccepted,
                        TotalSuggestionsRejected = _learningData.TotalSuggestionsRejected,
                        WordsByLength = _learningData.WordFrequencies.GroupBy(w => w.Key.Length.ToString())
                            .ToDictionary(g => g.Key, g => g.Sum(w => w.Value)),
                        BigramsByFrequency = _learningData.Bigrams.ToDictionary(b => b.Key, b => b.Value),
                        TrigramsByFrequency = _learningData.Trigrams.ToDictionary(t => t.Key, t => t.Value),
                        RecentActivities = new List<LearningActivity>()
                    };
                }
            });
        }

        private void LoadLearningData()
        {
            try
            {
                Console.WriteLine($"[LEARNING] LoadLearningData başlıyor, dosya yolu: {_dataFilePath}");

                if (File.Exists(_dataFilePath))
                {
                    Console.WriteLine($"[LEARNING] Dosya mevcut, yükleniyor...");
                    var json = File.ReadAllText(_dataFilePath);
                    var data = JsonSerializer.Deserialize<LearningData>(json);
                    if (data != null)
                    {
                        _learningData = data;
                        Console.WriteLine($"[LEARNING] *** Öğrenme verileri başarıyla yüklendi! ***");
                        Console.WriteLine($"[LEARNING] - Kelime sayısı: {_learningData.WordFrequencies.Count}");
                        Console.WriteLine($"[LEARNING] - Bigram sayısı: {_learningData.Bigrams.Count}");
                        Console.WriteLine($"[LEARNING] - Trigram sayısı: {_learningData.Trigrams.Count}");
                        Console.WriteLine($"[LEARNING] - Completion prefix sayısı: {_learningData.CompletionPrefixes.Count}");
                        Console.WriteLine($"[LEARNING] - Son güncelleme: {_learningData.LastUpdated}");

                        // İlk 5 kelimeyi logla
                        Console.WriteLine($"[LEARNING] İlk 5 kelime:");
                        foreach (var word in _learningData.WordFrequencies.Take(5))
                        {
                            Console.WriteLine($"[LEARNING]   - '{word.Key}': {word.Value}");
                        }

                        // İlk 5 bigram'ı logla
                        Console.WriteLine($"[LEARNING] İlk 5 bigram:");
                        foreach (var bigram in _learningData.Bigrams.Take(5))
                        {
                            Console.WriteLine($"[LEARNING]   - '{bigram.Key}': {bigram.Value}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[LEARNING] Dosya deserialize edilemedi, yeni veri oluşturuluyor");
                        _learningData = new LearningData();
                    }
                }
                else
                {
                    Console.WriteLine($"[LEARNING] Dosya bulunamadı, yeni veri oluşturuluyor");
                    _learningData = new LearningData();
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda yeni veri ile başla
                _learningData = new LearningData();
                Console.WriteLine($"[ERROR] Öğrenme verisi yükleme hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private void AutoSave(object? state)
        {
            if (_hasUnsavedChanges)
            {
                SaveLearningData();
            }
        }

        public void SaveLearningData()
        {
            try
            {
                lock (_lockObject)
                {
                    var json = JsonSerializer.Serialize(_learningData, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(_dataFilePath, json);
                    _hasUnsavedChanges = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Öğrenme verisi kaydetme hatası: {ex.Message}");
            }
        }

        public async Task ResetLearningDataAsync()
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    _learningData = new LearningData();
                    _hasUnsavedChanges = true;
                    SaveLearningData();
                }
            });
        }

        public void Dispose()
        {
            _saveTimer?.Dispose();
            if (_hasUnsavedChanges)
            {
                SaveLearningData();
            }
        }
    }
}
