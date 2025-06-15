"""
Data models for the upscaler service
"""

from datetime import datetime
from typing import Optional, Dict, Any, List
from enum import Enum

from pydantic import BaseModel, Field


class JobStatusEnum(str, Enum):
    QUEUED = "queued"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class UpscaleProfile(str, Enum):
    GENERAL = "General"
    ANIME = "Anime"
    FACE = "Face"
    COMPACT = "Compact"


class UpscaleRequest(BaseModel):
    """Request model for Real-ESRGAN upscaling jobs"""

    input_path: str = Field(..., description="Path to input video file")
    output_path: Optional[str] = Field(
        None, description="Path for output file (auto-generated if not provided)"
    )
    profile: str = Field(
        "General", description="Real-ESRGAN profile: General, Anime, Face, or Compact"
    )
    scale_factor: Optional[int] = Field(
        None,
        ge=1,
        le=4,
        description="Override upscaling factor (determined by profile if not set)",
    )

    # Processing options
    fps_limit: Optional[int] = Field(None, description="Limit FPS for processing")
    resolution_limit: Optional[str] = Field(
        None, description="Resolution limit (e.g., '1080p', '4K')"
    )
    use_gpu: bool = Field(True, description="Use GPU acceleration if available")
    batch_size: int = Field(4, ge=1, le=16, description="Processing batch size")

    # Metadata
    priority: int = Field(
        5, ge=1, le=10, description="Job priority (1=lowest, 10=highest)"
    )
    callback_url: Optional[str] = Field(
        None, description="URL to notify when job completes"
    )


class UpscaleResponse(BaseModel):
    """Response model for upscaling job creation"""

    job_id: str = Field(..., description="Unique job identifier")
    status: JobStatusEnum = Field(..., description="Current job status")
    message: str = Field(..., description="Response message")
    estimated_duration: Optional[int] = Field(
        None, description="Estimated processing time in seconds"
    )
    stream_url: Optional[str] = Field(
        None, description="URL for streaming the upscaled video in real-time"
    )


class JobStatus(BaseModel):
    """Model for job status information"""

    job_id: str = Field(..., description="Unique job identifier")
    status: JobStatusEnum = Field(..., description="Current job status")
    progress: float = Field(
        0.0, ge=0.0, le=100.0, description="Processing progress percentage"
    )

    # Timestamps
    created_at: float = Field(..., description="Job creation time (unix timestamp)")
    started_at: Optional[float] = Field(
        None, description="Processing start time (unix timestamp)"
    )
    completed_at: Optional[float] = Field(
        None, description="Processing completion time (unix timestamp)"
    )

    # File paths
    input_path: str = Field(..., description="Input video file path")
    output_path: Optional[str] = Field(None, description="Output video file path")

    # Processing info
    model_name: Optional[str] = Field(None, description="AI model being/was used")
    scale_factor: Optional[int] = Field(None, description="Upscaling factor")

    # Results
    error_message: Optional[str] = Field(None, description="Error message if failed")
    processing_time: Optional[float] = Field(
        None, description="Total processing time in seconds"
    )
    input_size_mb: Optional[float] = Field(None, description="Input file size in MB")
    output_size_mb: Optional[float] = Field(None, description="Output file size in MB")

    # Performance metrics
    fps_processed: Optional[float] = Field(
        None, description="Average FPS during processing"
    )
    gpu_utilization: Optional[float] = Field(
        None, description="Average GPU utilization percentage"
    )
    memory_usage_mb: Optional[float] = Field(
        None, description="Peak memory usage in MB"
    )


class HealthResponse(BaseModel):
    """Health check response model"""

    status: str = Field(..., description="Service health status")
    version: str = Field(..., description="Service version")
    models_available: List[str] = Field(..., description="Available AI models")
    gpu_available: bool = Field(..., description="GPU acceleration available")
    active_jobs: int = Field(..., description="Number of active jobs")
    system_info: Optional[Dict[str, Any]] = Field(
        None, description="System information"
    )


class BenchmarkResult(BaseModel):
    """Benchmark test result"""

    model_name: str = Field(..., description="AI model tested")
    test_duration: float = Field(..., description="Test duration in seconds")
    fps_achieved: float = Field(..., description="Frames per second achieved")
    gpu_utilization: float = Field(..., description="Average GPU utilization")
    memory_usage_mb: float = Field(..., description="Peak memory usage in MB")
    recommended: bool = Field(
        ..., description="Whether this model is recommended for this system"
    )


class SystemCapabilities(BaseModel):
    """System capabilities and recommendations"""

    gpu_available: bool = Field(..., description="GPU acceleration available")
    gpu_name: Optional[str] = Field(None, description="GPU model name")
    vram_mb: Optional[int] = Field(None, description="Available VRAM in MB")
    cpu_cores: int = Field(..., description="Number of CPU cores")
    ram_mb: int = Field(..., description="Available RAM in MB")

    recommended_models: List[str] = Field(
        ..., description="Recommended AI models for this system"
    )
    max_resolution: str = Field(..., description="Maximum recommended resolution")
    concurrent_jobs: int = Field(
        ..., description="Recommended number of concurrent jobs"
    )
