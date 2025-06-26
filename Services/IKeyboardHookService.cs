namespace OtomatikMetinGenisletici.Services
{
    public interface IKeyboardHookService : IDisposable
    {
        event Action<string>? KeyPressed;
        event Action<string>? WordCompleted;
        event Action<string>? SentenceCompleted;
        event Action? CtrlSpacePressed;
        event Func<bool>? TabPressed; // Func<bool> - true döndürürse Tab engellenir
        event Action<string>? SpacePressed;

        void StartListening();
        void StopListening();
        bool IsListening { get; }
    }
}
