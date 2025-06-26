using System.Windows;

namespace OtomatikMetinGenisletici.Models
{
    public class TourStep
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TargetElement { get; set; } = string.Empty;
        public TourStepPosition Position { get; set; } = TourStepPosition.Bottom;
        public string Icon { get; set; } = "ℹ️";
        public string ActionText { get; set; } = "Devam";
        public bool IsSkippable { get; set; } = true;
        public int Duration { get; set; } = 5000; // ms
        public TourStepType Type { get; set; } = TourStepType.Information;
        public string? ActionTarget { get; set; } // Hangi butona tıklanacak
        public Point? CustomPosition { get; set; } // Manuel pozisyon
    }

    public enum TourStepPosition
    {
        Top,
        Bottom,
        Left,
        Right,
        Center,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Custom
    }

    public enum TourStepType
    {
        Information,
        Action,
        Highlight,
        Demo
    }
}
