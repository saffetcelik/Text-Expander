namespace OtomatikMetinGenisletici.Services
{
    public interface INotificationService
    {
        void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);
        void ShowTrayNotification(string title, string message);
        void HideTrayIcon();
        void ShowTrayIcon();
        void SetMainWindow(MainWindow mainWindow);
        bool IsTrayVisible { get; }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
