#!/bin/bash

# Jellyfin Upscaler Plugin Installation Script
# This script builds and installs the plugin for Jellyfin

set -e

echo "🎬 Jellyfin Upscaler Plugin Installation"
echo "========================================"

# Check if running as root
if [[ $EUID -eq 0 ]]; then
   echo "❌ This script should not be run as root"
   exit 1
fi

# Detect system
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    JELLYFIN_PLUGIN_DIR="/var/lib/jellyfin/plugins"
    JELLYFIN_SERVICE="jellyfin"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    JELLYFIN_PLUGIN_DIR="$HOME/.local/share/jellyfin/plugins"
    JELLYFIN_SERVICE="jellyfin"
else
    echo "❌ Unsupported operating system: $OSTYPE"
    exit 1
fi

PLUGIN_NAME="JellyfinUpscalerPlugin"
PLUGIN_DIR="$JELLYFIN_PLUGIN_DIR/$PLUGIN_NAME"

echo "🔧 Detected System: $OSTYPE"
echo "📁 Plugin Directory: $PLUGIN_DIR"

# Check dependencies
echo "🔍 Checking dependencies..."

if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET 8.0 SDK not found"
    echo "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✅ .NET SDK found: $DOTNET_VERSION"

# Build the plugin
echo "🔨 Building plugin..."
if [[ -f "JellyfinUpscalerPlugin.csproj" ]]; then
    dotnet restore
    dotnet build --configuration Release
    echo "✅ Plugin built successfully"
    
    # Create proper plugin package
    echo "📦 Creating plugin package..."
    ./package.sh > /dev/null
    
    if [[ -f "JellyfinUpscalerPlugin-v1.0.0.zip" ]]; then
        echo "✅ Plugin package created successfully"
    else
        echo "❌ Failed to create plugin package"
        exit 1
    fi
else
    echo "❌ Plugin project file not found"
    echo "Please run this script from the plugin source directory"
    exit 1
fi

# Create plugin directory
echo "📁 Creating plugin directory..."
sudo mkdir -p "$PLUGIN_DIR"
sudo chown "$(whoami):$(whoami)" "$PLUGIN_DIR" 2>/dev/null || true

# Copy plugin files
echo "📦 Installing plugin files..."
unzip -q JellyfinUpscalerPlugin-v1.0.0.zip
cp -r JellyfinUpscalerPlugin/* "$PLUGIN_DIR/"
rm -rf JellyfinUpscalerPlugin

echo "✅ Plugin files installed successfully"

# Set permissions
echo "🔐 Setting permissions..."
sudo chmod -R 755 "$PLUGIN_DIR"

# Create systemd override if needed (Linux only)
if [[ "$OSTYPE" == "linux-gnu"* ]] && command -v systemctl &> /dev/null; then
    echo "🔧 Configuring Jellyfin service..."
    
    OVERRIDE_DIR="/etc/systemd/system/jellyfin.service.d"
    sudo mkdir -p "$OVERRIDE_DIR"
    
    cat << EOF | sudo tee "$OVERRIDE_DIR/upscaler-plugin.conf" > /dev/null
[Service]
# Allow access to GPU for AI processing
SupplementaryGroups=video render
# Increase memory limits for AI models
MemoryMax=4G
# Allow network access for plugin updates
RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6
EOF

    sudo systemctl daemon-reload
    echo "✅ Service configuration updated"
fi

# Restart Jellyfin
echo "🔄 Restarting Jellyfin..."
if command -v systemctl &> /dev/null; then
    sudo systemctl restart $JELLYFIN_SERVICE
    sleep 3
    if systemctl is-active --quiet $JELLYFIN_SERVICE; then
        echo "✅ Jellyfin restarted successfully"
    else
        echo "❌ Failed to restart Jellyfin"
        echo "Please restart manually: sudo systemctl restart $JELLYFIN_SERVICE"
    fi
else
    echo "⚠️  Please restart Jellyfin manually"
fi

# Installation complete
echo ""
echo "🎉 Installation Complete!"
echo "=========================="
echo ""
echo "Plugin installed to: $PLUGIN_DIR"
echo ""
echo "Next Steps:"
echo "1. Open Jellyfin Admin Dashboard"
echo "2. Navigate to Plugins → Jellyfin Upscaler"
echo "3. Run the benchmark test"
echo "4. Configure your preferred settings"
echo "5. Enjoy enhanced video quality!"
echo ""
echo "📚 Documentation: https://github.com/Kuschel-code/JellyfinUpscalerPlugin"
echo "🐛 Issues: https://github.com/Kuschel-code/JellyfinUpscalerPlugin/issues"
echo ""
echo "Happy streaming! 🍿"
