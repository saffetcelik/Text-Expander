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

        // Yeni tuş kombinasyonu event'leri
        event Action<string>? EnterPressed;
        event Action<string>? ShiftSpacePressed;
        event Action<string>? AltSpacePressed;
        event Action<string>? CtrlEnterPressed;
        event Action<string>? ShiftEnterPressed;
        event Action<string>? AltEnterPressed;

        void StartListening();
        void StopListening();
        bool IsListening { get; }

        /// <summary>
        /// Tab ile eklenen metni sentence buffer'a ekler
        /// </summary>
        void AddTabCompletedTextToSentenceBuffer(string text);
    }
}
