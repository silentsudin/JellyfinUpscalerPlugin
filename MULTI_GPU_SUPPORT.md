# Multi-GPU Support Guide - Jellyfin Upscaler Plugin

## Overview

The Jellyfin Upscaler Plugin now supports multiple GPU types through dedicated Docker containers, allowing users to choose the optimal runtime based on their hardware.

## Supported GPU Types

### 🟢 NVIDIA GPUs (CUDA)
- **Dockerfile**: `Dockerfile` (default)
- **Base Image**: `nvidia/cuda:12.2.2-devel-ubuntu22.04`
- **Acceleration**: CUDA 12.2 with PyTorch
- **Requirements**: NVIDIA GPU with CUDA support

### 🔵 Intel GPUs (IPEX)
- **Dockerfile**: `Dockerfile.intel`
- **Base Image**: `intel/intel-extension-for-pytorch:2.1.10-xpu-pip-base`
- **Acceleration**: Intel XPU with Intel Extension for PyTorch
- **Requirements**: Intel Arc GPU or Intel integrated graphics

### 🔴 AMD GPUs (ROCm)
- **Dockerfile**: `Dockerfile.amd`
- **Base Image**: `rocm/pytorch:rocm6.0_ubuntu22.04_py3.10_pytorch_2.1.1`
- **Acceleration**: ROCm 6.0 with PyTorch
- **Requirements**: AMD GPU with ROCm support

## Quick Start

### Check Your GPU Type
```bash
# NVIDIA GPU
nvidia-smi

# Intel GPU  
intel_gpu_top

# AMD GPU
rocm-smi
```

### Build and Deploy

#### NVIDIA GPU (Default)
```bash
# Single service
docker-compose up

# Or with explicit build
docker build -f docker/upscaler-service/Dockerfile -t jellyfin-upscaler-nvidia .
docker run --gpus all -p 8765:8765 jellyfin-upscaler-nvidia
```

#### Intel GPU
```bash
# Using profiles
docker-compose -f docker-compose-multi-gpu.yml --profile intel up

# Or direct build
docker build -f docker/upscaler-service/Dockerfile.intel -t jellyfin-upscaler-intel .
docker run --device /dev/dri:/dev/dri -p 8765:8765 jellyfin-upscaler-intel
```

#### AMD GPU
```bash
# Using profiles
docker-compose -f docker-compose-multi-gpu.yml --profile amd up

# Or direct build
docker build -f docker/upscaler-service/Dockerfile.amd -t jellyfin-upscaler-amd .
docker run --device /dev/kfd:/dev/kfd --device /dev/dri:/dev/dri -p 8765:8765 jellyfin-upscaler-amd
```

## Configuration Differences

### Environment Variables

#### NVIDIA
```yaml
environment:
  - UPSCALER_DEVICE_TYPE=cuda
  - NVIDIA_VISIBLE_DEVICES=all
  - NVIDIA_DRIVER_CAPABILITIES=compute,utility
```

#### Intel
```yaml
environment:
  - UPSCALER_DEVICE_TYPE=xpu
  - UPSCALER_USE_INTEL_GPU=true
  - SYCL_PI_LEVEL_ZERO_USE_IMMEDIATE_COMMANDLISTS=1
```

#### AMD
```yaml
environment:
  - UPSCALER_DEVICE_TYPE=cuda  # AMD uses CUDA API
  - UPSCALER_USE_AMD_GPU=true
  - HIP_VISIBLE_DEVICES=0
  - ROCM_VERSION=6.0
```

### Device Access

#### NVIDIA
```yaml
deploy:
  resources:
    reservations:
      devices:
        - driver: nvidia
          count: 1
          capabilities: [gpu]
```

#### Intel
```yaml
devices:
  - /dev/dri:/dev/dri
```

#### AMD
```yaml
devices:
  - /dev/kfd:/dev/kfd
  - /dev/dri:/dev/dri
group_add:
  - video
```

## Performance Characteristics

### NVIDIA GPUs
- **Best Performance**: Generally highest throughput for AI upscaling
- **Memory**: Excellent VRAM utilization
- **Models**: Full compatibility with all AI models
- **Precision**: Supports FP16 for faster processing

### Intel GPUs
- **Good Performance**: Competitive performance on Arc GPUs
- **Memory**: Shared system memory on integrated GPUs
- **Models**: Full compatibility via Intel Extension for PyTorch
- **Precision**: FP32 recommended (FP16 support varies)

### AMD GPUs
- **Good Performance**: Strong performance on modern RDNA GPUs
- **Memory**: Good VRAM utilization via ROCm
- **Models**: Full compatibility via ROCm PyTorch
- **Precision**: FP32 recommended for stability

## Testing and Verification

### Run Multi-GPU Tests
```bash
./test-multi-gpu-build.sh
```

