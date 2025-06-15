#!/bin/bash

# Jellyfin Upscaler Plugin - Docker Setup Script
# This script sets up the complete Jellyfin + Upscaler Sidecar environment

set -e

echo "🚀 Jellyfin Upscaler Plugin - Docker Setup"
echo "=========================================="

# Check requirements
echo "📋 Checking requirements..."

if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

echo "✅ Docker and Docker Compose are available"

# Create directory structure
echo "📁 Creating directory structure..."
mkdir -p {jellyfin/{config,cache,plugins},media,upscaler/{models,temp,output,logs},redis/data}

# Set permissions
echo "🔧 Setting permissions..."
sudo chown -R 1000:1000 jellyfin/ upscaler/ || {
    echo "⚠️  Could not set ownership with sudo. You may need to set permissions manually."
}

# GPU Detection
echo "🎮 Detecting GPU..."
GPU_TYPE="none"

if lspci | grep -i nvidia > /dev/null 2>&1; then
    echo "✅ NVIDIA GPU detected"
    GPU_TYPE="nvidia"
elif lspci | grep -i amd > /dev/null 2>&1; then
    echo "✅ AMD GPU detected"
    GPU_TYPE="amd"
elif lspci | grep -i intel.*graphics > /dev/null 2>&1; then
    echo "✅ Intel GPU detected"
    GPU_TYPE="intel"
else
    echo "ℹ️  No dedicated GPU detected - will use CPU"
fi

# Create environment file
echo "⚙️  Creating environment configuration..."
cat > .env << EOF
# Jellyfin Upscaler Plugin Configuration
PUID=1000
PGID=1000
TZ=\${TZ:-UTC}

# GPU Configuration
GPU_TYPE=${GPU_TYPE}

# Upscaler Settings
UPSCALER_MAX_CONCURRENT_JOBS=2
UPSCALER_USE_GPU=true
UPSCALER_LOG_LEVEL=INFO
EOF

# GPU-specific Docker Compose override
if [ "$GPU_TYPE" = "nvidia" ]; then
    echo "🏗️  Creating NVIDIA GPU Docker Compose override..."
    cat > docker-compose.nvidia.yml << EOF
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
    environment:
      - NVIDIA_VISIBLE_DEVICES=all
      - NVIDIA_DRIVER_CAPABILITIES=compute,utility
EOF
    COMPOSE_FILES="-f docker-compose.yml -f docker-compose.nvidia.yml"

elif [ "$GPU_TYPE" = "amd" ]; then
    echo "🏗️  Creating AMD GPU Docker Compose override..."
    cat > docker-compose.amd.yml << EOF
version: '3.8'

services:
  jellyfin-upscaler-sidecar:
    devices:
      - /dev/kfd:/dev/kfd
      - /dev/dri:/dev/dri
    group_add:
      - video
    environment:
      - ROC_ENABLE_PRE_VEGA=1
EOF
    COMPOSE_FILES="-f docker-compose.yml -f docker-compose.amd.yml"

elif [ "$GPU_TYPE" = "intel" ]; then
    echo "🏗️  Creating Intel GPU Docker Compose override..."
    cat > docker-compose.intel.yml << EOF
version: '3.8'

services:
  jellyfin-upscaler-sidecar:
    devices:
      - /dev/dri:/dev/dri
    group_add:
      - video
    environment:
      - LIBVA_DRIVER_NAME=iHD
EOF
    COMPOSE_FILES="-f docker-compose.yml -f docker-compose.intel.yml"
else
    COMPOSE_FILES="-f docker-compose.yml"
fi

# Build the upscaler service
echo "🔨 Building upscaler service..."
docker-compose $COMPOSE_FILES build jellyfin-upscaler-sidecar

# Download AI models (optional)
echo "🤖 Setting up AI models..."
read -p "Do you want to download AI models now? This will take several GB of space. (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "📥 Downloading AI models..."
    # Create a temporary container to download models
    docker run --rm -v "$(pwd)/upscaler/models:/models" python:3.11-slim bash -c "
        pip install gdown
        cd /models
        echo 'Downloading Real-ESRGAN models...'
        mkdir -p ESRGAN Waifu2x RealESRGAN
        # Add actual model download URLs here
        echo 'Models download complete (placeholder)'
    "
else
    echo "⏭️  Skipping model download - you can add models manually later"
fi

# Copy plugin to Jellyfin
echo "🔌 Setting up Jellyfin plugin..."
if [ -f "JellyfinUpscalerPlugin-v1.0.0.zip" ]; then
    echo "📦 Found plugin package, extracting to Jellyfin plugins directory..."
    cd jellyfin/plugins
    unzip -o "../../JellyfinUpscalerPlugin-v1.0.0.zip"
    cd ../..
else
    echo "⚠️  Plugin package not found. Please build the plugin first with './package.sh'"
fi

# Start services
echo "🚀 Starting services..."
docker-compose $COMPOSE_FILES up -d

echo ""
echo "✅ Setup complete!"
echo ""
echo "📍 Access Information:"
echo "   Jellyfin Web UI: http://localhost:8096"
echo "   Upscaler API:    http://localhost:8765"
echo ""
echo "📋 Next Steps:"
echo "1. Open Jellyfin at http://localhost:8096"
echo "2. Complete the initial Jellyfin setup"
echo "3. Go to Dashboard → Plugins → Jellyfin Upscaler"
echo "4. Configure the upscaler settings"
echo "5. Test with a sample video"
echo ""
echo "📊 Monitor Services:"
echo "   docker-compose logs -f jellyfin"
echo "   docker-compose logs -f jellyfin-upscaler-sidecar"
echo ""
echo "🛑 Stop Services:"
echo "   docker-compose $COMPOSE_FILES down"
echo ""

# Save compose command for future use
echo "# Use this command for future docker-compose operations:" > docker-compose.sh
echo "docker-compose $COMPOSE_FILES \$@" >> docker-compose.sh
chmod +x docker-compose.sh

echo "💡 Pro tip: Use './docker-compose.sh' for future Docker Compose commands"
