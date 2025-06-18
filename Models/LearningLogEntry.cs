using System;

namespace OtomatikMetinGenisletici.Models
{
    public class LearningLogEntry
    {
        public int Id { get; set; }
        public string Time { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Sentence { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public DateTime Timestamp { get; set; }
        
        public LearningLogEntry()
        {
        }
        
        public LearningLogEntry(string sentence, DateTime timestamp)
        {
            Sentence = sentence;
            Timestamp = timestamp;
            Time = timestamp.ToString("HH:mm:ss");
            Date = timestamp.ToString("dd.MM.yyyy");
            WordCount = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
