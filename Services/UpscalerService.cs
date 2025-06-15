using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.UpscalerPlugin.Configuration;

namespace Jellyfin.Plugin.UpscalerPlugin.Services;

/// <summary>
/// Service responsible for video upscaling logic.
/// </summary>
public class UpscalerService
{
    private readonly ILogger<UpscalerService> _logger;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpscalerService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="mediaEncoder">Media encoder instance.</param>
    /// <param name="libraryManager">Library manager instance.</param>
    public UpscalerService(
        ILogger<UpscalerService> logger,
        IMediaEncoder mediaEncoder,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _mediaEncoder = mediaEncoder;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Determines the appropriate upscaling method based on content and configuration.
    /// </summary>
    /// <param name="mediaItem">The media item to be processed.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>Upscaling parameters.</returns>
    public UpscalingParameters DetermineUpscalingMethod(BaseItem mediaItem, PluginConfiguration config)
    {
        var parameters = new UpscalingParameters();

        // Get video stream info
        var mediaInfo = mediaItem.GetMediaSources(true).FirstOrDefault();
        if (mediaInfo?.MediaStreams == null)
        {
            _logger.LogWarning("No media streams found for item {ItemId}", mediaItem.Id);
            return parameters;
        }

        var videoStream = mediaInfo.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
        if (videoStream == null)
        {
            _logger.LogWarning("No video stream found for item {ItemId}", mediaItem.Id);
            return parameters;
        }

        // Determine content type
        var contentType = DetermineContentType(mediaItem);

        // Apply Real-ESRGAN profile-based settings
        switch (config.SelectedProfile.ToLowerInvariant())
        {
            case "general":
                parameters = GetGeneralProfile(videoStream);
                break;
            case "anime":
                parameters = GetAnimeProfile(videoStream);
                break;
            case "face":
                parameters = GetFaceProfile(videoStream);
                break;
            case "compact":
                parameters = GetCompactProfile(videoStream);
                break;
            default:
                parameters = GetGeneralProfile(videoStream);
                break;
        }

        _logger.LogInformation(
            "Selected upscaling method {Method} for {ItemName} (Resolution: {Width}x{Height})",
            parameters.Method,
            mediaItem.Name,
            videoStream.Width,
            videoStream.Height);

        return parameters;
    }

    /// <summary>
    /// Generates FFmpeg filters for upscaling.
    /// </summary>
    /// <param name="parameters">Upscaling parameters.</param>
    /// <returns>FFmpeg filter string.</returns>
    public string GenerateFFmpegFilters(UpscalingParameters parameters)
    {
        var filters = new List<string>();

        switch (parameters.Method)
        {
            case UpscalingMethod.RealESRGAN:
                // Real-ESRGAN processing is handled by the sidecar service
                // This fallback provides basic high-quality scaling for compatibility
                filters.Add($"scale={parameters.TargetWidth}:{parameters.TargetHeight}:flags=lanczos");
                break;
        }

        return string.Join(",", filters);
    }

    private ContentType DetermineContentType(BaseItem mediaItem)
    {
        // Check parent folder or genre to determine content type
        if (mediaItem.Genres?.Any(g => g.Contains("anime", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return ContentType.Anime;
        }

        if (mediaItem is Movie)
        {
            return ContentType.Movie;
        }

        if (mediaItem is Episode)
        {
            return ContentType.TVShow;
        }

        return ContentType.Unknown;
    }

    private UpscalingParameters GetGeneralProfile(MediaStream videoStream)
    {
        var resolution = GetTargetResolution(videoStream, 4); // 4x upscale

        return new UpscalingParameters
        {
            Method = UpscalingMethod.RealESRGAN,
            ModelName = "RealESRGAN_x4plus",
            TargetWidth = resolution.Width,
            TargetHeight = resolution.Height,
            Profile = "General"
        };
    }

    private UpscalingParameters GetAnimeProfile(MediaStream videoStream)
    {
        var resolution = GetTargetResolution(videoStream, 4); // 4x upscale

        return new UpscalingParameters
        {
            Method = UpscalingMethod.RealESRGAN,
            ModelName = "RealESRGANv2-animevideo-xsx4",
            TargetWidth = resolution.Width,
            TargetHeight = resolution.Height,
            Profile = "Anime"
        };
    }

    private UpscalingParameters GetFaceProfile(MediaStream videoStream)
    {
        var resolution = GetTargetResolution(videoStream, 4); // 4x upscale

        return new UpscalingParameters
        {
            Method = UpscalingMethod.RealESRGAN,
            ModelName = "GFPGANv1.4",
            TargetWidth = resolution.Width,
            TargetHeight = resolution.Height,
            Profile = "Face"
        };
    }

    private UpscalingParameters GetCompactProfile(MediaStream videoStream)
    {
        var resolution = GetTargetResolution(videoStream, 2); // 2x upscale for speed

        return new UpscalingParameters
        {
            Method = UpscalingMethod.RealESRGAN,
            ModelName = "RealESRGAN_x2plus",
            TargetWidth = resolution.Width,
            TargetHeight = resolution.Height,
            Profile = "Compact"
        };
    }

    private (int Width, int Height) GetTargetResolution(MediaStream videoStream, int scaleFactor = 4)
    {
        var currentWidth = videoStream.Width ?? 1920;
        var currentHeight = videoStream.Height ?? 1080;

        // Apply the specified scale factor
        return (currentWidth * scaleFactor, currentHeight * scaleFactor);
    }

    private static int ResolutionToHeight(string resolution)
    {
        return resolution.ToLowerInvariant() switch
        {
            "480p" => 480,
            "720p" => 720,
            "1080p" => 1080,
            "1440p" => 1440,
            "2160p" => 2160,
            "4320p" => 4320,
            _ => 1080
        };
    }
}

/// <summary>
/// Parameters for Real-ESRGAN upscaling operation.
/// </summary>
public class UpscalingParameters
{
    /// <summary>
    /// Gets or sets the upscaling method.
    /// </summary>
    public UpscalingMethod Method { get; set; } = UpscalingMethod.RealESRGAN;

    /// <summary>
    /// Gets or sets the Real-ESRGAN model name.
    /// </summary>
    public string ModelName { get; set; } = "RealESRGAN_x4plus";

    /// <summary>
    /// Gets or sets the Real-ESRGAN profile.
    /// </summary>
    public string Profile { get; set; } = "General";

    /// <summary>
    /// Gets or sets the target width.
    /// </summary>
    public int TargetWidth { get; set; }

    /// <summary>
    /// Gets or sets the target height.
    /// </summary>
    public int TargetHeight { get; set; }
}

/// <summary>
/// Available upscaling methods.
/// </summary>
public enum UpscalingMethod
{
    /// <summary>
    /// Real-ESRGAN AI upscaling.
    /// </summary>
    RealESRGAN
}

/// <summary>
/// Content type enumeration.
/// </summary>
public enum ContentType
{
    /// <summary>
    /// Unknown content type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Anime content.
    /// </summary>
    Anime,

    /// <summary>
    /// Movie content.
    /// </summary>
    Movie,

    /// <summary>
    /// TV show content.
    /// </summary>
    TVShow
}
