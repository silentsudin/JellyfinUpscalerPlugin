# AI Models Directory

This directory will contain the AI upscaling models used by the service.

## Model Download

Models will be automatically downloaded on first use or can be pre-loaded.

## Supported Models

- **RealESRGAN_x4plus**: General purpose 4x upscaling
- **RealESRGAN_x2plus**: Fast 2x upscaling  
- **RealESRGANv2-animevideo-xsx4**: Optimized for anime content

## Volume Mounting

In production, mount this directory as a volume to persist downloaded models:

```yaml
volumes:
  - ./upscaler/models:/app/models
```

## Model Files

Models will be downloaded to this directory structure:
```
models/
├── RealESRGAN_x4plus.pth
├── RealESRGAN_x2plus.pth
└── RealESRGANv2-animevideo-xsx4.pth
```
