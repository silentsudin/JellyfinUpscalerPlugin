#!/bin/bash

# Docker build test script for Jellyfin Upscaler Plugin
set -e

echo "🐳 Testing Docker builds for Jellyfin Upscaler Service"
echo "======================================================"

cd "$(dirname "$0")/docker/upscaler-service"

# Test the main Dockerfile (Ubuntu 22.04)
echo ""
echo "📦 Building main Dockerfile (Ubuntu 22.04 + CUDA 12.2)..."
if docker build -t jellyfin-upscaler-service:ubuntu22 -f Dockerfile .; then
    echo "✅ Ubuntu 22.04 build successful!"
    UBUNTU22_SUCCESS=true
else
    echo "❌ Ubuntu 22.04 build failed"
    UBUNTU22_SUCCESS=false
fi

# Test the alternative Dockerfile (Ubuntu 22.04 explicit)
echo ""
echo "📦 Building alternative Dockerfile (Ubuntu 22.04 explicit)..."
if docker build -t jellyfin-upscaler-service:ubuntu22-alt -f Dockerfile.ubuntu22 .; then
    echo "✅ Ubuntu 22.04 alternative build successful!"
    UBUNTU22_ALT_SUCCESS=true
else
    echo "❌ Ubuntu 22.04 alternative build failed"
    UBUNTU22_ALT_SUCCESS=false
fi

echo ""
echo "📊 Build Results Summary:"
echo "========================"
if [ "$UBUNTU22_SUCCESS" = true ]; then
    echo "✅ Main Dockerfile (Ubuntu 22.04): SUCCESS"
    RECOMMENDED_IMAGE="jellyfin-upscaler-service:ubuntu22"
elif [ "$UBUNTU22_ALT_SUCCESS" = true ]; then
    echo "✅ Alternative Dockerfile (Ubuntu 22.04): SUCCESS"
    RECOMMENDED_IMAGE="jellyfin-upscaler-service:ubuntu22-alt"
else
    echo "❌ All builds failed"
    exit 1
fi

echo ""
echo "🚀 Testing container startup..."
# Test the successful build
CONTAINER_ID=$(docker run -d --rm -p 8765:8765 --name jellyfin-upscaler-test $RECOMMENDED_IMAGE)

echo "⏳ Waiting for service to start..."
sleep 15

# Test health endpoint
echo "🔍 Testing health endpoint..."
if curl -f http://localhost:8765/health > /dev/null 2>&1; then
    echo "✅ Service health check passed!"
    echo "🎯 Container is ready for production use"
else
    echo "⚠️  Health check failed, checking container logs..."
    docker logs $CONTAINER_ID
fi

# Cleanup
echo "🧹 Cleaning up test container..."
docker stop $CONTAINER_ID || true

echo ""
echo "🎉 Docker build testing completed!"
echo "Recommended image: $RECOMMENDED_IMAGE"
echo ""
echo "📋 Next steps:"
echo "1. Use the successful image in your docker-compose.yml"
echo "2. Deploy with proper GPU runtime support"
echo "3. Test with actual video files"
