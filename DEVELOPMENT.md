# Development and Testing Guide

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Modern web browser with WebGL support
- Jellyfin 10.10.3+ (for testing)

### Building the Plugin

```bash
# Clone the repository
git clone https://github.com/Kuschel-code/JellyfinUpscalerPlugin.git
cd JellyfinUpscalerPlugin

# Restore dependencies
dotnet restore

# Build in debug mode
dotnet build --configuration Debug

# Build for release
dotnet build --configuration Release

# Run automated installation
chmod +x install.sh
./install.sh
```

## Testing the Plugin

### 1. Benchmark Testing
Open a web browser and run:
```javascript
// Load the benchmark module
import('./upscale.js').then(module => {
    // Run comprehensive benchmark
    module.runBenchmarkTest().then(results => {
        console.log('Benchmark Results:', results);
    });
});
```

### 2. Client-side Processing Test
```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/@tensorflow/tfjs@4.0.0/dist/tf.min.js"></script>
    <script src="Web/upscaler-client.js"></script>
</head>
<body>
    <video controls src="test-video.mp4"></video>
    <script>
        // The upscaler will automatically enhance the video
        console.log('Upscaler loaded:', window.jellyfinUpscaler);
    </script>
</body>
</html>
```

### 3. Shader Testing
Test individual shader performance:
```javascript
// Test shader compilation
const canvas = document.createElement('canvas');
const gl = canvas.getContext('webgl2');

// Load and compile shaders
fetch('shaders/bicubic.glsl')
    .then(response => response.text())
    .then(shaderSource => {
        // Compile and test shader
        console.log('Shader loaded successfully');
    });
```

## Plugin Architecture

### Core Components

1. **Plugin.cs** - Main plugin entry point
2. **PluginConfiguration.cs** - Configuration management
3. **UpscalerService.cs** - Core upscaling logic
4. **UpscalerTranscodingProvider.cs** - FFmpeg integration
5. **PlaybackUpscalerManager.cs** - Real-time processing
6. **upscaler-client.js** - Client-side enhancement

### Data Flow

```
Video Playback Request
        ↓
Content Analysis (UpscalerService)
        ↓
Method Selection (AI vs Shader)
        ↓
Real-time Processing (Client/Server)
        ↓
Enhanced Video Output
```

## Debugging

### Enable Debug Logging
Add to Jellyfin `logging.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Jellyfin.Plugin.UpscalerPlugin": "Debug"
      }
    }
  }
}
```

### Browser Debugging
```javascript
// Enable verbose logging
localStorage.setItem('jellyfinUpscalerDebug', 'true');

// Check benchmark results
console.log(localStorage.getItem('jellyfinUpscalerBenchmark'));

// Monitor processing performance
window.jellyfinUpscaler?.enablePerformanceMonitoring();
```

### Common Issues

#### Performance Problems
- Check GPU acceleration is enabled
- Verify WebGL support: `about:gpu` in Chrome
- Monitor browser performance tab during playback
- Test with lower resolution content first

#### Build Failures
```bash
# Clean and rebuild
dotnet clean
dotnet restore --force
dotnet build --verbosity detailed
```

#### Plugin Not Loading
- Check Jellyfin logs: `journalctl -u jellyfin -f`
- Verify plugin directory permissions
- Ensure all dependencies are installed

## Performance Benchmarks

Run comprehensive benchmarks:
```bash
# JavaScript benchmarks
node -e "require('./upscale.js').runBenchmarkTest()"

# Server-side performance test
dotnet test --configuration Release --logger:console
```

Expected performance targets:
- 1080p → 4K upscaling: < 100ms (RTX 4070)
- Traditional shaders: < 20ms
- AI model loading: < 5 seconds
- Benchmark completion: < 30 seconds

## Contributing

### Code Style
- Follow C# conventions for server-side code
- Use ESLint for JavaScript code
- Add XML documentation for public APIs
- Include unit tests for new features

### Pull Request Process
1. Fork the repository
2. Create feature branch
3. Implement changes with tests
4. Update documentation
5. Submit pull request

### Testing Checklist
- [ ] All unit tests pass
- [ ] Benchmark test completes successfully
- [ ] Plugin loads in Jellyfin without errors
- [ ] Video enhancement works with test content
- [ ] Configuration UI functions correctly
- [ ] Performance meets targets

## CI/CD and Release Process

### 🔄 Automated Release Pipeline

This repository uses GitHub Actions to automatically build, test, and release the Jellyfin Upscaler Plugin.

#### 1. **Pull Request Testing** (`test-build.yml`)
- **Triggers**: Pull requests to `main` or `develop`
- **Actions**:
  - Build .NET plugin
  - Test Docker images for all GPU types (CUDA, Intel, AMD)
  - Create test package
- **Purpose**: Ensure code quality before merging

#### 2. **Release Pipeline** (`build-and-release.yml`)
- **Triggers**: Git tags matching `v*` pattern
- **Actions**:
  - Calculate version using GitVersion
  - Build and package .NET plugin
  - Build and push Docker images to GHCR
  - Create GitHub release with assets
  - Update plugin manifest

### 🏷️ Versioning Strategy

Uses **GitVersion** with semantic versioning:
- **Major.Minor.Patch** for stable releases
- **Major.Minor.Patch-alpha.X** for development builds
- **Major.Minor.Patch-beta.X** for release candidates

### 📦 Release Artifacts

Each release creates:

#### Plugin Package
- `JellyfinUpscalerPlugin-vX.X.X.zip` - Ready for Jellyfin installation

#### Docker Images (Published to GHCR)
- `ghcr.io/owner/repo-cuda:vX.X.X` - NVIDIA GPU support
- `ghcr.io/owner/repo-intel:vX.X.X` - Intel GPU support
- `ghcr.io/owner/repo-amd:vX.X.X` - AMD GPU support

### 🚀 Creating a Release

#### Automatic Release (Recommended)
```bash
# Create and push a version tag
git tag v1.2.3
git push origin v1.2.3
```

The GitHub Actions workflow will automatically:
1. Calculate the version using GitVersion
2. Build the .NET plugin
3. Create plugin package with correct version
4. Build all Docker images for different GPU types
5. Push Docker images to GitHub Container Registry
6. Create GitHub release with generated release notes
7. Update manifest.json with new version info

### 🐳 Docker Registry

Images are published to **GitHub Container Registry (GHCR)**:
- `ghcr.io/[owner]/[repo]-cuda:[tag]` - NVIDIA GPU support
- `ghcr.io/[owner]/[repo]-intel:[tag]` - Intel GPU support  
- `ghcr.io/[owner]/[repo]-amd:[tag]` - AMD GPU support

### 📋 Release Checklist

Before creating a release tag:
- [ ] Test all GPU Docker builds locally
- [ ] Verify plugin builds and packages correctly
- [ ] Update documentation if needed
- [ ] Ensure all tests pass on latest commit

---

For detailed API documentation, see the inline code comments and generated XML documentation.
