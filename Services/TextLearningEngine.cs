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
        private readonly ShortcutService? _shortcutService;


        public TextLearningEngine(string dataFilePath = "learning_data.json", ShortcutService? shortcutService = null)
        {
            try
            {
                Console.WriteLine("[DEBUG] TextLearningEngine constructor başlıyor...");

                _dataFilePath = dataFilePath;
                _learningData = new LearningData();
                _shortcutService = shortcutService;

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
        private readonly Dictionary<string, DateTime> _recentContexts = new();
        private readonly object _contextTrackingLock = new object();

        public async Task LearnFromTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            {
                Console.WriteLine($"[LEARNING] Metin çok kısa, öğrenme atlandı: '{text}'");
                return;
            }

            // Kısayol genişletmesi kontrolü
            if (_shortcutService?.IsRecentlyExpandedText(text) == true)
            {
                Console.WriteLine($"[LEARNING] *** KISAYOL GENİŞLETMESİ ALGILANDI, ÖĞRENME ATLANIYOR: '{text}' ***");
                return;
            }

            // Normalize edilmiş cümle kontrolü
            var normalizedText = text.Trim().ToLowerInvariant();
            Console.WriteLine($"[LEARNING] Normalized text: '{normalizedText}'");
            Console.WriteLine($"[LEARNING] Learned sentences count: {_learnedSentences.Count}");

            // AYNI CÜMLE TEKRAR ÖĞRENİLEBİLİR (frekans artışı için)
            var isAlreadyLearned = _learnedSentences.Contains(normalizedText);
            if (isAlreadyLearned)
            {
                Console.WriteLine($"[LEARNING] *** CÜMLE DAHA ÖNCE ÖĞRENİLMİŞ, FREKANS ARTIRILIYOR: '{text}' ***");
            }
            else
            {
                Console.WriteLine($"[LEARNING] *** YENİ CÜMLE BULUNDU, ÖĞRENİLECEK: '{text}' ***");
            }

            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    Console.WriteLine($"[LEARNING] Cümle öğreniliyor: '{text}' (Daha önce öğrenilmiş: {isAlreadyLearned})");
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

                    // Öğrenilen cümleyi kaydet (sadece ilk kez)
                    if (!isAlreadyLearned)
                    {
                        _learnedSentences.Add(normalizedText);
                    }

                    _learningData.TotalWordsLearned += words.Count;
                    _learningData.LastUpdated = DateTime.Now;
                    _hasUnsavedChanges = true;

                    Console.WriteLine($"[LEARNING] Öğrenme tamamlandı. Toplam kelime: {_learningData.TotalWordsLearned}");
                }
            });
        }

        private bool IsRecentContext(string context)
        {
            lock (_contextTrackingLock)
            {
                var normalizedContext = context.Trim().ToLowerInvariant();

                if (_recentContexts.TryGetValue(normalizedContext, out var lastSeen))
                {
                    // Son 2 saniye içinde aynı context kullanıldıysa true döndür
                    if (DateTime.Now - lastSeen < TimeSpan.FromSeconds(2))
                    {
                        Console.WriteLine($"[CONTEXT_TRACKING] Recent context detected: '{context}'");
                        return true;
                    }
                }

                // Context'i güncelle
                _recentContexts[normalizedContext] = DateTime.Now;

                // Eski context'leri temizle (10'dan fazla varsa)
                if (_recentContexts.Count > 10)
                {
                    var oldContexts = _recentContexts
                        .Where(kvp => DateTime.Now - kvp.Value > TimeSpan.FromSeconds(10))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var oldContext in oldContexts)
                    {
                        _recentContexts.Remove(oldContext);
                    }
                }

                return false;
            }
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

                    // Recent context kontrolü - performans için
                    if (IsRecentContext(context))
                    {
                        Console.WriteLine($"[SUGGESTIONS] Recent context detected, öneri atlandı");
                        return new List<SmartSuggestion>();
                    }

                    Console.WriteLine($"[SUGGESTIONS] Toplam öğrenilen kelime: {_learningData.WordFrequencies.Count}");
                    Console.WriteLine($"[SUGGESTIONS] Toplam bigram: {_learningData.Bigrams.Count}");
                    Console.WriteLine($"[SUGGESTIONS] Toplam trigram: {_learningData.Trigrams.Count}");
                    Console.WriteLine($"[SUGGESTIONS] Toplam 4-gram: {_learningData.FourGrams.Count}");

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

                    // 1. Cümle başlangıcı kontrolü (en yüksek öncelik)
                    var sentenceStartSuggestions = GetSentenceStartSuggestions(words, maxSuggestions);
                    suggestions.AddRange(sentenceStartSuggestions);
                    Console.WriteLine($"[SUGGESTIONS] {sentenceStartSuggestions.Count} cümle başlangıcı önerisi bulundu");

                    // 2. Kelime tamamlama önerileri (sadece son kelime yarım ise)
                    if (suggestions.Count < maxSuggestions)
                    {
                        var lastWord = words.Last();
                        if (!string.IsNullOrEmpty(lastWord) && lastWord.Length >= 2)
                        {
                            var completions = GetWordCompletions(lastWord, maxSuggestions - suggestions.Count);
                            suggestions.AddRange(completions);
                            Console.WriteLine($"[SUGGESTIONS] {completions.Count} kelime tamamlama önerisi bulundu");
                        }
                    }

                    // 3. Sonraki kelime önerileri (context'e göre)
                    if (suggestions.Count < maxSuggestions)
                    {
                        var nextWords = GetNextWordPredictions(words, maxSuggestions - suggestions.Count);
                        suggestions.AddRange(nextWords);
                        Console.WriteLine($"[SUGGESTIONS] {nextWords.Count} sonraki kelime önerisi bulundu");
                    }

                    // 3. Cümle devamı önerileri (bigram/trigram bazlı)
                    var sentenceContinuations = GetSentenceContinuations(words, maxSuggestions);
                    suggestions.AddRange(sentenceContinuations);
                    Console.WriteLine($"[SUGGESTIONS] {sentenceContinuations.Count} cümle devamı önerisi bulundu");

                    // 4. Öğrenilmiş kalıp önerileri
                    var learnedPatterns = GetLearnedPatterns(words, maxSuggestions);
                    suggestions.AddRange(learnedPatterns);
                    Console.WriteLine($"[SUGGESTIONS] {learnedPatterns.Count} öğrenilmiş kalıp önerisi bulundu");

                    // Önerileri skorla ve sırala - FREKANSA GÖRE ÖNCELIK
                    var finalSuggestions = suggestions
                        .GroupBy(s => s.Text.ToLowerInvariant())
                        .Select(g => g.OrderByDescending(s => s.Frequency).ThenByDescending(s => s.Confidence).First())
                        .OrderByDescending(s => s.Frequency)
                        .ThenByDescending(s => s.Confidence)
                        .Take(maxSuggestions)
                        .ToList();

                    Console.WriteLine($"[SUGGESTIONS] *** ÖNERİ SONUÇLARI (FREKANS ÖNCELIKLI SIRALAMA) ***");
                    Console.WriteLine($"[SUGGESTIONS] Toplam {finalSuggestions.Count} öneri döndürülüyor");
                    foreach (var suggestion in finalSuggestions)
                    {
                        Console.WriteLine($"[SUGGESTIONS] → '{suggestion.Text}' (FREKANS: {suggestion.Frequency}, güven: {suggestion.Confidence:P0}, tip: {suggestion.Type})");
                    }

                    // Öneri sayısını artır
                    _learningData.TotalSuggestionsGiven += finalSuggestions.Count;
                    _hasUnsavedChanges = true;

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
                      .Where(w => !string.IsNullOrEmpty(w) && IsValidWord(w))
                      .ToList();
        }

        private bool IsValidWord(string word)
        {
            // Rakam içeren kelimeleri filtrele (güvenlik için)
            if (word.Any(char.IsDigit))
                return false;

            // Sadece rakamlardan oluşan kelimeleri filtrele (güvenlik için)
            if (word.All(char.IsDigit))
                return false;

            // Sadece noktalama işaretlerinden oluşan kelimeleri filtrele
            if (word.All(c => char.IsPunctuation(c) || char.IsSymbol(c)))
                return false;

            // En az bir harf içermeli
            if (!word.Any(char.IsLetter))
                return false;

            return true;
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
                var newCount = _learningData.Bigrams.AddOrUpdate(bigram, 1, (key, oldValue) => oldValue + 1);
            }

            // Trigrams
            for (int i = 0; i < words.Count - 2; i++)
            {
                var trigram = $"{words[i]} {words[i + 1]} {words[i + 2]}";
                var newCount = _learningData.Trigrams.AddOrUpdate(trigram, 1, (key, oldValue) => oldValue + 1);
            }

            // 4-grams
            for (int i = 0; i < words.Count - 3; i++)
            {
                var fourgram = $"{words[i]} {words[i + 1]} {words[i + 2]} {words[i + 3]}";
                var newCount = _learningData.FourGrams.AddOrUpdate(fourgram, 1, (key, oldValue) => oldValue + 1);
                Console.WriteLine($"[4GRAM] '{fourgram}' → frekans: {newCount}");
            }

            // Cümle başlangıcı kalıplarını öğren
            UpdateSentenceStartPatterns(words);
        }

        private void UpdateSentenceStartPatterns(List<string> words)
        {
            if (words.Count < 2) return;

            var fullSentence = string.Join(" ", words);
            var firstWord = words[0].ToLowerInvariant();

            // 1. Cümle başlangıcı kalıpları (START_ prefix ile)
            var sentenceStartKey = $"START_{firstWord}";
            if (!_learningData.CompletionPrefixes.ContainsKey(sentenceStartKey))
            {
                _learningData.CompletionPrefixes[sentenceStartKey] = new List<string>();
            }

            // Her seferinde cümleyi ekle (frekans için)
            _learningData.CompletionPrefixes[sentenceStartKey].Add(fullSentence);
            Console.WriteLine($"[SENTENCE_START] Cümle başlangıç kalıbı öğrenildi: '{firstWord}' → '{fullSentence}' (toplam: {_learningData.CompletionPrefixes[sentenceStartKey].Count(s => s == fullSentence)})");

            // 2. Tüm alt dizileri kaydet (cümle tamamlama için)
            for (int i = 0; i < words.Count - 1; i++)
            {
                for (int j = i + 1; j <= words.Count; j++)
                {
                    var subSequence = string.Join(" ", words.Skip(i).Take(j - i));
                    var subKey = string.Join(" ", words.Skip(i).Take(Math.Min(3, j - i))); // İlk 3 kelimeyi key olarak kullan

                    if (subSequence.Split(' ').Length >= 2) // En az 2 kelimeli alt diziler
                    {
                        if (!_learningData.CompletionPrefixes.ContainsKey(subKey))
                        {
                            _learningData.CompletionPrefixes[subKey] = new List<string>();
                        }

                        // Her seferinde cümleyi ekle (frekans için)
                        _learningData.CompletionPrefixes[subKey].Add(fullSentence);
                        var currentCount = _learningData.CompletionPrefixes[subKey].Count(s => s == fullSentence);
                        Console.WriteLine($"[SENTENCE_PATTERN] Alt dizi kalıbı öğrenildi: '{subKey}' → '{fullSentence}' (frekans: {currentCount})");
                    }
                }
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
            Console.WriteLine($"[N-GRAM] GetNextWordPredictions başlıyor, kelime sayısı: {words.Count}");

            // Tüm kaynaklardan önerileri topla ve sonra frekansa göre sırala
            var allSuggestions = new List<SmartSuggestion>();

            // 1. Tam cümle kalıplarından öneriler
            var sentenceCompletions = GetSentenceCompletions(words, maxSuggestions * 2);
            allSuggestions.AddRange(sentenceCompletions);
            Console.WriteLine($"[SENTENCE_COMPLETION] {sentenceCompletions.Count} öneri bulundu");

            // 2. 4-gram tabanlı tahminler
            if (words.Count >= 3)
            {
                var trigramKey = $"{words[words.Count - 3]} {words[words.Count - 2]} {words[words.Count - 1]}";
                Console.WriteLine($"[4-GRAM] Aranan trigram context: '{trigramKey}'");

                var fourgramCandidates = _learningData.FourGrams
                    .Where(kvp => kvp.Key.StartsWith(trigramKey + " ", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kvp => kvp.Value);

                foreach (var fourgram in fourgramCandidates)
                {
                    var parts = fourgram.Key.Split(' ');
                    if (parts.Length == 4)
                    {
                        var nextWord = parts[3];
                        if (!allSuggestions.Any(s => s.Text.Equals(nextWord, StringComparison.OrdinalIgnoreCase)))
                        {
                            // 4-gram için yüksek güven skoru (0.85-0.98 arası)
                            var confidence = Math.Min(0.98, 0.85 + (fourgram.Value / 10.0 * 0.13));

                            allSuggestions.Add(new SmartSuggestion
                            {
                                Text = nextWord,
                                Confidence = confidence,
                                Context = trigramKey,
                                Frequency = fourgram.Value,
                                Type = SuggestionType.NextWord,
                                LastUsed = DateTime.Now
                            });

                            Console.WriteLine($"[4-GRAM] Bulundu: '{nextWord}' (güven: {confidence:P0}, frekans: {fourgram.Value})");
                        }
                    }
                }
            }

            // 3. Trigram tabanlı tahminler
            if (words.Count >= 2)
            {
                var bigramKey = $"{words[words.Count - 2]} {words[words.Count - 1]}";
                Console.WriteLine($"[TRIGRAM] Aranan bigram context: '{bigramKey}'");

                var trigramCandidates = _learningData.Trigrams
                    .Where(kvp => kvp.Key.StartsWith(bigramKey + " ", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kvp => kvp.Value);

                Console.WriteLine($"[TRIGRAM] Bulunan trigram adayları:");
                foreach (var candidate in trigramCandidates)
                {
                    Console.WriteLine($"[TRIGRAM] - '{candidate.Key}' (frekans: {candidate.Value})");
                }

                foreach (var trigram in trigramCandidates)
                {
                    var parts = trigram.Key.Split(' ');
                    if (parts.Length == 3)
                    {
                        var nextWord = parts[2];
                        if (!allSuggestions.Any(s => s.Text.Equals(nextWord, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Trigram için orta-yüksek güven skoru (0.75-0.95 arası)
                            var confidence = Math.Min(0.95, 0.75 + (trigram.Value / 15.0 * 0.20));

                            allSuggestions.Add(new SmartSuggestion
                            {
                                Text = nextWord,
                                Confidence = confidence,
                                Context = bigramKey,
                                Frequency = trigram.Value,
                                Type = SuggestionType.NextWord,
                                LastUsed = DateTime.Now
                            });

                            Console.WriteLine($"[TRIGRAM] Eklendi: '{nextWord}' (güven: {confidence:P0}, frekans: {trigram.Value})");
                        }
                        else
                        {
                            Console.WriteLine($"[TRIGRAM] Atlandı (zaten var): '{nextWord}' (frekans: {trigram.Value})");
                        }
                    }
                }
            }

            // 4. Bigram tabanlı tahminler
            if (words.Count >= 1)
            {
                var lastWord = words.Last();
                Console.WriteLine($"[BIGRAM] Aranan unigram context: '{lastWord}'");

                var bigramCandidates = _learningData.Bigrams
                    .Where(kvp => kvp.Key.StartsWith(lastWord + " ", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kvp => kvp.Value);

                foreach (var bigram in bigramCandidates)
                {
                    var parts = bigram.Key.Split(' ');
                    if (parts.Length == 2)
                    {
                        var nextWord = parts[1];
                        if (!allSuggestions.Any(s => s.Text.Equals(nextWord, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Bigram için orta güven skoru (0.60-0.85 arası)
                            var confidence = Math.Min(0.85, 0.60 + (bigram.Value / 20.0 * 0.25));

                            allSuggestions.Add(new SmartSuggestion
                            {
                                Text = nextWord,
                                Confidence = confidence,
                                Context = lastWord,
                                Frequency = bigram.Value,
                                Type = SuggestionType.NextWord,
                                LastUsed = DateTime.Now
                            });

                            Console.WriteLine($"[BIGRAM] Bulundu: '{nextWord}' (güven: {confidence:P0}, frekans: {bigram.Value})");
                        }
                    }
                }
            }

            // 5. Unigram tabanlı tahminler (en düşük öncelik) - SADECE CONTEXT YOKSA
            if (words.Count == 0)
            {
                Console.WriteLine($"[UNIGRAM] Context yok, en sık kullanılan kelimelerden öneriler alınıyor");

                var unigramCandidates = _learningData.WordFrequencies
                    .Where(kvp => kvp.Value >= 2) // En az 2 kez kullanılmış olmalı
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(3); // Maksimum 3 unigram önerisi

                foreach (var word in unigramCandidates)
                {
                    if (!allSuggestions.Any(s => s.Text.Equals(word.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        var confidence = Math.Min(0.3, word.Value / 200.0 + 0.05); // Çok düşük güven

                        allSuggestions.Add(new SmartSuggestion
                        {
                            Text = word.Key,
                            Confidence = confidence,
                            Context = "genel",
                            Frequency = word.Value,
                            Type = SuggestionType.NextWord,
                            LastUsed = DateTime.Now
                        });

                        Console.WriteLine($"[UNIGRAM] Bulundu: '{word.Key}' (güven: {confidence:P0}, frekans: {word.Value})");
                    }
                }
            }
            else if (words.Count > 0)
            {
                Console.WriteLine($"[UNIGRAM] Context mevcut ('{string.Join(" ", words)}'), unigram önerileri atlanıyor");
            }

            Console.WriteLine($"[N-GRAM] Toplam {allSuggestions.Count} öneri bulundu");

            Console.WriteLine($"[N-GRAM] Tüm öneriler (sıralama öncesi):");
            foreach (var suggestion in allSuggestions)
            {
                Console.WriteLine($"[N-GRAM] - '{suggestion.Text}' (güven: {suggestion.Confidence:P1}, frekans: {suggestion.Frequency}, context: '{suggestion.Context}', tip: {suggestion.Type})");
            }

            // Tüm önerileri frekansa göre sırala (aynı kelime için en yüksek frekansı al)
            var groupedSuggestions = allSuggestions
                .GroupBy(s => s.Text.ToLowerInvariant())
                .Select(g => {
                    var best = g.OrderByDescending(s => s.Frequency).ThenByDescending(s => s.Confidence).First();
                    Console.WriteLine($"[N-GRAM] Grup '{g.Key}': En iyi → frekans: {best.Frequency}, güven: {best.Confidence:P1}, tip: {best.Type}");
                    return best;
                })
                .OrderByDescending(s => s.Frequency)
                .ThenByDescending(s => s.Confidence)
                .Take(maxSuggestions)
                .ToList();

            Console.WriteLine($"[N-GRAM] Final sıralanmış öneriler:");
            foreach (var suggestion in groupedSuggestions)
            {
                Console.WriteLine($"[N-GRAM] - '{suggestion.Text}' (güven: {suggestion.Confidence:P1}, frekans: {suggestion.Frequency}, context: '{suggestion.Context}', tip: {suggestion.Type})");
            }



            return groupedSuggestions;
        }

        private List<SmartSuggestion> GetSentenceStartSuggestions(List<string> words, int maxSuggestions)
        {
            var suggestions = new List<SmartSuggestion>();

            if (words.Count == 0) return suggestions;

            var firstWord = words[0].ToLowerInvariant();
            var sentenceStartKey = $"START_{firstWord}";

            Console.WriteLine($"[SENTENCE_START] Cümle başlangıcı kontrol ediliyor: '{firstWord}'");

            if (_learningData.CompletionPrefixes.ContainsKey(sentenceStartKey))
            {
                var patterns = _learningData.CompletionPrefixes[sentenceStartKey];
                Console.WriteLine($"[SENTENCE_START] {patterns.Count} kalıp bulundu");

                foreach (var pattern in patterns.Take(maxSuggestions))
                {
                    var patternWords = pattern.Split(' ');

                    // Eğer mevcut context pattern'in başlangıcıyla eşleşiyorsa
                    if (patternWords.Length > words.Count)
                    {
                        bool matches = true;
                        for (int i = 0; i < words.Count; i++)
                        {
                            if (!patternWords[i].Equals(words[i], StringComparison.OrdinalIgnoreCase))
                            {
                                matches = false;
                                break;
                            }
                        }

                        if (matches)
                        {
                            // Sonraki kelimeyi öner
                            var nextWord = patternWords[words.Count];
                            var remainingText = string.Join(" ", patternWords.Skip(words.Count));

                            suggestions.Add(new SmartSuggestion
                            {
                                Text = nextWord,
                                Confidence = 0.99, // En yüksek güven - tam kalıp eşleşmesi
                                Context = string.Join(" ", words),
                                Frequency = 1,
                                Type = SuggestionType.SentenceCompletion,
                                LastUsed = DateTime.Now
                            });

                            Console.WriteLine($"[SENTENCE_START] Kalıp eşleşmesi: '{nextWord}' (kalan: '{remainingText}')");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"[SENTENCE_START] '{firstWord}' için kalıp bulunamadı");
            }

            return suggestions.Take(maxSuggestions).ToList();
        }

        private List<SmartSuggestion> GetSentenceCompletions(List<string> words, int maxSuggestions)
        {
            var suggestions = new List<SmartSuggestion>();

            if (words.Count == 0) return suggestions;

            var currentContext = string.Join(" ", words).ToLowerInvariant();
            Console.WriteLine($"[SENTENCE_COMPLETION] Cümle tamamlama aranıyor: '{currentContext}'");

            // Uzun context için özel işlem - son 8 kelimeyi kullan
            var contextForMatching = currentContext;
            if (words.Count > 8)
            {
                var lastWords = words.TakeLast(8).ToList();
                contextForMatching = string.Join(" ", lastWords).ToLowerInvariant();
                Console.WriteLine($"[SENTENCE_COMPLETION] Uzun context tespit edildi, son 8 kelime kullanılıyor: '{contextForMatching}'");
            }

            // Sonraki kelime ve frekanslarını topla
            var nextWordFrequencies = new Dictionary<string, int>();

            Console.WriteLine($"[SENTENCE_COMPLETION] CompletionPrefixes toplam anahtar sayısı: {_learningData.CompletionPrefixes.Count}");

            // Tüm öğrenilen cümleleri kontrol et
            foreach (var sentenceKey in _learningData.CompletionPrefixes.Keys)
            {
                if (sentenceKey.StartsWith("START_")) continue; // Cümle başlangıcı kalıplarını atla

                var sentences = _learningData.CompletionPrefixes[sentenceKey];
                Console.WriteLine($"[SENTENCE_COMPLETION] Anahtar: '{sentenceKey}' → {sentences.Count} cümle");

                foreach (var sentence in sentences)
                {
                    var sentenceLower = sentence.ToLowerInvariant();
                    Console.WriteLine($"[SENTENCE_COMPLETION] Kontrol ediliyor: '{sentenceLower}' vs '{contextForMatching}'");

                    // Hem tam context hem de kısaltılmış context ile eşleştir
                    bool isMatch = false;
                    string matchingContext = "";

                    // Önce tam context ile dene
                    if (sentenceLower.StartsWith(currentContext + " ") || sentenceLower.Equals(currentContext))
                    {
                        isMatch = true;
                        matchingContext = currentContext;
                    }
                    // Sonra kısaltılmış context ile dene (uzun metinler için)
                    else if (contextForMatching != currentContext &&
                             (sentenceLower.Contains(" " + contextForMatching + " ") ||
                              sentenceLower.StartsWith(contextForMatching + " ") ||
                              sentenceLower.Equals(contextForMatching)))
                    {
                        isMatch = true;
                        matchingContext = contextForMatching;
                        Console.WriteLine($"[SENTENCE_COMPLETION] ✅ Kısaltılmış context ile eşleşme: '{contextForMatching}'");
                    }

                    if (isMatch)
                    {
                        Console.WriteLine($"[SENTENCE_COMPLETION] ✅ Eşleşme bulundu: '{sentenceLower}'");

                        // Eşleşen context'in pozisyonunu bul
                        int contextIndex = sentenceLower.IndexOf(matchingContext);
                        if (contextIndex >= 0)
                        {
                            // Kalan kısmı al
                            var startIndex = contextIndex + matchingContext.Length;
                            if (startIndex < sentence.Length)
                            {
                                var remaining = sentence.Substring(startIndex).Trim();
                                Console.WriteLine($"[SENTENCE_COMPLETION] Kalan kısım: '{remaining}'");

                                if (!string.IsNullOrEmpty(remaining))
                                {
                                    // İlk kelimeyi al (sonraki kelime önerisi için)
                                    var nextWords = remaining.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (nextWords.Length > 0)
                                    {
                                        var nextWord = nextWords[0].ToLowerInvariant();

                                        // Bu sonraki kelimenin frekansını artır
                                        if (!nextWordFrequencies.ContainsKey(nextWord))
                                            nextWordFrequencies[nextWord] = 0;
                                        nextWordFrequencies[nextWord]++;

                                        Console.WriteLine($"[SENTENCE_COMPLETION] ✅ Kelime eklendi: '{nextWord}' (toplam frekans: {nextWordFrequencies[nextWord]})");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[SENTENCE_COMPLETION] ❌ Eşleşme yok");
                    }
                }
            }

            // Frekans verilerine göre önerileri oluştur
            foreach (var kvp in nextWordFrequencies.OrderByDescending(x => x.Value).Take(maxSuggestions))
            {
                var nextWord = kvp.Key;
                var frequency = kvp.Value;
                var confidence = Math.Min(0.99, frequency / 10.0 + 0.5); // Yüksek güven

                suggestions.Add(new SmartSuggestion
                {
                    Text = nextWord,
                    Confidence = confidence,
                    Context = currentContext,
                    Frequency = frequency,
                    Type = SuggestionType.SentenceCompletion,
                    LastUsed = DateTime.Now
                });

                Console.WriteLine($"[SENTENCE_COMPLETION] Öneri eklendi: '{nextWord}' (güven: {confidence:P0}, frekans: {frequency})");
            }

            return suggestions;
        }

        private List<SmartSuggestion> GetSentenceContinuations(List<string> words, int maxSuggestions)
        {
            var suggestions = new List<SmartSuggestion>();

            if (words.Count == 0) return suggestions;

            // Uzun metinler için daha fazla context kullan
            int contextRange = words.Count > 10 ? 5 : 3; // Uzun metinlerde 5 kelimeye kadar context

            // Son 1-contextRange kelimeye göre devam önerileri
            for (int i = Math.Max(0, words.Count - contextRange); i < words.Count; i++)
            {
                var context = string.Join(" ", words.Skip(i));
                Console.WriteLine($"[SENTENCE_CONTINUATION] Context aranıyor: '{context}' (uzunluk: {words.Count})");

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
                                Confidence = Math.Min(0.85, 0.60 + (bigram.Value / 20.0 * 0.25)), // Tutarlı bigram confidence hesaplama
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
                                Confidence = Math.Min(0.95, 0.75 + (trigram.Value / 15.0 * 0.20)), // Tutarlı trigram confidence hesaplama
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

                // Doğruluk oranını hesapla
                var totalSuggestions = _learningData.TotalSuggestionsGiven;
                var acceptedSuggestions = _learningData.TotalSuggestionsAccepted;
                var accuracyScore = totalSuggestions > 0 ? (double)acceptedSuggestions / totalSuggestions : 0.0;

                return new LearningStatistics
                {
                    TotalUniqueWords = _learningData.WordFrequencies.Count,
                    TotalWordCount = _learningData.WordFrequencies.Values.Sum(),
                    TotalBigrams = _learningData.Bigrams.Count,
                    TotalTrigrams = _learningData.Trigrams.Count,
                    TotalFourGrams = _learningData.FourGrams.Count,
                    CompletionPrefixes = _learningData.CompletionPrefixes.Count,
                    UserCorrections = _learningData.UserCorrections.Count,
                    MostCommonWords = mostCommonWords,
                    LastLearningSession = _learningData.LastUpdated,
                    AccuracyScore = accuracyScore,
                    AveragePredictionTime = 0.0, // Şimdilik sabit
                    TotalLearningTime = TimeSpan.FromMinutes(Math.Max(1, _learningData.WordFrequencies.Count / 10)), // Basit hesaplama
                    TotalSuggestionsGiven = _learningData.TotalSuggestionsGiven,
                    TotalSuggestionsAccepted = _learningData.TotalSuggestionsAccepted,
                    TotalSuggestionsRejected = _learningData.TotalSuggestionsRejected
                };
            }
        }

        public async Task<DetailedStatistics> GetDetailedStatisticsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    // Doğruluk oranını hesapla
                    var totalSuggestions = _learningData.TotalSuggestionsGiven;
                    var acceptedSuggestions = _learningData.TotalSuggestionsAccepted;
                    var accuracyScore = totalSuggestions > 0 ? (double)acceptedSuggestions / totalSuggestions : 0.0;

                    return new DetailedStatistics
                    {
                        TotalUniqueWords = _learningData.WordFrequencies.Count,
                        TotalWordCount = _learningData.WordFrequencies.Values.Sum(),
                        TotalBigrams = _learningData.Bigrams.Count,
                        TotalTrigrams = _learningData.Trigrams.Count,
                        TotalFourGrams = _learningData.FourGrams.Count,
                        CompletionPrefixes = _learningData.CompletionPrefixes.Count,
                        UserCorrections = _learningData.UserCorrections.Count,
                        MostCommonWords = _learningData.WordFrequencies
                            .OrderByDescending(kvp => kvp.Value)
                            .Take(20)
                            .Select(kvp => (kvp.Key, kvp.Value))
                            .ToList(),
                        AveragePredictionTime = 0.0,
                        AccuracyScore = accuracyScore,
                        LastLearningSession = _learningData.LastUpdated,
                        TotalLearningTime = TimeSpan.FromMinutes(Math.Max(1, _learningData.WordFrequencies.Count / 10)),
                        TotalSuggestionsGiven = _learningData.TotalSuggestionsGiven,
                        TotalSuggestionsAccepted = _learningData.TotalSuggestionsAccepted,
                        TotalSuggestionsRejected = _learningData.TotalSuggestionsRejected,
                        WordsByLength = _learningData.WordFrequencies.GroupBy(w => w.Key.Length.ToString())
                            .ToDictionary(g => g.Key, g => g.Sum(w => w.Value)),
                        BigramsByFrequency = _learningData.Bigrams.ToDictionary(b => b.Key, b => b.Value),
                        TrigramsByFrequency = _learningData.Trigrams.ToDictionary(t => t.Key, t => t.Value),
                        FourGramsByFrequency = _learningData.FourGrams.ToDictionary(f => f.Key, f => f.Value),
                        WordsByFrequency = _learningData.WordFrequencies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
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

                        // Mevcut verilerden rakam içeren kelimeleri temizle
                        CleanInvalidWordsFromData();
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

                    // Boş dosyayı hemen oluştur
                    Console.WriteLine($"[LEARNING] Boş veri dosyası oluşturuluyor: {_dataFilePath}");
                    SaveLearningData();
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda yeni veri ile başla
                _learningData = new LearningData();
                Console.WriteLine($"[ERROR] Öğrenme verisi yükleme hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");

                // Hata durumunda da boş dosyayı oluştur
                try
                {
                    Console.WriteLine($"[LEARNING] Hata sonrası boş veri dosyası oluşturuluyor: {_dataFilePath}");
                    SaveLearningData();
                }
                catch (Exception saveEx)
                {
                    Console.WriteLine($"[ERROR] Boş dosya oluşturma hatası: {saveEx.Message}");
                }
            }
        }

        private void CleanInvalidWordsFromData()
        {
            Console.WriteLine($"[LEARNING] Geçersiz kelimeleri temizleme başlıyor...");

            // WordFrequencies'den geçersiz kelimeleri temizle
            var invalidWords = _learningData.WordFrequencies.Keys.Where(word => !IsValidWord(word)).ToList();
            Console.WriteLine($"[LEARNING] {invalidWords.Count} geçersiz kelime bulundu: {string.Join(", ", invalidWords.Take(10))}");
            foreach (var word in invalidWords)
            {
                _learningData.WordFrequencies.TryRemove(word, out _);
            }

            // Bigrams'den geçersiz kelimeleri temizle
            var invalidBigrams = _learningData.Bigrams.Keys.Where(bigram =>
                bigram.Split(' ').Any(word => !IsValidWord(word))).ToList();
            Console.WriteLine($"[LEARNING] {invalidBigrams.Count} geçersiz bigram bulundu");
            foreach (var bigram in invalidBigrams)
            {
                _learningData.Bigrams.TryRemove(bigram, out _);
            }

            // Trigrams'den geçersiz kelimeleri temizle
            var invalidTrigrams = _learningData.Trigrams.Keys.Where(trigram =>
                trigram.Split(' ').Any(word => !IsValidWord(word))).ToList();
            Console.WriteLine($"[LEARNING] {invalidTrigrams.Count} geçersiz trigram bulundu");
            foreach (var trigram in invalidTrigrams)
            {
                _learningData.Trigrams.TryRemove(trigram, out _);
            }

            // CompletionPrefixes'den geçersiz kelimeleri temizle
            var invalidPrefixes = new List<string>();
            foreach (var kvp in _learningData.CompletionPrefixes)
            {
                var validCompletions = kvp.Value.Where(word => IsValidWord(word)).ToList();
                if (validCompletions.Count == 0)
                {
                    invalidPrefixes.Add(kvp.Key);
                }
                else
                {
                    _learningData.CompletionPrefixes[kvp.Key] = validCompletions;
                }
            }
            foreach (var prefix in invalidPrefixes)
            {
                _learningData.CompletionPrefixes.TryRemove(prefix, out _);
            }

            Console.WriteLine($"[LEARNING] Temizleme tamamlandı. Kalan kelime sayısı: {_learningData.WordFrequencies.Count}");

            // Temizleme sonrası verileri kaydet
            SaveLearningData();
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

        // Veri Yönetimi Fonksiyonları
        public bool UpdateWordFrequency(string oldWord, string newWord, int newCount)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_learningData.WordFrequencies.ContainsKey(oldWord))
                    {
                        _learningData.WordFrequencies.TryRemove(oldWord, out _);
                    }

                    if (!string.IsNullOrWhiteSpace(newWord) && newCount > 0)
                    {
                        _learningData.WordFrequencies[newWord] = newCount;
                    }

                    _hasUnsavedChanges = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Kelime güncelleme hatası: {ex.Message}");
                    return false;
                }
            }
        }

        public bool DeleteWord(string word)
        {
            lock (_lockObject)
            {
                try
                {
                    bool removed = _learningData.WordFrequencies.TryRemove(word, out _);
                    if (removed)
                    {
                        // İlgili bigram ve trigramları da temizle
                        var bigramsToRemove = _learningData.Bigrams.Keys
                            .Where(b => b.Split(' ').Contains(word))
                            .ToList();

                        foreach (var bigram in bigramsToRemove)
                        {
                            _learningData.Bigrams.TryRemove(bigram, out _);
                        }

                        var trigramsToRemove = _learningData.Trigrams.Keys
                            .Where(t => t.Split(' ').Contains(word))
                            .ToList();

                        foreach (var trigram in trigramsToRemove)
                        {
                            _learningData.Trigrams.TryRemove(trigram, out _);
                        }

                        // Completion prefixes'leri temizle
                        var prefixesToRemove = _learningData.CompletionPrefixes.Keys
                            .Where(p => _learningData.CompletionPrefixes[p].Contains(word))
                            .ToList();

                        foreach (var prefix in prefixesToRemove)
                        {
                            _learningData.CompletionPrefixes[prefix].Remove(word);
                            if (_learningData.CompletionPrefixes[prefix].Count == 0)
                            {
                                _learningData.CompletionPrefixes.TryRemove(prefix, out _);
                            }
                        }

                        _hasUnsavedChanges = true;
                    }
                    return removed;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Kelime silme hatası: {ex.Message}");
                    return false;
                }
            }
        }

        public bool UpdateBigram(string oldBigram, string newBigram, int newCount)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_learningData.Bigrams.ContainsKey(oldBigram))
                    {
                        _learningData.Bigrams.TryRemove(oldBigram, out _);
                    }

                    if (!string.IsNullOrWhiteSpace(newBigram) && newCount > 0)
                    {
                        _learningData.Bigrams[newBigram] = newCount;
                    }

                    _hasUnsavedChanges = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Bigram güncelleme hatası: {ex.Message}");
                    return false;
                }
            }
        }

        public bool DeleteBigram(string bigram)
        {
            lock (_lockObject)
            {
                try
                {
                    bool removed = _learningData.Bigrams.TryRemove(bigram, out _);
                    if (removed)
                    {
                        _hasUnsavedChanges = true;
                    }
                    return removed;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Bigram silme hatası: {ex.Message}");
                    return false;
                }
            }
        }

        public bool UpdateTrigram(string oldTrigram, string newTrigram, int newCount)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_learningData.Trigrams.ContainsKey(oldTrigram))
                    {
                        _learningData.Trigrams.TryRemove(oldTrigram, out _);
                    }

                    if (!string.IsNullOrWhiteSpace(newTrigram) && newCount > 0)
                    {
                        _learningData.Trigrams[newTrigram] = newCount;
                    }

                    _hasUnsavedChanges = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Trigram güncelleme hatası: {ex.Message}");
                    return false;
                }
            }
        }

        public bool DeleteTrigram(string trigram)
        {
            lock (_lockObject)
            {
                try
                {
                    bool removed = _learningData.Trigrams.TryRemove(trigram, out _);
                    if (removed)
                    {
                        _hasUnsavedChanges = true;
                    }
                    return removed;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Trigram silme hatası: {ex.Message}");
                    return false;
                }
            }
        }

        public List<(string Word, int Count)> SearchWords(string searchTerm, int maxResults = 50)
        {
            lock (_lockObject)
            {
                return _learningData.WordFrequencies
                    .Where(kvp => kvp.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(maxResults)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList();
            }
        }

        public List<(string Bigram, int Count)> SearchBigrams(string searchTerm, int maxResults = 50)
        {
            lock (_lockObject)
            {
                return _learningData.Bigrams
                    .Where(kvp => kvp.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(maxResults)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList();
            }
        }

        public List<(string Trigram, int Count)> SearchTrigrams(string searchTerm, int maxResults = 50)
        {
            lock (_lockObject)
            {
                return _learningData.Trigrams
                    .Where(kvp => kvp.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(maxResults)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList();
            }
        }
    }
}
