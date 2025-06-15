using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.UpscalerPlugin.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the selected Real-ESRGAN profile.
    /// </summary>
    public string SelectedProfile { get; set; } = "General";

    /// <summary>
    /// Gets or sets a value indicating whether benchmark test is enabled.
    /// </summary>
    public bool EnableBenchmark { get; set; } = true;

    /// <summary>
    /// Gets or sets the sidecar service URL.
    /// </summary>
    public string SidecarServiceUrl { get; set; } = "http://jellyfin-upscaler-sidecar:8765";

    /// <summary>
    /// Gets or sets the connection timeout for sidecar service in seconds.
    /// </summary>
    public int SidecarTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically start upscaling jobs.
    /// </summary>
    public bool AutoUpscaleEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the Real-ESRGAN settings.
    /// </summary>
    public RealESRGANSettings RealESRGANSettings { get; set; } = new();
}

/// <summary>
/// Real-ESRGAN specific settings for the plugin.
/// </summary>
public class RealESRGANSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable GPU acceleration.
    /// </summary>
    public bool EnableGPUAcceleration { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum resolution for AI upscaling.
    /// </summary>
    public string MinResolutionForAI { get; set; } = "720p";

    /// <summary>
    /// Gets or sets the maximum resolution for AI upscaling.
    /// </summary>
    public string MaxResolutionForAI { get; set; } = "4K";

    /// <summary>
    /// Gets or sets the target upscale factor (2x or 4x).
    /// </summary>
    public int UpscaleFactor { get; set; } = 4;
}
