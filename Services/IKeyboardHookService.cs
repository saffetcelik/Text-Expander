namespace OtomatikMetinGenisletici.Services
{
    public interface IKeyboardHookService : IDisposable
    {
        event Action<string>? KeyPressed;
        event Action<string>? WordCompleted;
        event Action<string>? SentenceCompleted;
        event Action? CtrlSpacePressed;
        event Action? TabPressed;
        event Action<string>? SpacePressed;

        void StartListening();
        void StopListening();
        bool IsListening { get; }
    }
}
