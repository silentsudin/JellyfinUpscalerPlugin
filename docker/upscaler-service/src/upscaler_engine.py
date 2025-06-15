"""
Core upscaler engine for processing video files with AI models
"""

import asyncio
import json
import os
import tempfile
import time
import uuid
from pathlib import Path
from typing import Dict, List, Optional, AsyncGenerator
import logging

import cv2
import numpy as np
import torch

# Try to import Intel Extension for PyTorch
try:
    import intel_extension_for_pytorch as ipex

    IPEX_AVAILABLE = True
except ImportError:
    IPEX_AVAILABLE = False

# Import AI libraries with error handling
try:
    from basicsr.archs.rrdbnet_arch import RRDBNet
    from realesrgan import RealESRGANer
    from realesrgan.archs.srvgg_arch import SRVGGNetCompact

    BASICSR_AVAILABLE = True
except ImportError as e:
    logging.warning(f"BasicSR/RealESRGAN import failed: {e}")
    BASICSR_AVAILABLE = False

    # Define dummy classes to prevent crashes
    class RRDBNet:
        def __init__(self, *args, **kwargs):
            raise RuntimeError("BasicSR not available")

    class RealESRGANer:
        def __init__(self, *args, **kwargs):
            raise RuntimeError("RealESRGAN not available")

    class SRVGGNetCompact:
        def __init__(self, *args, **kwargs):
            raise RuntimeError("SRVGGNetCompact not available")


import ffmpeg

from .models import UpscaleRequest, JobStatus
from .config import get_settings
from .utils import (
    ensure_directory,
    validate_video_file,
    generate_output_filename,
    cleanup_file,
)

logger = logging.getLogger(__name__)


