using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.UpscalerPlugin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Jellyfin.Plugin.UpscalerPlugin;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Jellyfin Upscaler";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("f87f700e-679d-43e6-9c7c-b3a410dc3f12");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = "JellyfinUpscalerPlugin.Configuration.config.html"
            }
        };
    }

    /// <summary>
    /// Register plugin services with dependency injection.
    /// </summary>
    /// <param name="serviceCollection">Service collection.</param>
    public static void RegisterServices(IServiceCollection serviceCollection)
    {
        // Register HTTP client for sidecar communication
        serviceCollection.AddHttpClient("JellyfinUpscaler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register plugin services
        serviceCollection.AddSingleton<Services.SidecarUpscalerService>();
        serviceCollection.AddSingleton<Playback.PlaybackUpscalerManager>();
    }
}
