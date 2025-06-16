using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OtomatikMetinGenisletici.Services
{
    public class AutoSaveService : BackgroundService
    {
        private readonly IShortcutService _shortcutService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<AutoSaveService> _logger;
        private readonly TimeSpan _saveInterval = TimeSpan.FromMinutes(5); // Auto-save every 5 minutes

        public AutoSaveService(
            IShortcutService shortcutService,
            ISettingsService settingsService,
            ILogger<AutoSaveService> logger)
        {
            _shortcutService = shortcutService;
            _settingsService = settingsService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Auto-save service started with interval: {Interval}", _saveInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_saveInterval, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await SaveAllDataAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during auto-save");
                }
            }

            // Final save before stopping
            await SaveAllDataAsync();
            _logger.LogInformation("Auto-save service stopped");
        }

        private async Task SaveAllDataAsync()
        {
            try
            {
                _logger.LogDebug("Performing auto-save...");

                await Task.WhenAll(
                    _shortcutService.SaveShortcutsAsync(),
                    _settingsService.SaveSettingsAsync()
                );

                _logger.LogDebug("Auto-save completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform auto-save");
            }
        }
    }
}
