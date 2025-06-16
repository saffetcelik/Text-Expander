using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtomatikMetinGenisletici.Helpers;

namespace OtomatikMetinGenisletici.Services
{
    public class KeyboardListenerService : BackgroundService
    {
        private readonly IKeyboardHookService _keyboardHookService;
        private readonly IShortcutService _shortcutService;
        private readonly ILogger<KeyboardListenerService> _logger;
        private string _contextBuffer = string.Empty;

        public KeyboardListenerService(
            IKeyboardHookService keyboardHookService,
            IShortcutService shortcutService,
            ILogger<KeyboardListenerService> logger)
        {
            _keyboardHookService = keyboardHookService;
            _shortcutService = shortcutService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Keyboard Listener Service started");

            // Subscribe to keyboard events
            _keyboardHookService.KeyPressed += OnKeyPressed;
            _keyboardHookService.WordCompleted += OnWordCompleted;

            // Start listening
            _keyboardHookService.StartListening();

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("Keyboard Listener Service stopping");
        }

        private void OnKeyPressed(string buffer)
        {
            _contextBuffer = buffer;
            _logger.LogDebug("Key pressed: {Buffer}", buffer);
        }

        private void OnWordCompleted(string word)
        {
            _contextBuffer += word;
            if (_contextBuffer.Length > 200)
            {
                _contextBuffer = _contextBuffer.Substring(_contextBuffer.Length - 200);
            }

            _logger.LogDebug("Word completed: {Word}", word);

            // Kısayol genişletme kontrolü (sadece bu uygulama aktif değilse)
            if (WindowHelper.ShouldTextExpansionBeActive() &&
                _shortcutService.TryExpandShortcut(word.TrimEnd(), out string expansion))
            {
                _logger.LogInformation("Shortcut expanded: {Word} -> {Expansion}", word.TrimEnd(), expansion);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _keyboardHookService.KeyPressed -= OnKeyPressed;
            _keyboardHookService.WordCompleted -= OnWordCompleted;
            _keyboardHookService.StopListening();

            await base.StopAsync(cancellationToken);
        }
    }
}
