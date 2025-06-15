#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UpscalerPlugin.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UpscalerPlugin.Services;

/// <summary>
/// Service for handling AI upscaling operations via sidecar container.
/// </summary>
public class SidecarUpscalerService
{
    private readonly ILogger<SidecarUpscalerService> _logger;
    private readonly IServerConfigurationManager _config;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly HttpClient _httpClient;
    private readonly string _sidecarBaseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidecarUpscalerService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The server configuration manager.</param>
    /// <param name="mediaEncoder">The media encoder.</param>
    /// <param name="httpClient">The HTTP client.</param>
    public SidecarUpscalerService(
        ILogger<SidecarUpscalerService> logger,
        IServerConfigurationManager config,
        IMediaEncoder mediaEncoder,
        HttpClient httpClient)
    {
        _logger = logger;
        _config = config;
        _mediaEncoder = mediaEncoder;
        _httpClient = httpClient;

        // Get sidecar URL from plugin configuration or environment
        var pluginConfig = Plugin.Instance?.Configuration;
        _sidecarBaseUrl = pluginConfig?.SidecarServiceUrl ??
                         Environment.GetEnvironmentVariable("JELLYFIN_UPSCALER_SIDECAR_URL") ??
                         "http://jellyfin-upscaler-sidecar:8765";

        _logger.LogInformation("SidecarUpscalerService initialized with sidecar URL: {SidecarUrl}", _sidecarBaseUrl);
    }

    /// <summary>
    /// Checks if the sidecar service is available and healthy.
    /// </summary>
    /// <returns>True if service is healthy, false otherwise.</returns>
    public async Task<bool> IsServiceHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_sidecarBaseUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check sidecar service health");
            return false;
        }
    }

    /// <summary>
    /// Starts an upscaling job on the sidecar service.
    /// </summary>
    /// <param name="inputPath">Path to the input video file.</param>
    /// <param name="outputPath">Path for the output video file.</param>
    /// <param name="settings">Upscaling settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Job ID for tracking the upscaling process.</returns>
    public async Task<string> StartUpscaleJobAsync(
        string inputPath,
        string outputPath,
        UpscaleSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpscaleRequest
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                ModelName = settings.ModelName,
                ScaleFactor = settings.ScaleFactor,
                Sharpness = settings.Sharpness,
                Saturation = settings.Saturation,
                Contrast = settings.Contrast,
                Denoising = settings.Denoising,
                UseGpu = settings.UseGpu,
                BatchSize = settings.BatchSize,
                Priority = settings.Priority
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_sidecarBaseUrl}/upscale", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to start upscale job: {response.StatusCode} - {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var upscaleResponse = JsonSerializer.Deserialize<UpscaleResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Started upscale job {JobId} for file {InputPath}", upscaleResponse?.JobId, inputPath);
            return upscaleResponse?.JobId ?? throw new InvalidOperationException("No job ID returned");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start upscale job for {InputPath}", inputPath);
            throw;
        }
    }

    /// <summary>
    /// Starts a streaming upscaling job on the sidecar service for real-time processing.
    /// </summary>
    /// <param name="inputPath">Path to the input video file.</param>
    /// <param name="outputPath">Path for the output video file.</param>
    /// <param name="settings">Upscaling settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Job ID and stream URL for tracking and accessing the upscaling process.</returns>
    public async Task<(string JobId, string StreamUrl)> StartStreamingUpscaleJobAsync(
        string inputPath,
        string outputPath,
        UpscaleSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpscaleRequest
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                ModelName = settings.ModelName,
                ScaleFactor = settings.ScaleFactor,
                Sharpness = settings.Sharpness,
                Saturation = settings.Saturation,
                Contrast = settings.Contrast,
                Denoising = settings.Denoising,
                UseGpu = settings.UseGpu,
                BatchSize = settings.BatchSize,
                Priority = settings.Priority
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_sidecarBaseUrl}/upscale/stream", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to start streaming upscale job: {response.StatusCode} - {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var upscaleResponse = JsonSerializer.Deserialize<UpscaleStreamResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var jobId = upscaleResponse?.JobId ?? throw new InvalidOperationException("No job ID returned");
            var streamUrl = upscaleResponse?.StreamUrl ?? $"{_sidecarBaseUrl}/job/{jobId}/stream";

            _logger.LogInformation("Started streaming upscale job {JobId} for file {InputPath} with stream URL {StreamUrl}",
                jobId, inputPath, streamUrl);

            return (jobId, streamUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start streaming upscale job for {InputPath}", inputPath);
            throw;
        }
    }

    /// <summary>
    /// Gets the status of an upscaling job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Job status information.</returns>
    public async Task<JobStatus?> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_sidecarBaseUrl}/job/{jobId}/status", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                throw new InvalidOperationException($"Failed to get job status: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<JobStatus>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for job {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    /// Cancels an upscaling job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully cancelled.</returns>
    public async Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_sidecarBaseUrl}/job/{jobId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return false;
        }
    }

    /// <summary>
    /// Gets available AI models from the sidecar service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available models.</returns>
    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_sidecarBaseUrl}/models", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get available models: {StatusCode}", response.StatusCode);
                return new List<string> { "real-esrgan", "esrgan", "waifu2x" }; // Default fallback
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var modelsResponse = JsonSerializer.Deserialize<ModelsResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return modelsResponse?.Models ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available models");
            return new List<string> { "real-esrgan", "esrgan", "waifu2x" }; // Default fallback
        }
    }

    /// <summary>
    /// Runs a benchmark test on the sidecar service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Benchmark results.</returns>
    public async Task<BenchmarkResult?> RunBenchmarkAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_sidecarBaseUrl}/benchmark", null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Benchmark failed: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var benchmarkResponse = JsonSerializer.Deserialize<BenchmarkResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return benchmarkResponse?.BenchmarkResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run benchmark");
            throw;
        }
    }

    /// <summary>
    /// Gets a stream of upscaled video data as it's being processed.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream of video data.</returns>
    public async Task<System.IO.Stream> GetUpscaleStreamAsync(string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_sidecarBaseUrl}/job/{jobId}/stream",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to get upscale stream: {response.StatusCode}");
            }

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get upscale stream for job {JobId}", jobId);
            throw;
        }
    }
}

