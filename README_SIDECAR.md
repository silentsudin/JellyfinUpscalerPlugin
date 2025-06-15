# Jellyfin Upscaler Plugin

A real-time AI video upscaling plugin for Jellyfin that enhances video quality using advanced AI models and GPU acceleration through a sidecar container architecture.

## 🏗️ Architecture

This plugin uses a **sidecar container** approach for optimal performance and flexibility:

- **Jellyfin Plugin**: Handles media detection, configuration, and orchestration
- **Sidecar Service**: Performs AI upscaling with GPU support (CUDA/ROCm/Intel XPU)
- **Docker Compose**: Easy deployment with automatic GPU detection

```
┌─────────────────┐    HTTP API    ┌─────────────────────┐
│  Jellyfin       │◄──────────────►│  Upscaler Sidecar  │
│  + Plugin       │                │  + AI Models        │
│                 │                │  + GPU Support      │
└─────────────────┘                └─────────────────────┘
```

## ✨ Features

### AI Upscaling Models
- **Real-ESRGAN**: Best overall quality and performance
- **ESRGAN**: High-quality upscaling for general content  
- **Waifu2x**: Optimized for anime and cartoon content
- **SwinIR**: Advanced transformer-based upscaling

### GPU Support
- **NVIDIA GPUs**: CUDA acceleration with TensorRT optimization
- **AMD GPUs**: ROCm support for Radeon cards
- **Intel GPUs**: Intel XPU support for Arc and integrated graphics
- **CPU Fallback**: Works without GPU but slower

### Smart Processing
- **Automatic Detection**: Intelligently determines when upscaling is beneficial
- **Quality Profiles**: Pre-configured settings for different content types
- **Real-time Processing**: Low-latency upscaling during playback
- **Batch Processing**: Queue multiple files for background processing

## 🚀 Quick Start with Docker

### Prerequisites
- Docker & Docker Compose
- At least 8GB RAM (16GB+ recommended for 4K)
- GPU with 4GB+ VRAM (optional but recommended)

### Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/Kuschel-code/JellyfinUpscalerPlugin.git
   cd JellyfinUpscalerPlugin
   ```

2. **Run the setup script**:
   ```bash
   chmod +x setup-docker.sh
   ./setup-docker.sh
   ```

3. **Start the services**:
   ```bash
   ./docker-compose.sh up -d
   ```

4. **Configure Jellyfin**:
   - Open http://localhost:8096
   - Complete Jellyfin setup
   - Go to Dashboard → Plugins → Jellyfin Upscaler
   - Configure your preferences

## 📁 Directory Structure

```
jellyfin-upscaler/
├── docker-compose.yml          # Main orchestration
├── setup-docker.sh             # Automated setup script
├── jellyfin/                   # Jellyfin data
│   ├── config/                 # Jellyfin configuration
│   ├── cache/                  # Jellyfin cache
│   └── plugins/                # Plugin installation
├── upscaler/                   # Sidecar service data
│   ├── models/                 # AI models storage
│   ├── temp/                   # Temporary processing files
│   ├── output/                 # Upscaled video output
│   └── logs/                   # Service logs
└── media/                      # Your media files (mount point)
```

## ⚙️ Configuration

### Plugin Settings

| Setting | Description | Default |
|---------|-------------|---------|
| **Sidecar Service URL** | URL of the upscaler service | `http://jellyfin-upscaler-sidecar:8765` |
| **Connection Timeout** | Timeout for sidecar requests | `30 seconds` |
| **Auto Upscale** | Automatically upscale during playback | `false` |
| **Profile** | Quality/performance preset | `Default` |

### Quality Profiles

| Profile | Model | Scale | Performance | Quality |
|---------|-------|-------|-------------|---------|
| **Fast** | Real-ESRGAN | 2x | ⚡⚡⚡ | ⭐⭐⭐ |
| **Balanced** | Real-ESRGAN | 2x | ⚡⚡ | ⭐⭐⭐⭐ |
| **Quality** | ESRGAN | 2x | ⚡ | ⭐⭐⭐⭐⭐ |
| **Anime** | Waifu2x | 2x | ⚡⚡ | ⭐⭐⭐⭐ |
| **Custom** | User-defined | Variable | Variable | Variable |

## 🎮 GPU Setup

### NVIDIA GPUs (Recommended)
```bash
# Install NVIDIA Container Toolkit
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
  sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
  sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit
sudo systemctl restart docker
```

### AMD GPUs
```bash
# Install ROCm
wget https://repo.radeon.com/amdgpu-install/latest/ubuntu/jammy/amdgpu-install_5.7.50700-1_all.deb
sudo dpkg -i amdgpu-install_5.7.50700-1_all.deb
sudo amdgpu-install --usecase=dkms,graphics,multimedia,opencl,hip,rocm
```

### Intel GPUs
```bash
# Install Intel GPU drivers
sudo apt-get update
sudo apt-get install -y intel-media-va-driver-non-free intel-media-va-driver
```

## 📊 Monitoring

### Health Checks
```bash
# Check service status
./docker-compose.sh ps

# View logs
./docker-compose.sh logs -f jellyfin-upscaler-sidecar

# Test upscaler API
curl http://localhost:8765/health
```

### Performance Metrics
- Access Jellyfin admin dashboard for plugin statistics
- Monitor GPU usage: `nvidia-smi` (NVIDIA) or `rocm-smi` (AMD)
- Check processing queue: `curl http://localhost:8765/jobs`

## 🚨 Troubleshooting

### Common Issues

**Plugin not loading**:
- Check Jellyfin logs: `journalctl -u jellyfin -f`
- Verify plugin installation: Plugin should be in `/var/lib/jellyfin/plugins/`
- Restart Jellyfin service

**Sidecar service unreachable**:
- Check Docker network: `docker network ls`
- Verify service health: `curl http://localhost:8765/health`
- Check firewall settings

**GPU not detected**:
- Verify GPU drivers are installed
- Check Docker GPU support: `docker run --rm --gpus all nvidia/cuda:11.0-base-ubuntu20.04 nvidia-smi`
- Review Docker Compose GPU configuration

## 🛠️ Development

### Building from Source
```bash
git clone https://github.com/Kuschel-code/JellyfinUpscalerPlugin.git
cd JellyfinUpscalerPlugin
dotnet build --configuration Release
./package.sh
```

### API Documentation
The sidecar service provides a REST API:
- **Health**: `GET /health`
- **Upscale**: `POST /upscale`
- **Job Status**: `GET /job/{id}/status`
- **Cancel Job**: `DELETE /job/{id}`
- **Models**: `GET /models`
- **Benchmark**: `POST /benchmark`

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 🙏 Acknowledgments

- [Jellyfin](https://jellyfin.org/) - Open-source media server
- [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN) - AI upscaling model
- [BasicSR](https://github.com/XPixelGroup/BasicSR) - Image/video restoration toolkit
- [FastAPI](https://fastapi.tiangolo.com/) - Modern Python web framework
