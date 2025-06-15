# Real-ESRGAN Simplification - Plugin Refactor

## Overview

The Jellyfin Upscaler Plugin has been simplified to focus exclusively on **Real-ESRGAN** AI upscaling, removing complex shader interpolation, noise reduction, and other post-processing features.

## What Was Removed

### 🗑️ Removed Features
- **Shader Interpolation**: Bicubic, Bilinear, and Lanczos GLSL shaders
- **Noise Reduction**: AI-powered denoising settings
- **Color/Contrast Adjustments**: Manual sharpness, saturation, contrast controls
- **Client-side Processing**: TensorFlow.js and browser-based upscaling
- **Waifu2x Support**: Removed in favor of Real-ESRGAN only
- **Complex Configuration**: Simplified settings focused on profile selection

### 📁 Removed Files/Directories
- `shaders/` directory (bicubic.glsl, bilinear.glsl, lanczos.glsl)
- `models/ESRGAN/` and `models/Waifu2x/` directories
- Complex benchmark testing in `upscale.js`

## New Simplified Approach

### 🎯 **Profile-Based Model Selection**

Instead of manual model selection, users choose from content-optimized profiles:

| Profile | Model | Best For | Upscale Factor |
|---------|-------|----------|----------------|
| **General** | RealESRGAN_x4plus | Live action movies, TV shows, photos | 4x |
| **Anime** | RealESRGANv2-animevideo-xsx4 | Anime, cartoons, drawn content | 4x |
| **Face** | GFPGANv1.4 | Faces and portraits | 4x |
| **Compact** | RealESRGAN_x2plus | Fast processing, real-time use | 2x |

### 🔧 **Simplified Configuration**

#### Old Configuration (Removed):
```csharp
public class CustomSettings {
    public bool EnableFPSRule { get; set; }
    public string MaxFPSForAI { get; set; }
    public string DefaultShaderBelowMinResolution { get; set; }
    public string DefaultShaderAboveMaxResolution { get; set; }
    public int Sharpness { get; set; }
    public int Saturation { get; set; }
    public double Contrast { get; set; }
    public int Denoising { get; set; }
}
```

#### New Configuration:
```csharp
public class RealESRGANSettings {
    public bool EnableGPUAcceleration { get; set; } = true;
    public string MinResolutionForAI { get; set; } = "720p";
    public string MaxResolutionForAI { get; set; } = "4K";
    public int UpscaleFactor { get; set; } = 4;
}
```

### 🐳 **Sidecar Service Focus**

The Docker sidecar service now exclusively handles Real-ESRGAN processing:

- **Single Purpose**: Only Real-ESRGAN model loading and inference
- **Profile Mapping**: Automatic model selection based on user profile choice
- **Multi-GPU Support**: NVIDIA CUDA, Intel IPEX, AMD ROCm backends
- **Streaming Output**: Real-time video processing with streaming responses

## API Changes

### 📡 **Request Structure**

#### Old:
```json
{
  "model_name": "RealESRGAN_x4plus",
  "sharpness": 2,
  "saturation": 1,
  "contrast": 1.0,
  "denoising": 1
}
```

#### New:
```json
{
  "profile": "General",
  "scale_factor": 4  // optional override
}
```

### 🎛️ **Profile-to-Model Mapping**

```python
def get_model_by_profile(profile: str) -> str:
    profile_map = {
        "General": "RealESRGAN_x4plus",
        "Anime": "RealESRGANv2-animevideo-xsx4", 
        "Face": "GFPGANv1.4",
        "Compact": "RealESRGAN_x2plus"
    }
    return profile_map.get(profile, "RealESRGAN_x4plus")
```

## Benefits of Simplification

### ✅ **User Experience**
- **Easier Setup**: Choose profile based on content type, not technical details
- **Better Results**: Optimized models for specific content types
- **Reduced Complexity**: No manual tuning of sharpness, contrast, etc.

### ✅ **Development & Maintenance**
- **Focused Codebase**: Single AI upscaling approach (Real-ESRGAN)
- **Reduced Dependencies**: No TensorFlow.js, GLSL shaders, or client-side processing
- **Simplified Testing**: Only test Real-ESRGAN models and sidecar connectivity

### ✅ **Performance**
- **GPU Acceleration**: All processing on dedicated GPU via sidecar
- **Optimized Models**: Each profile uses the best model for its content type
- **Streaming Support**: Real-time processing for large video files

## Migration Path

### 🔄 **For Existing Users**
1. **Configuration Migration**: Old settings will map to closest Real-ESRGAN profile
2. **Profile Recommendations**:
   - Movies/TV Shows → **General** profile
   - Anime/Cartoons → **Anime** profile  
   - Portrait content → **Face** profile
   - Quick processing → **Compact** profile

### 🔄 **For Developers**
1. **Remove shader loading code** from client-side components
2. **Update API calls** to use profile-based requests
3. **Test sidecar connectivity** instead of browser GPU capabilities

## File Structure Changes

### Before:
```
JellyfinUpscalerPlugin/
├── shaders/
│   ├── bicubic.glsl
│   ├── bilinear.glsl
│   └── lanczos.glsl
├── models/
│   ├── ESRGAN/
│   └── Waifu2x/
└── upscale.js (complex TensorFlow.js)
```

### After:
```
JellyfinUpscalerPlugin/
├── models/
│   └── README.md (Real-ESRGAN info)
└── upscale.js (simple sidecar testing)
```

## Docker Service Updates

The sidecar service models are now focused on Real-ESRGAN only:

```python
builtin_models = {
    "RealESRGAN_x4plus": {
        "profile": "General",
        "description": "Best for live action content"
    },
    "RealESRGANv2-animevideo-xsx4": {
        "profile": "Anime", 
        "description": "Optimized for anime/cartoon content"
    },
    "GFPGANv1.4": {
        "profile": "Face",
        "description": "Specialized for faces and portraits"
    },
    "RealESRGAN_x2plus": {
        "profile": "Compact",
        "description": "Faster processing with 2x upscaling"
    }
}
```

This refactor creates a more focused, maintainable, and user-friendly Real-ESRGAN upscaling solution!
