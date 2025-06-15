/**
 * Jellyfin Upscaler Plugin - Client-side real-time video enhancement
 */

class JellyfinUpscaler {
    constructor() {
        this.initialized = false;
        this.config = null;
        this.models = new Map();
        this.canvas = null;
        this.ctx = null;
        this.benchmarkResult = null;

        this.init();
    }

    async init() {
        try {
            // Load configuration from plugin
            this.config = await this.loadConfiguration();

            // Initialize TensorFlow.js
            await tf.ready();

            // Run benchmark if enabled
            if (this.config.EnableBenchmark && !this.getBenchmarkResult()) {
                await this.runBenchmark();
            }

            // Setup video player integration
            this.setupVideoPlayerIntegration();

            this.initialized = true;
            console.log('Jellyfin Upscaler Plugin initialized successfully');
        } catch (error) {
            console.error('Failed to initialize Jellyfin Upscaler Plugin:', error);
        }
    }

    async loadConfiguration() {
        try {
            // This would integrate with Jellyfin's API to get plugin configuration
            const response = await fetch('/api/Configuration/jellyfin-upscaler');
            return await response.json();
        } catch (error) {
            console.warn('Could not load plugin configuration, using defaults');
            return {
                SelectedProfile: 'Default',
                EnableBenchmark: true,
                CustomSettings: {
                    EnableFPSRule: false,
                    MaxFPSForAI: 'Unlimited',
                    MinResolutionForAI: '1080p',
                    MaxResolutionForAI: '4320p',
                    DefaultShaderBelowMinResolution: 'Bicubic',
                    DefaultShaderAboveMaxResolution: 'Lanczos',
                    Sharpness: 2,
                    Saturation: 1,
                    Contrast: 1.0,
                    Denoising: 1
                }
            };
        }
    }

    async runBenchmark() {
        console.log('Running device benchmark for AI upscaling...');

        const testSizes = [
            { width: 640, height: 360 },
            { width: 1280, height: 720 },
            { width: 1920, height: 1080 }
        ];

        let totalTime = 0;
        let testCount = 0;

        try {
            // Create test canvas
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');

            for (const size of testSizes) {
                canvas.width = size.width;
                canvas.height = size.height;

                // Create test image data
                const imageData = ctx.createImageData(size.width, size.height);
                for (let i = 0; i < imageData.data.length; i += 4) {
                    imageData.data[i] = Math.random() * 255;     // R
                    imageData.data[i + 1] = Math.random() * 255; // G
                    imageData.data[i + 2] = Math.random() * 255; // B
                    imageData.data[i + 3] = 255;                 // A
                }
                ctx.putImageData(imageData, 0, 0);

                // Test processing time
                const startTime = performance.now();

                // Create tensor from canvas
                const tensor = tf.browser.fromPixels(canvas);

                // Simulate simple processing (resize operation)
                const processed = tf.image.resizeBilinear(tensor, [size.height * 2, size.width * 2]);

                // Convert back to pixels (forces computation)
                await tf.browser.toPixels(processed, canvas);

                const endTime = performance.now();
                const processingTime = endTime - startTime;

                totalTime += processingTime;
                testCount++;

                console.log(`Benchmark ${size.width}x${size.height}: ${processingTime.toFixed(2)}ms`);

                // Cleanup tensors
                tensor.dispose();
                processed.dispose();
            }

            const averageTime = totalTime / testCount;
            const isCapable = averageTime < 100; // Less than 100ms average is considered good

            this.saveBenchmarkResult(isCapable);

            console.log(`Benchmark completed. Average processing time: ${averageTime.toFixed(2)}ms`);
            console.log(`Device is ${isCapable ? 'capable' : 'not capable'} of real-time AI upscaling`);

            return isCapable;
        } catch (error) {
            console.error('Benchmark failed:', error);
            this.saveBenchmarkResult(false);
            return false;
        }
    }

    saveBenchmarkResult(isCapable) {
        localStorage.setItem('jellyfinUpscalerBenchmark', JSON.stringify({
            capable: isCapable,
            timestamp: Date.now()
        }));
        this.benchmarkResult = isCapable;
    }

