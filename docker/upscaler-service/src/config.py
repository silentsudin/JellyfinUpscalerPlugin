"""
Configuration settings for the upscaler service
"""

import os
from typing import Optional, List
from functools import lru_cache

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings"""

    # Server settings
    debug: bool = False
    log_level: str = "INFO"
    host: str = "0.0.0.0"
    port: int = 8765

    # Paths
    model_path: str = "/app/models"
    temp_path: str = "/app/temp"
    output_path: str = "/app/output"
    log_path: str = "/app/logs"

    # Processing settings
    max_concurrent_jobs: int = 2
    job_timeout_minutes: int = 60
    cleanup_temp_files: bool = True
    cleanup_after_hours: int = 24

    # GPU settings
    use_gpu: bool = True
    gpu_memory_limit_mb: Optional[int] = None
    cuda_visible_devices: Optional[str] = None

    # Model settings
    default_model: str = "RealESRGAN_x4plus"
    enable_model_download: bool = True
    model_cache_size_gb: int = 10

    # Quality presets
    quality_presets: dict = {
        "fast": {
            "model": "RealESRGAN_x2plus",
            "scale_factor": 2,
            "batch_size": 8,
            "denoising": 0,
        },
        "balanced": {
            "model": "RealESRGAN_x4plus",
            "scale_factor": 2,
            "batch_size": 4,
            "denoising": 1,
        },
        "quality": {
            "model": "RealESRGAN_x4plus",
            "scale_factor": 4,
            "batch_size": 2,
            "denoising": 2,
        },
        "anime": {
            "model": "RealESRGANv2-animevideo-xsx4",
            "scale_factor": 4,
            "batch_size": 4,
            "denoising": 1,
        },
    }

    # Security
    api_key: Optional[str] = None
    allowed_origins: List[str] = ["*"]
    max_file_size_mb: int = 1000

    # Monitoring
    enable_metrics: bool = True
    metrics_port: int = 9090
    health_check_interval: int = 30

    # Redis settings (for job queue)
    redis_url: Optional[str] = None
    redis_password: Optional[str] = None

    class Config:
        env_prefix = "UPSCALER_"
        case_sensitive = False


@lru_cache()
def get_settings() -> Settings:
    """Get cached settings instance"""
    return Settings()
