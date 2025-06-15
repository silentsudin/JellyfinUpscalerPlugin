"""
Jellyfin Upscaler Sidecar Service
Main FastAPI application for handling upscaling requests
"""

import asyncio
import logging
import os
from contextlib import asynccontextmanager
from typing import Optional

import structlog
import uvicorn
from fastapi import FastAPI, HTTPException, BackgroundTasks, UploadFile, File
from fastapi.responses import FileResponse, JSONResponse, StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from .upscaler_engine import UpscalerEngine
from .models import UpscaleRequest, UpscaleResponse, HealthResponse, JobStatus
from .config import get_settings
from .utils import setup_logging

# Setup structured logging
setup_logging()
logger = structlog.get_logger(__name__)

# Global upscaler engine instance
upscaler_engine: Optional[UpscalerEngine] = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Manage application lifecycle"""
    global upscaler_engine

    logger.info("Starting Jellyfin Upscaler Service")
    settings = get_settings()

    # Initialize upscaler engine with error handling
    try:
        upscaler_engine = UpscalerEngine(settings)
        await upscaler_engine.initialize()

        logger.info(
            "Upscaler service ready",
            models_loaded=len(upscaler_engine.available_models),
            gpu_available=upscaler_engine.gpu_available,
        )
    except Exception as e:
        logger.error(f"Failed to initialize upscaler engine: {e}")
        # Still start the service but mark engine as unavailable
        upscaler_engine = None
        logger.warning("Service started in limited mode - upscaling features disabled")

    yield

    # Cleanup
    if upscaler_engine:
        await upscaler_engine.cleanup()
    logger.info("Upscaler service shutdown complete")


# Create FastAPI app
app = FastAPI(
    title="Jellyfin Upscaler Service",
    description="AI-powered video upscaling service for Jellyfin",
    version="1.0.0",
    lifespan=lifespan,
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint"""
    global upscaler_engine

    if not upscaler_engine:
        # Service is running but upscaler engine failed to initialize
        return HealthResponse(
            status="degraded",
            version="1.0.0",
            models_available=[],
            gpu_available=False,
            active_jobs=0,
        )

    return HealthResponse(
        status="healthy",
        version="1.0.0",
        models_available=list(upscaler_engine.available_models.keys()),
        gpu_available=upscaler_engine.gpu_available,
        active_jobs=upscaler_engine.get_active_job_count(),
    )


@app.post("/upscale", response_model=UpscaleResponse)
async def create_upscale_job(
    request: UpscaleRequest, background_tasks: BackgroundTasks
):
    """Create a new upscaling job"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    try:
        job_id = await upscaler_engine.create_job(request)

        # Start processing in background
        background_tasks.add_task(upscaler_engine.process_job, job_id)

        logger.info(
            "Upscale job created",
            job_id=job_id,
            input_file=request.input_path,
            model=request.model_name,
        )

        return UpscaleResponse(
            job_id=job_id, status="queued", message="Job created successfully"
        )

    except Exception as e:
        logger.error("Failed to create upscale job", error=str(e))
        raise HTTPException(status_code=500, detail=f"Failed to create job: {str(e)}")


@app.get("/job/{job_id}/status", response_model=JobStatus)
async def get_job_status(job_id: str):
    """Get status of an upscaling job"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    status = await upscaler_engine.get_job_status(job_id)

    if not status:
        raise HTTPException(status_code=404, detail="Job not found")

    return status


@app.get("/job/{job_id}/stream")
async def stream_job_result(job_id: str):
    """Stream the result of an upscaling job as it's being processed"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    status = await upscaler_engine.get_job_status(job_id)
    if not status:
        raise HTTPException(status_code=404, detail="Job not found")

    if status.status == "failed":
        raise HTTPException(
            status_code=400, detail=f"Job failed: {status.error_message}"
        )

    # Return streaming response
    return StreamingResponse(
        upscaler_engine.stream_upscale_generator(job_id),
        media_type="video/mp4",
        headers={
            "Content-Disposition": f"attachment; filename=upscaled_{job_id}.mp4",
            "Accept-Ranges": "bytes",
            "Transfer-Encoding": "chunked",
        },
    )


@app.post("/upscale/stream", response_model=UpscaleResponse)
async def create_streaming_upscale_job(
    request: UpscaleRequest, background_tasks: BackgroundTasks
):
    """Create a new upscaling job with immediate streaming capability"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    try:
        job_id = await upscaler_engine.create_job(request)

        # Start processing in background
        background_tasks.add_task(upscaler_engine.process_job, job_id)

        logger.info(
            "Streaming upscale job created",
            job_id=job_id,
            input_file=request.input_path,
            model=request.model_name,
        )

        return UpscaleResponse(
            job_id=job_id,
            status="queued",
            message="Streaming job created successfully",
            stream_url=f"/job/{job_id}/stream",
        )

    except Exception as e:
        logger.error("Failed to create streaming upscale job", error=str(e))
        raise HTTPException(status_code=500, detail=f"Failed to create job: {str(e)}")


@app.get("/job/{job_id}/result")
async def get_job_result(job_id: str):
    """Download the result of a completed upscaling job"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    status = await upscaler_engine.get_job_status(job_id)

    if not status:
        raise HTTPException(status_code=404, detail="Job not found")

    if status.status != "completed":
        raise HTTPException(status_code=400, detail="Job not completed")

    if not status.output_path or not os.path.exists(status.output_path):
        raise HTTPException(status_code=404, detail="Output file not found")

    return FileResponse(
        path=status.output_path,
        media_type="video/mp4",
        filename=os.path.basename(status.output_path),
    )


@app.delete("/job/{job_id}")
async def cancel_job(job_id: str):
    """Cancel an upscaling job"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    success = await upscaler_engine.cancel_job(job_id)

    if not success:
        raise HTTPException(
            status_code=404, detail="Job not found or cannot be cancelled"
        )

    return {"message": "Job cancelled successfully"}


@app.get("/models")
async def list_models():
    """List available upscaling models"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    return {
        "models": list(upscaler_engine.available_models.keys()),
        "default_model": upscaler_engine.default_model,
    }


@app.post("/benchmark")
async def run_benchmark():
    """Run a benchmark test to evaluate system performance"""
    global upscaler_engine

    if not upscaler_engine:
        raise HTTPException(status_code=503, detail="Service not ready")

    try:
        results = await upscaler_engine.run_benchmark()
        return {"benchmark_results": results}
    except Exception as e:
        logger.error("Benchmark failed", error=str(e))
        raise HTTPException(status_code=500, detail=f"Benchmark failed: {str(e)}")


if __name__ == "__main__":
    settings = get_settings()
    uvicorn.run(
        "src.main:app",
        host="0.0.0.0",
        port=8765,
        log_level=settings.log_level.lower(),
        reload=settings.debug,
    )
