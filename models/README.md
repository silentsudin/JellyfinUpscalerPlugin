# Real-ESRGAN Models

This directory contains the Real-ESRGAN models used for AI upscaling. Models are downloaded automatically when first used.

## Available Profiles and Models

### General (x4+)
- **Model**: RealESRGAN_x4plus.pth
- **Best for**: Live action content, photographs, movies, TV shows
- **Upscale**: 4x resolution increase
- **Quality**: High

### Anime (x4)
- **Model**: RealESRGANv2-animevideo-xsx4.pth
- **Best for**: Anime, cartoons, drawn content
- **Upscale**: 4x resolution increase  
- **Features**: Preserves sharp lines and vibrant colors

### Face Enhancement
- **Model**: GFPGANv1.4.pth
- **Best for**: Faces and portraits
- **Upscale**: 4x resolution increase
- **Features**: Enhanced facial details and skin texture

### Compact (x2+)
- **Model**: RealESRGAN_x2plus.pth
- **Best for**: Faster processing, real-time use
- **Upscale**: 2x resolution increase
- **Speed**: Fastest processing

## Model Download

Models are automatically downloaded from the official Real-ESRGAN repository when first used. Manual downloads are not required.

## Storage Location

- **Plugin**: `/path/to/jellyfin/plugins/UpscalerPlugin/models/`
- **Docker**: `/app/models/` (mounted from host)

## Model Size

- General (x4+): ~67MB
- Anime (x4): ~67MB  
- Face Enhancement: ~348MB
- Compact (x2+): ~67MB

Total storage required: ~549MB for all models.
