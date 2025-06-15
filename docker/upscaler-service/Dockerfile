# Jellyfin Upscaler Sidecar Service - NVIDIA CUDA
# Using Ubuntu 22.04 for better package compatibility
FROM nvidia/cuda:12.2.2-devel-ubuntu22.04

# Prevent interactive prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Install system dependencies including Python 3.11
RUN apt-get update && apt-get install -y \
    software-properties-common \
    ca-certificates \
    gnupg \
    lsb-release \
    && add-apt-repository ppa:deadsnakes/ppa \
    && apt-get update && apt-get install -y \
    python3.11 \
    python3.11-dev \
    python3.11-distutils \
    python3-pip \
    build-essential \
    cmake \
    git \
    wget \
    curl \
    ffmpeg \
    libgl1-mesa-glx \
    libgl1-mesa-dev \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgomp1 \
    libegl1-mesa \
    libgles2-mesa \
    && rm -rf /var/lib/apt/lists/*

# Set Python 3.11 as default
RUN update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.11 1
RUN update-alternatives --install /usr/bin/python python /usr/bin/python3.11 1

# Upgrade pip
RUN python3 -m pip install --upgrade pip

# Set working directory
WORKDIR /app

# Install PyTorch with CUDA support first (CUDA 12.1 for compatibility)
RUN python3 -m pip install torch==2.1.0 torchvision==0.16.0 torchaudio==2.1.0 --index-url https://download.pytorch.org/whl/cu121

# Copy requirements and install remaining Python dependencies
COPY requirements.txt .
RUN python3 -m pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY src/ ./src/

# Copy config and models directories (now they exist)
COPY config/ ./config/
COPY models/ ./models/

# Create additional necessary directories
RUN mkdir -p /app/temp /app/output /app/logs

# Expose port for API
EXPOSE 8765

# Set environment variables
ENV PYTHONPATH=/app
ENV UPSCALER_CONFIG_PATH=/app/config
ENV UPSCALER_MODEL_PATH=/app/models
ENV UPSCALER_TEMP_PATH=/app/temp
ENV UPSCALER_LOG_LEVEL=INFO
ENV NVIDIA_VISIBLE_DEVICES=all
ENV NVIDIA_DRIVER_CAPABILITIES=compute,utility

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8765/health || exit 1

# Run the upscaler service
CMD ["python3", "-m", "src.main"]
