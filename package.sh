#!/bin/bash

# Jellyfin Upscaler Plugin - Package Script
# Creates a proper plugin ZIP file for Jellyfin installation

set -e

echo "📦 Jellyfin Upscaler Plugin - Package Creation"
echo "=============================================="

# Build the plugin first
echo "🔨 Building plugin..."
dotnet clean --configuration Release
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

# Create package directory
PACKAGE_VERSION="${VERSION:-1.0.0}"
PACKAGE_DIR="JellyfinUpscalerPlugin"
PACKAGE_FILE="JellyfinUpscalerPlugin-v${PACKAGE_VERSION}.zip"

echo "📁 Creating package directory for version $PACKAGE_VERSION..."
rm -rf "$PACKAGE_DIR" "$PACKAGE_FILE" 2>/dev/null || true
mkdir -p "$PACKAGE_DIR"

# Copy essential plugin files
echo "📋 Copying plugin files..."
cp bin/Release/net8.0/JellyfinUpscalerPlugin.dll "$PACKAGE_DIR/"
cp bin/Release/net8.0/JellyfinUpscalerPlugin.pdb "$PACKAGE_DIR/"
cp bin/Release/net8.0/meta.json "$PACKAGE_DIR/"

# Copy Real-ESRGAN benchmark script
cp bin/Release/net8.0/upscale.js "$PACKAGE_DIR/" 2>/dev/null || echo "⚠️  Benchmark script not found"

# Copy Web assets if they exist
if [ -d "Web/" ]; then
    cp -r Web/ "$PACKAGE_DIR/Web/"
fi

# Create the ZIP package
echo "🗜️  Creating ZIP package..."
zip -r "$PACKAGE_FILE" "$PACKAGE_DIR/"

# Calculate checksum
echo "🔍 Calculating checksum..."
if command -v sha256sum &> /dev/null; then
    CHECKSUM=$(sha256sum "$PACKAGE_FILE" | cut -d' ' -f1)
elif command -v shasum &> /dev/null; then
    CHECKSUM=$(shasum -a 256 "$PACKAGE_FILE" | cut -d' ' -f1)
else
    CHECKSUM="checksum-not-available"
fi

# Update meta.json with checksum
echo "📝 Updating checksum..."
sed -i.bak "s/\"checksum\": \"\"/\"checksum\": \"$CHECKSUM\"/" "$PACKAGE_DIR/meta.json"

# Recreate ZIP with updated checksum
zip -r "$PACKAGE_FILE" "$PACKAGE_DIR/"

# Clean up
rm -rf "$PACKAGE_DIR"

# Show results
echo ""
echo "✅ Package created successfully!"
echo "📁 File: $PACKAGE_FILE"
echo "🔍 Checksum: $CHECKSUM"
echo "📏 Size: $(du -h "$PACKAGE_FILE" | cut -f1)"
echo ""
echo "Installation Instructions:"
echo "1. Upload $PACKAGE_FILE to Jellyfin Admin → Plugins → Upload Plugin"
echo "2. Or extract to: /var/lib/jellyfin/plugins/JellyfinUpscalerPlugin/"
echo "3. Restart Jellyfin server"
echo ""
echo "🎉 Ready for installation!"
