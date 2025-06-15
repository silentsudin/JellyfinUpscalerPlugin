# Docker Build Troubleshooting Guide

## Issue: Package Installation Errors

### Problem
```
Package libgl1-mesa-glx is not available, but is referred to by another package.
E: Package 'libgl1-mesa-glx' has no installation candidate
```

### Root Cause
The original Dockerfile was using Ubuntu 24.04, which is very recent and some packages have changed names or been removed.

### Solution
✅ **Fixed**: Updated to use Ubuntu 22.04 LTS for better package compatibility.

## Changes Made

### 1. Base Image Updated
- **Before**: `nvidia/cuda:12.9.0-devel-ubuntu24.04`
- **After**: `nvidia/cuda:12.2-devel-ubuntu22.04`

### 2. Package Dependencies Fixed
- **Removed**: `libgl1-mesa-glx` (not available in Ubuntu 24.04)
- **Added**: `libgl1-mesa-dev`, `libegl1-mesa`, `libgles2-mesa`
- **Added**: Better CA certificates and GPG handling

### 3. PyTorch Version Matched
- **Updated**: CUDA version to match base image (cu121)
- **Added**: `facexlib` dependency for better AI model support

## Available Dockerfiles

### 1. Main Dockerfile (Recommended)
- **File**: `Dockerfile`
- **Base**: Ubuntu 22.04 + CUDA 12.2
- **Status**: ✅ Fixed package issues

### 2. Alternative Dockerfile
- **File**: `Dockerfile.ubuntu22`
- **Base**: Ubuntu 22.04 + CUDA 12.2 (explicit)
- **Purpose**: Backup option if main fails

## Testing

### Quick Test
```bash
cd /Users/holzr/github/JellyfinUpscalerPlugin
./docker-build-test.sh
```

### Manual Test
```bash
cd docker/upscaler-service
docker build -t jellyfin-upscaler-service:test .
```

### Full Integration Test
```bash
docker-compose up --build
```

## Expected Output

### Successful Build
```
✅ Ubuntu 22.04 build successful!
✅ Service health check passed!
🎯 Container is ready for production use
```

### Service Health Check
```bash
curl http://localhost:8765/health
```

Should return:
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "models_available": ["RealESRGAN_x4plus", ...],
  "gpu_available": true,
  "active_jobs": 0
}
```

## GPU Support

### NVIDIA (Default)
```yaml
deploy:
  resources:
    reservations:
      devices:
        - driver: nvidia
          count: 1
          capabilities: [gpu]
```

### AMD ROCm
```yaml
devices:
  - /dev/kfd:/dev/kfd
  - /dev/dri:/dev/dri
group_add:
  - video
```

### Intel
```yaml
devices:
  - /dev/dri:/dev/dri
group_add:
  - video
```

## Common Issues

### 1. NVIDIA Runtime Not Found
**Error**: `could not select device driver "" with capabilities: [[gpu]]`
**Solution**: Install nvidia-container-runtime
```bash
# Ubuntu/Debian
sudo apt-get install nvidia-container-runtime
sudo systemctl restart docker
```

### 2. Permission Denied
**Error**: `permission denied while trying to connect to the Docker daemon`
**Solution**: Add user to docker group
```bash
sudo usermod -aG docker $USER
newgrp docker
```

### 3. Out of Memory
**Error**: `CUDA out of memory`
**Solution**: Reduce batch size or concurrent jobs
```yaml
environment:
  - UPSCALER_MAX_CONCURRENT_JOBS=1
```

## Next Steps

1. ✅ Build completed successfully
2. ✅ Container starts and responds to health checks
3. 🔄 Test with actual video files
4. 🔄 Deploy to production environment
5. 🔄 Monitor performance and adjust settings

## Support

If you continue to experience issues:

1. Check the container logs: `docker logs jellyfin-upscaler-sidecar`
2. Verify GPU access: `docker run --rm --gpus all nvidia/cuda:12.2-base-ubuntu22.04 nvidia-smi`
3. Test without GPU: Set `UPSCALER_USE_GPU=false`
