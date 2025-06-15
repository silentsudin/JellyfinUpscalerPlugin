using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.UpscalerPlugin.Services;
using Jellyfin.Plugin.UpscalerPlugin.Playback;
using Jellyfin.Plugin.UpscalerPlugin.Transcoding;

namespace Jellyfin.Plugin.UpscalerPlugin;

/// <summary>
/// Plugin service that handles initialization and cleanup.
/// </summary>
public class UpscalerPluginService : BackgroundService
{
    private readonly ILogger<UpscalerPluginService> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpscalerPluginService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="serviceProvider">Service provider instance.</param>
    public UpscalerPluginService(
        ILogger<UpscalerPluginService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Jellyfin Upscaler Plugin service started");

        try
        {
            // Initialize services
            var upscalerService = _serviceProvider.GetService<UpscalerService>();
            var playbackManager = _serviceProvider.GetService<PlaybackUpscalerManager>();
            var transcodingHelper = _serviceProvider.GetService<UpscalerTranscodingHelper>();

            _logger.LogInformation("Upscaler services initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize upscaler services");
        }

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Jellyfin Upscaler Plugin service stopped");
    }
}
