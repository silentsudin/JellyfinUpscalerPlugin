# GPU Backend Selection Guide

## Quick Start - Choose Your GPU Type

### 🟢 NVIDIA GPU Users (RTX/GTX Series)
```bash
# Use the default CUDA setup
docker-compose up -d
```

### 🔵 Intel GPU Users (Arc/Iris Xe)
```bash
# Use Intel IPEX setup
docker-compose --profile intel -f docker-compose-multi-gpu.yml up -d
```

### 🔴 AMD GPU Users (Radeon RX Series)
```bash
# Use AMD ROCm setup
docker-compose --profile amd -f docker-compose-multi-gpu.yml up -d
```

## How It Works

The plugin automatically detects your GPU hardware at runtime and uses the appropriate acceleration:

1. **NVIDIA CUDA**: For RTX/GTX cards with CUDA cores
2. **Intel IPEX**: For Intel Arc discrete GPUs and Iris Xe integrated graphics
3. **AMD ROCm**: For Radeon RX series cards with ROCm support
4. **CPU Fallback**: When no compatible GPU is detected

## Hardware Requirements

| GPU Type | Minimum | Recommended | VRAM |
|----------|---------|-------------|------|
| NVIDIA | GTX 1060 6GB | RTX 4060+ | 6GB+ |
| Intel | Arc A380 | Arc A750+ | 4GB+ |
| AMD | RX 6600 XT | RX 7700 XT+ | 8GB+ |

## Performance Expectations

- **4K → 4K upscaling**: ~2-5 seconds per frame (GPU dependent)
- **1080p → 4K upscaling**: ~1-3 seconds per frame
- **Real-time streaming**: Possible with high-end GPUs (RTX 4080+, Arc A770)

For detailed setup instructions, see [MULTI_GPU_SUPPORT.md](MULTI_GPU_SUPPORT.md).