class UpscalerEngine:
    """Main upscaler engine for processing video files"""

    def __init__(self, settings):
        self.settings = settings
        self.available_models: Dict[str, dict] = {}
        self.loaded_upscalers: Dict[str, RealESRGANer] = {}
        self.active_jobs: Dict[str, JobStatus] = {}

        # Detect available GPU acceleration
        self.cuda_available = torch.cuda.is_available()
        self.intel_xpu_available = IPEX_AVAILABLE and self._check_intel_xpu()
        self.gpu_available = self.cuda_available or self.intel_xpu_available

        # Determine device type
        if self.intel_xpu_available:
            self.device_type = "xpu"
            self.device = torch.device("xpu")
        elif self.cuda_available:
            self.device_type = "cuda"
            self.device = torch.device("cuda")
        else:
            self.device_type = "cpu"
            self.device = torch.device("cpu")

        self.default_model = "RealESRGAN_x4plus"

        # Create necessary directories
        ensure_directory(self.settings.temp_path)
        ensure_directory(self.settings.output_path)

    def _check_intel_xpu(self) -> bool:
        """Check if Intel XPU is available"""
        try:
            if IPEX_AVAILABLE:
                import intel_extension_for_pytorch as ipex

                return torch.xpu.is_available()
        except Exception:
            pass
        return False

    async def initialize(self):
        """Initialize the upscaler engine and load models"""
        logger.info("Initializing upscaler engine...")

        # Check BasicSR availability
        if not BASICSR_AVAILABLE:
            logger.error(
                "BasicSR/RealESRGAN libraries not available. AI upscaling will not work."
            )
            return

        # Check GPU availability
        if self.intel_xpu_available:
            logger.info(f"Intel XPU available: {self.device}")
            if IPEX_AVAILABLE:
                import intel_extension_for_pytorch as ipex

                logger.info(f"Intel Extension for PyTorch version: {ipex.__version__}")
        elif self.cuda_available:
            logger.info(f"CUDA available: {torch.cuda.get_device_name(0)}")
        else:
            logger.warning("No GPU acceleration available, using CPU (will be slow)")

        # Load available models
        await self._load_available_models()

        # Pre-load default model
        if self.default_model in self.available_models:
            await self._load_model(self.default_model)

        logger.info(
            f"Upscaler engine initialized with {len(self.available_models)} models on {self.device_type}"
        )

    async def _load_available_models(self):
        """Load information about available models"""
        models_path = Path(self.settings.model_path)

        # Define Real-ESRGAN models only
        builtin_models = {
            "RealESRGAN_x4plus": {
                "name": "General x4+",
                "scale": 4,
                "description": "Best for live action content - photos, movies, TV shows",
                "model_path": "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth",
                "netscale": 4,
                "model_type": "RRDBNet",
                "profile": "General",
            },
            "RealESRGAN_x2plus": {
                "name": "Compact x2+",
                "scale": 2,
                "description": "Faster processing with 2x upscaling",
                "model_path": "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x2plus.pth",
                "netscale": 2,
                "model_type": "RRDBNet",
                "profile": "Compact",
            },
            "RealESRGANv2-animevideo-xsx4": {
                "name": "Anime x4",
                "scale": 4,
                "description": "Optimized for anime, cartoons, and drawn content",
                "model_path": "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.2.4/RealESRGANv2-animevideo-xsx4.pth",
                "netscale": 4,
                "model_type": "RRDBNet",
                "profile": "Anime",
            },
            "GFPGANv1.4": {
                "name": "Face Enhancement",
                "scale": 4,
                "description": "Specialized for faces and portraits",
                "model_path": "https://github.com/TencentARC/GFPGAN/releases/download/v1.3.0/GFPGANv1.4.pth",
                "netscale": 4,
                "model_type": "RRDBNet",
                "profile": "Face",
            },
        }

        # Load models from filesystem if available
        if models_path.exists():
            for model_dir in models_path.iterdir():
                if model_dir.is_dir():
                    model_json = model_dir / "model.json"
                    if model_json.exists():
                        try:
                            with open(model_json) as f:
                                model_info = json.load(f)
                                self.available_models[model_dir.name] = model_info
                        except Exception as e:
                            logger.warning(
                                f"Failed to load model {model_dir.name}: {e}"
                            )

        # Add builtin models
        self.available_models.update(builtin_models)

    def get_model_by_profile(self, profile: str) -> str:
        """Get the appropriate model name for a given profile"""
        profile_map = {
            "General": "RealESRGAN_x4plus",
            "Anime": "RealESRGANv2-animevideo-xsx4",
            "Face": "GFPGANv1.4",
            "Compact": "RealESRGAN_x2plus",
        }
        return profile_map.get(profile, "RealESRGAN_x4plus")

    async def _load_model(self, model_name: str) -> RealESRGANer:
        """Load a specific upscaling model"""
        if model_name in self.loaded_upscalers:
            return self.loaded_upscalers[model_name]

        if model_name not in self.available_models:
            raise ValueError(f"Model {model_name} not available")

        model_info = self.available_models[model_name]

        try:
            # Use the determined device type
            device = self.device

            # Create model architecture
            if model_info.get("model_type") == "RRDBNet":
                model = RRDBNet(
                    num_in_ch=3,
                    num_out_ch=3,
                    num_feat=64,
                    num_block=23,
                    num_grow_ch=32,
                    scale=model_info["netscale"],
                )
            else:
                # Default to RRDBNet
                model = RRDBNet(
                    num_in_ch=3,
                    num_out_ch=3,
                    num_feat=64,
                    num_block=23,
                    num_grow_ch=32,
                    scale=model_info["netscale"],
                )

            # Create upscaler with device-appropriate settings
            tile_size = 400 if self.gpu_available else 200
            use_half_precision = (
                self.gpu_available and self.device_type != "xpu"
            )  # Intel XPU may not support half precision

            upscaler = RealESRGANer(
                scale=model_info["netscale"],
                model_path=model_info["model_path"],
                model=model,
                tile=tile_size,
                tile_pad=10,
                pre_pad=0,
                half=use_half_precision,
                device=device,
            )

            self.loaded_upscalers[model_name] = upscaler
            logger.info(f"Loaded model: {model_name}")
            return upscaler

        except Exception as e:
            logger.error(f"Failed to load model {model_name}: {e}")
            raise

    async def create_job(self, request: UpscaleRequest) -> str:
        """Create a new Real-ESRGAN upscaling job"""
        job_id = str(uuid.uuid4())

        # Validate input
        if not validate_video_file(request.input_path):
            raise ValueError("Invalid input video file")

        # Get model name from profile
        model_name = self.get_model_by_profile(request.profile)
        if model_name not in self.available_models:
            raise ValueError(f"Model for profile {request.profile} not available")

        model_info = self.available_models[model_name]
        scale_factor = request.scale_factor or model_info["scale"]

        # Generate output path
        output_filename = generate_output_filename(
            request.input_path,
            model_name,
            scale_factor,
        )
        output_path = os.path.join(self.settings.output_path, output_filename)

        # Create job status
        status = JobStatus(
            job_id=job_id,
            status="queued",
            input_path=request.input_path,
            output_path=output_path,
            model_name=model_name,
            scale_factor=scale_factor,
            created_at=time.time(),
            progress=0.0,
        )

        self.active_jobs[job_id] = status
        return job_id

    async def process_job(self, job_id: str):
        """Process an upscaling job"""
        if job_id not in self.active_jobs:
            return

        job = self.active_jobs[job_id]

        try:
            job.status = "processing"
            job.started_at = time.time()

            # Load model if needed
            upscaler = await self._load_model(job.model_name)

            # Process video with streaming
            await self._process_video_streaming(job, upscaler)

            job.status = "completed"
            job.completed_at = time.time()
            job.progress = 100.0

        except Exception as e:
            logger.error(f"Job {job_id} failed: {e}")
            job.status = "failed"
            job.error_message = str(e)
            job.completed_at = time.time()

    async def _process_video_streaming(self, job: JobStatus, upscaler: RealESRGANer):
        """Process video with frame-by-frame streaming output"""

        # Open input video
        cap = cv2.VideoCapture(job.input_path)
        if not cap.isOpened():
            raise ValueError("Could not open input video")

        try:
            # Get video properties
            fps = cap.get(cv2.CAP_PROP_FPS)
            total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
            width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
            height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

            # Calculate output dimensions
            scale = self.available_models[job.model_name]["scale"]
            out_width = width * scale
            out_height = height * scale

            # Setup FFmpeg for streaming output
            output_process = (
                ffmpeg.input(
                    "pipe:",
                    format="rawvideo",
                    pix_fmt="bgr24",
                    s=f"{out_width}x{out_height}",
                    r=fps,
                )
                .output(job.output_path, pix_fmt="yuv420p", vcodec="libx264", crf=18)
                .overwrite_output()
                .run_async(pipe_stdin=True)
            )

            processed_frames = 0

            # Process frames one by one
            while True:
                ret, frame = cap.read()
                if not ret:
                    break

                # Upscale frame
                upscaled_frame, _ = upscaler.enhance(frame, outscale=scale)

                # Write frame to output stream
                output_process.stdin.write(upscaled_frame.tobytes())

                processed_frames += 1
                job.progress = (processed_frames / total_frames) * 100

                # Allow other tasks to run
                if processed_frames % 10 == 0:
                    await asyncio.sleep(0.01)

            # Close streams
            output_process.stdin.close()
            output_process.wait()

        finally:
            cap.release()

    async def get_job_status(self, job_id: str) -> Optional[JobStatus]:
        """Get status of a job"""
        return self.active_jobs.get(job_id)

    async def cancel_job(self, job_id: str) -> bool:
        """Cancel a job"""
        if job_id not in self.active_jobs:
            return False

        job = self.active_jobs[job_id]
        if job.status in ["completed", "failed"]:
            return False

        job.status = "cancelled"
        job.completed_at = time.time()
        return True

    def get_active_job_count(self) -> int:
        """Get number of active jobs"""
        return len(
            [
                job
                for job in self.active_jobs.values()
                if job.status in ["queued", "processing"]
            ]
        )

    async def cleanup(self):
        """Cleanup resources"""
        # Cancel active jobs
        for job_id in list(self.active_jobs.keys()):
            await self.cancel_job(job_id)

        # Clear loaded models
        self.loaded_upscalers.clear()

        logger.info("Upscaler engine cleanup complete")

    async def run_benchmark(self) -> dict:
        """Run a benchmark test"""
        # Create a test image
        test_image = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)

        results = {}

        for model_name in self.available_models.keys():
            try:
                start_time = time.time()
                upscaler = await self._load_model(model_name)

                # Test upscaling
                upscaled, _ = upscaler.enhance(test_image)

                end_time = time.time()
                processing_time = end_time - start_time

                results[model_name] = {
                    "processing_time": processing_time,
                    "input_size": test_image.shape,
                    "output_size": upscaled.shape,
                    "fps_estimate": 1.0 / processing_time if processing_time > 0 else 0,
                }

            except Exception as e:
                results[model_name] = {"error": str(e)}

        return results

    async def stream_upscale_generator(
        self, job_id: str
    ) -> AsyncGenerator[bytes, None]:
        """Generator for streaming upscaled video data as it's processed"""
        if job_id not in self.active_jobs:
            return

        job = self.active_jobs[job_id]

        # Wait for job to start processing
        while job.status == "queued":
            await asyncio.sleep(0.1)

        if job.status == "failed":
            return

        # Monitor output file and stream new data
        last_position = 0

        while job.status == "processing":
            if os.path.exists(job.output_path):
                current_size = os.path.getsize(job.output_path)

                if current_size > last_position:
                    # Read new data
                    with open(job.output_path, "rb") as f:
                        f.seek(last_position)
                        chunk = f.read(current_size - last_position)
                        if chunk:
                            yield chunk
                            last_position = current_size

            await asyncio.sleep(0.1)

        # Stream any remaining data
        if job.status == "completed" and os.path.exists(job.output_path):
            current_size = os.path.getsize(job.output_path)
            if current_size > last_position:
                with open(job.output_path, "rb") as f:
                    f.seek(last_position)
                    chunk = f.read()
                    if chunk:
                        yield chunk
