using Orleans;

namespace Aetherium.Dashboard.Services
{
    /// <summary>
    /// Hosted service that connects the Orleans client with retry/backoff logic.
    /// </summary>
    public class OrleansClientConnectionService : IHostedService
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<OrleansClientConnectionService>? _logger;

        public OrleansClientConnectionService(IClusterClient clusterClient, ILogger<OrleansClientConnectionService>? logger = null)
        {
            _clusterClient = clusterClient;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Retry connection with exponential backoff
            var maxRetries = 5;
            var baseDelay = TimeSpan.FromSeconds(2);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    await _clusterClient.Connect(cancellationToken);
                    var msg = $"[Dashboard] Orleans client connected successfully (attempt {attempt + 1})";
                    _logger?.LogInformation(msg);
                    Console.WriteLine(msg);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries - 1)
                    {
                        var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                        var msg = $"[Dashboard] Orleans client connection failed (attempt {attempt + 1}/{maxRetries}): {ex.Message}. Retrying in {delay.TotalSeconds:F1}s...";
                        _logger?.LogWarning(ex, msg);
                        Console.WriteLine(msg);
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        var msg = $"[Dashboard] Orleans client connection failed after {maxRetries} attempts. Some features may be unavailable.";
                        _logger?.LogError(ex, msg);
                        Console.WriteLine(msg);
                    }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _clusterClient.Close();
                _logger?.LogInformation("[Dashboard] Orleans client disconnected");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Dashboard] Error disconnecting Orleans client");
            }
        }
    }
}