    getBenchmarkResult() {
        try {
            const stored = localStorage.getItem('jellyfinUpscalerBenchmark');
            if (stored) {
                const result = JSON.parse(stored);
                // Check if benchmark is recent (within 7 days)
                if (Date.now() - result.timestamp < 7 * 24 * 60 * 60 * 1000) {
                    this.benchmarkResult = result.capable;
                    return result.capable;
                }
            }
        } catch (error) {
            console.warn('Could not retrieve benchmark result:', error);
        }
        return null;
    }

    setupVideoPlayerIntegration() {
        // Integration with Jellyfin's video player
        this.observeVideoElements();

        // Setup mutation observer to catch dynamically added video elements
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        if (node.tagName === 'VIDEO') {
                            this.enhanceVideo(node);
                        } else {
                            const videos = node.querySelectorAll?.('video');
                            if (videos) {
                                videos.forEach(video => this.enhanceVideo(video));
                            }
                        }
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    observeVideoElements() {
        const videos = document.querySelectorAll('video');
        videos.forEach(video => this.enhanceVideo(video));
    }

    async enhanceVideo(videoElement) {
        if (videoElement.dataset.upscalerEnhanced) {
            return; // Already enhanced
        }

        console.log('Enhancing video element with upscaling');
        videoElement.dataset.upscalerEnhanced = 'true';

        // Check if enhancement should be applied
        if (!this.shouldEnhanceVideo(videoElement)) {
            return;
        }

        try {
            // Create enhancement canvas
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');

            // Style canvas to match video
            canvas.style.position = 'absolute';
            canvas.style.top = '0';
            canvas.style.left = '0';
            canvas.style.width = '100%';
            canvas.style.height = '100%';
            canvas.style.pointerEvents = 'none';
            canvas.style.zIndex = '1';

            // Insert canvas after video
            videoElement.parentNode.insertBefore(canvas, videoElement.nextSibling);

            // Start real-time processing
            this.startRealTimeProcessing(videoElement, canvas);

        } catch (error) {
            console.error('Failed to enhance video:', error);
        }
    }

    shouldEnhanceVideo(videoElement) {
        // Check benchmark result
        if (this.benchmarkResult === false) {
            console.log('Skipping video enhancement - device not capable');
            return false;
        }

        // Check video resolution
        const width = videoElement.videoWidth;
        const height = videoElement.videoHeight;

        if (!width || !height) {
            return false; // Video not loaded yet
        }

        // Apply configuration rules
        if (this.config.SelectedProfile === 'Custom') {
            const settings = this.config.CustomSettings;

            // Check resolution limits
            const minHeight = this.resolutionToHeight(settings.MinResolutionForAI);
            const maxHeight = this.resolutionToHeight(settings.MaxResolutionForAI);

            if (height < minHeight || height > maxHeight) {
                console.log(`Video resolution ${height}p outside enhancement range (${minHeight}p-${maxHeight}p)`);
                return false;
            }
        }

        return true;
    }

    startRealTimeProcessing(videoElement, canvas) {
        let lastFrameTime = 0;
        const targetFPS = 30; // Process at 30 FPS max
        const frameInterval = 1000 / targetFPS;

        const processFrame = (currentTime) => {
            if (currentTime - lastFrameTime >= frameInterval) {
                this.processVideoFrame(videoElement, canvas);
                lastFrameTime = currentTime;
            }

            if (!videoElement.paused && !videoElement.ended) {
                requestAnimationFrame(processFrame);
            }
        };

        // Start processing when video plays
        videoElement.addEventListener('play', () => {
            requestAnimationFrame(processFrame);
        });

        // Initial processing if video is already playing
        if (!videoElement.paused && !videoElement.ended) {
            requestAnimationFrame(processFrame);
        }
    }

    processVideoFrame(videoElement, canvas) {
        try {
            const ctx = canvas.getContext('2d');

            // Match canvas size to video
            if (canvas.width !== videoElement.videoWidth || canvas.height !== videoElement.videoHeight) {
                canvas.width = videoElement.videoWidth;
                canvas.height = videoElement.videoHeight;
            }

            // Draw current video frame
            ctx.drawImage(videoElement, 0, 0);

            // Apply enhancement based on selected profile
            this.applyEnhancement(ctx, canvas);

        } catch (error) {
            console.error('Error processing video frame:', error);
        }
    }

    applyEnhancement(ctx, canvas) {
        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);

        // Apply enhancement based on configuration
        switch (this.config.SelectedProfile.toLowerCase()) {
            case 'anime':
                this.applyAnimeEnhancement(imageData);
                break;
            case 'movies':
                this.applyMovieEnhancement(imageData);
                break;
            case 'tv shows':
                this.applyTVShowEnhancement(imageData);
                break;
            case 'custom':
                this.applyCustomEnhancement(imageData);
                break;
            default:
                this.applyDefaultEnhancement(imageData);
                break;
        }

        ctx.putImageData(imageData, 0, 0);
    }

