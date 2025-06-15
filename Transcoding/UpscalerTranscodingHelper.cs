using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Dlna;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.UpscalerPlugin.Services;

namespace Jellyfin.Plugin.UpscalerPlugin.Transcoding;

/// <summary>
/// Provides custom transcoding options with upscaling support.
/// </summary>
public class UpscalerTranscodingHelper
{
    private readonly ILogger<UpscalerTranscodingHelper> _logger;
    private readonly UpscalerService _upscalerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpscalerTranscodingHelper"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="upscalerService">Upscaler service instance.</param>
    public UpscalerTranscodingHelper(
        ILogger<UpscalerTranscodingHelper> logger,
        UpscalerService upscalerService)
    {
        _logger = logger;
        _upscalerService = upscalerService;
    }

    /// <summary>
    /// Gets the name of the transcoding provider.
    /// </summary>
    public string Name => "Jellyfin Upscaler";

    /// <summary>
    /// Generates FFmpeg command line arguments for upscaling.
    /// </summary>
    /// <param name="sourceFile">Source video file path.</param>
    /// <param name="outputFile">Output video file path.</param>
    /// <returns>FFmpeg arguments string.</returns>
    public string GenerateUpscalingArguments(string sourceFile, string outputFile)
    {
        try
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null)
            {
                return string.Empty;
            }

            // Check if Real-ESRGAN is enabled (use main plugin setting)
            if (config.AutoUpscaleEnabled != true)
            {
                return string.Empty;
            }

            // Use the selected profile from configuration
            var profile = config.SelectedProfile;

            _logger.LogInformation("Using Real-ESRGAN profile: {Profile}", profile);

            // Return basic scaling filter as placeholder - actual Real-ESRGAN processing
            // would be handled by the sidecar service
            return "-vf \"scale=iw*4:ih*4\"";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Real-ESRGAN upscaling arguments");
        }

        return string.Empty;
    }

    /// <summary>
    /// Generates arguments for Real-ESRGAN sidecar processing.
    /// </summary>
    /// <param name="inputFile">Input file path.</param>
    /// <param name="outputFile">Output file path.</param>
    /// <param name="profile">Real-ESRGAN profile to use.</param>
    /// <returns>Command arguments for sidecar service.</returns>
    public string GenerateRealESRGANArguments(string inputFile, string outputFile, string profile)
    {
        try
        {
            var args = new List<string>
            {
                "--input", $"\"{inputFile}\"",
                "--output", $"\"{outputFile}\"",
                "--profile", profile
            };

            var config = Plugin.Instance?.Configuration?.RealESRGANSettings;
            if (config != null)
            {
                if (config.UpscaleFactor != 4)
                {
                    args.AddRange(new[] { "--scale", config.UpscaleFactor.ToString() });
                }
            }

            return string.Join(" ", args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Real-ESRGAN sidecar arguments");
            return string.Empty;
        }
    }

    private static bool IsVideoContent(string mediaPath)
    {
        if (string.IsNullOrEmpty(mediaPath))
        {
            return false;
        }

        var videoExtensions = new[]
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv",
            ".webm", ".m4v", ".3gp", ".ts", ".m2ts", ".mts"
        };

        return videoExtensions.Any(ext =>
            mediaPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
