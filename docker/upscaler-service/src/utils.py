"""
Utility functions for the upscaler service
"""

import logging
import os
import sys
from typing import Optional

import structlog


def setup_logging(log_level: str = "INFO") -> None:
    """Setup structured logging"""

    # Configure structlog
    structlog.configure(
        processors=[
            structlog.stdlib.filter_by_level,
            structlog.stdlib.add_logger_name,
            structlog.stdlib.add_log_level,
            structlog.stdlib.PositionalArgumentsFormatter(),
            structlog.processors.TimeStamper(fmt="iso"),
            structlog.processors.StackInfoRenderer(),
            structlog.processors.format_exc_info,
            structlog.processors.UnicodeDecoder(),
            (
                structlog.processors.JSONRenderer()
                if os.getenv("UPSCALER_JSON_LOGS")
                else structlog.dev.ConsoleRenderer()
            ),
        ],
        context_class=dict,
        logger_factory=structlog.stdlib.LoggerFactory(),
        wrapper_class=structlog.stdlib.BoundLogger,
        cache_logger_on_first_use=True,
    )

    # Configure standard logging
    logging.basicConfig(
        format="%(message)s",
        stream=sys.stdout,
        level=getattr(logging, log_level.upper()),
    )


def ensure_directory(path: str) -> None:
    """Ensure directory exists"""
    os.makedirs(path, exist_ok=True)


def get_file_size(path: str) -> Optional[int]:
    """Get file size in bytes"""
    try:
        return os.path.getsize(path)
    except OSError:
        return None


def cleanup_file(path: str) -> bool:
    """Safely remove a file"""
    try:
        if os.path.exists(path):
            os.remove(path)
            return True
    except OSError:
        pass
    return False


def validate_video_file(path: str) -> bool:
    """Validate if file is a video file"""
    if not os.path.exists(path):
        return False

    video_extensions = {".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".wmv", ".m4v"}
    _, ext = os.path.splitext(path.lower())
    return ext in video_extensions


def generate_output_filename(
    input_path: str, model_name: str, scale_factor: int
) -> str:
    """Generate output filename based on input and parameters"""
    base_name = os.path.splitext(os.path.basename(input_path))[0]
    return f"{base_name}_upscaled_{model_name}_{scale_factor}x.mp4"