    applyAnimeEnhancement(imageData) {
        // Enhanced for anime content - boost saturation and sharpness
        this.adjustImageData(imageData, {
            sharpness: 3,
            saturation: 1.2,
            contrast: 1.1
        });
    }

    applyMovieEnhancement(imageData) {
        // Balanced enhancement for movies
        this.adjustImageData(imageData, {
            sharpness: 2,
            saturation: 1.0,
            contrast: 1.0
        });
    }

    applyTVShowEnhancement(imageData) {
        // Lighter enhancement for TV shows
        this.adjustImageData(imageData, {
            sharpness: 1,
            saturation: 1.0,
            contrast: 1.0
        });
    }

    applyCustomEnhancement(imageData) {
        const settings = this.config.CustomSettings;
        this.adjustImageData(imageData, {
            sharpness: settings.Sharpness,
            saturation: settings.Saturation,
            contrast: settings.Contrast
        });
    }

    applyDefaultEnhancement(imageData) {
        this.adjustImageData(imageData, {
            sharpness: 2,
            saturation: 1.0,
            contrast: 1.0
        });
    }

    adjustImageData(imageData, params) {
        const data = imageData.data;

        for (let i = 0; i < data.length; i += 4) {
            let r = data[i];
            let g = data[i + 1];
            let b = data[i + 2];

            // Apply contrast
            if (params.contrast !== 1.0) {
                r = Math.min(255, Math.max(0, (r - 128) * params.contrast + 128));
                g = Math.min(255, Math.max(0, (g - 128) * params.contrast + 128));
                b = Math.min(255, Math.max(0, (b - 128) * params.contrast + 128));
            }

            // Apply saturation
            if (params.saturation !== 1.0) {
                const gray = 0.299 * r + 0.587 * g + 0.114 * b;
                r = Math.min(255, Math.max(0, gray + params.saturation * (r - gray)));
                g = Math.min(255, Math.max(0, gray + params.saturation * (g - gray)));
                b = Math.min(255, Math.max(0, gray + params.saturation * (b - gray)));
            }

            data[i] = r;
            data[i + 1] = g;
            data[i + 2] = b;
        }

        // Apply sharpness (simplified edge enhancement)
        if (params.sharpness > 0) {
            this.applySharpeningFilter(imageData, params.sharpness * 0.2);
        }
    }

    applySharpeningFilter(imageData, strength) {
        // Simple sharpening kernel
        const width = imageData.width;
        const height = imageData.height;
        const data = imageData.data;
        const output = new Uint8ClampedArray(data);

        const kernel = [
            0, -1, 0,
            -1, 5, -1,
            0, -1, 0
        ];

        for (let y = 1; y < height - 1; y++) {
            for (let x = 1; x < width - 1; x++) {
                const idx = (y * width + x) * 4;

                for (let c = 0; c < 3; c++) { // RGB channels only
                    let sum = 0;
                    for (let ky = -1; ky <= 1; ky++) {
                        for (let kx = -1; kx <= 1; kx++) {
                            const kidx = ((y + ky) * width + (x + kx)) * 4 + c;
                            const kval = kernel[(ky + 1) * 3 + (kx + 1)];
                            sum += data[kidx] * kval;
                        }
                    }

                    output[idx + c] = Math.min(255, Math.max(0,
                        data[idx + c] + (sum - data[idx + c]) * strength
                    ));
                }
            }
        }

        // Copy sharpened data back
        for (let i = 0; i < data.length; i++) {
            data[i] = output[i];
        }
    }

    resolutionToHeight(resolution) {
        const map = {
            '480p': 480,
            '720p': 720,
            '1080p': 1080,
            '1440p': 1440,
            '2160p': 2160,
            '4320p': 4320
        };
        return map[resolution] || 1080;
    }
}

// Initialize the upscaler when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.jellyfinUpscaler = new JellyfinUpscaler();
    });
} else {
    window.jellyfinUpscaler = new JellyfinUpscaler();
}