This script will:
- Build all three Docker variants
- Test container startup
- Verify health endpoints
- Provide deployment recommendations

### Manual Testing
```bash
# Test specific GPU type
docker build -f docker/upscaler-service/Dockerfile.intel -t test-intel .
docker run --rm -p 8765:8765 test-intel

# Check health endpoint
curl http://localhost:8765/health
```

### Expected Health Response
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "models_available": ["RealESRGAN_x4plus", "RealESRGAN_x2plus"],
  "gpu_available": true,
  "active_jobs": 0
}
```

## Troubleshooting

### NVIDIA Issues
```bash
# Check NVIDIA runtime
docker run --rm --gpus all nvidia/cuda:12.2.2-base-ubuntu22.04 nvidia-smi

# Verify container toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker
```

### Intel Issues
```bash
# Check Intel GPU access
ls -la /dev/dri/

# Verify Intel GPU tools
intel_gpu_top

# Check IPEX installation
python3 -c "import intel_extension_for_pytorch as ipex; print(ipex.__version__)"
```

### AMD Issues
```bash
# Check ROCm installation
rocm-smi

# Verify AMD GPU access
ls -la /dev/kfd /dev/dri/

# Check ROCm PyTorch
python3 -c "import torch; print(torch.version.hip)"
```

## Production Deployment

### Docker Compose Override
Create `docker-compose.override.yml`:

#### For NVIDIA
```yaml
version: '3.8'
services:
  jellyfin-upscaler-sidecar:
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
```

#### For Intel
```yaml
version: '3.8'
services:
  jellyfin-upscaler-sidecar:
    build:
      dockerfile: Dockerfile.intel
    devices:
      - /dev/dri:/dev/dri
    environment:
      - UPSCALER_DEVICE_TYPE=xpu
```

#### For AMD
```yaml
version: '3.8'
services:
  jellyfin-upscaler-sidecar:
    build:
      dockerfile: Dockerfile.amd
    devices:
      - /dev/kfd:/dev/kfd
      - /dev/dri:/dev/dri
    group_add:
      - video
    environment:
      - HIP_VISIBLE_DEVICES=0
```

### Kubernetes Deployment
Example pod specification for NVIDIA:
```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
  - name: upscaler
    image: jellyfin-upscaler-nvidia
    resources:
      limits:
        nvidia.com/gpu: 1
```

## File Structure

```
docker/upscaler-service/
├── Dockerfile                 # NVIDIA CUDA (default)
├── Dockerfile.intel          # Intel IPEX  
├── Dockerfile.amd            # AMD ROCm
├── requirements.txt          # NVIDIA requirements
├── requirements-intel.txt    # Intel requirements
├── requirements-amd.txt      # AMD requirements
└── src/                      # Shared application code
    ├── upscaler_engine.py    # Multi-GPU detection and support
    └── ...
```

## Migration Guide

### From Single GPU to Multi-GPU

1. **Identify your GPU type**:
   ```bash
   # Check GPU vendor
   lspci | grep -E "VGA|3D"
   ```

2. **Choose appropriate Dockerfile**:
   - NVIDIA: `Dockerfile` (no change needed)
   - Intel: `Dockerfile.intel`
   - AMD: `Dockerfile.amd`

3. **Update docker-compose.yml**:
   - Use `docker-compose-multi-gpu.yml` as reference
   - Add appropriate device access and environment variables

4. **Test deployment**:
   ```bash
   ./test-multi-gpu-build.sh
   ```

## Performance Tuning

### Memory Optimization
```yaml
environment:
  - UPSCALER_MAX_CONCURRENT_JOBS=1  # Reduce for limited VRAM
  - UPSCALER_TILE_SIZE=200          # Smaller tiles for less memory
```

### Quality vs Speed
```yaml
environment:
  - UPSCALER_USE_HALF_PRECISION=false  # Higher quality, slower
  - UPSCALER_BATCH_SIZE=1              # Lower memory usage
```

## Future Enhancements

- **Auto-detection**: Automatic GPU type detection
- **Hybrid processing**: CPU + GPU workload distribution  
- **Multi-GPU**: Support for multiple GPUs simultaneously
- **Cloud deployment**: GPU instances on AWS/GCP/Azure

## Support Matrix

| GPU Type | Supported | Performance | Memory Efficiency | Model Compatibility |
|----------|-----------|-------------|-------------------|-------------------|
| NVIDIA   | ✅ Full    | Excellent   | Excellent         | 100%              |
| Intel    | ✅ Full    | Good        | Good              | 100%              |
| AMD      | ✅ Full    | Good        | Good              | 100%              |
| CPU      | ✅ Fallback| Poor        | Good              | 100%              |

The multi-GPU support provides flexibility to deploy on any hardware while maintaining full AI upscaling capabilities and streaming performance.