/// <summary>
/// Settings for upscaling operations.
/// </summary>
public class UpscaleSettings
{
    /// <summary>
    /// Gets or sets the AI model name.
    /// </summary>
    public string ModelName { get; set; } = "real-esrgan";

    /// <summary>
    /// Gets or sets the scale factor.
    /// </summary>
    public int ScaleFactor { get; set; } = 2;

    /// <summary>
    /// Gets or sets the sharpness level.
    /// </summary>
    public int Sharpness { get; set; } = 2;

    /// <summary>
    /// Gets or sets the saturation level.
    /// </summary>
    public int Saturation { get; set; } = 1;

    /// <summary>
    /// Gets or sets the contrast level.
    /// </summary>
    public double Contrast { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the denoising level.
    /// </summary>
    public int Denoising { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to use GPU acceleration.
    /// </summary>
    public bool UseGpu { get; set; } = true;

    /// <summary>
    /// Gets or sets the batch size for processing.
    /// </summary>
    public int BatchSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets the job priority.
    /// </summary>
    public int Priority { get; set; } = 5;
}

// DTO classes for API communication
public class UpscaleRequest
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string ModelName { get; set; } = "real-esrgan";
    public int ScaleFactor { get; set; } = 2;
    public int Sharpness { get; set; } = 2;
    public int Saturation { get; set; } = 1;
    public double Contrast { get; set; } = 1.0;
    public int Denoising { get; set; } = 1;
    public bool UseGpu { get; set; } = true;
    public int BatchSize { get; set; } = 4;
    public int Priority { get; set; } = 5;
}

public class UpscaleResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class JobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string InputPath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public double? ProcessingTime { get; set; }
}

public class ModelsResponse
{
    public List<string> Models { get; set; } = new();
    public string DefaultModel { get; set; } = string.Empty;
}

public class BenchmarkResponse
{
    public BenchmarkResult BenchmarkResults { get; set; } = new();
}

public class BenchmarkResult
{
    public string ModelName { get; set; } = string.Empty;
    public double TestDuration { get; set; }
    public double FpsAchieved { get; set; }
    public double GpuUtilization { get; set; }
    public double MemoryUsageMb { get; set; }
    public bool Recommended { get; set; }
}

public class UpscaleStreamResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
}
