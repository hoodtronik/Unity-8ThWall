/**
 * xr8-bridge.js — Open-source bridge between 8th Wall XR8 engine and Unity WebGL
 *
 * This replaces Imagine WebAR's obfuscated arcamera.js + itracker.js with a
 * transparent, readable implementation built on XR8's public API.
 *
 * Classes:
 *   XR8CameraBridge  — camera lifecycle, video texture, FOV, orientation
 *   XR8TrackerBridge — image target tracking, pose data forwarding to Unity
 *   XR8WorldBridge   — world/SLAM tracking, surface detection, hit testing
 *
 * Data is forwarded to Unity via unityInstance.SendMessage(), using the same
 * CSV format that the C# scripts expect.
 *
 * License: MIT
 * Author: hoodtronik (https://github.com/hoodtronik)
 */

// ============================================================================
// XR8CameraBridge — Camera management
// ============================================================================
class XR8CameraBridge {
    constructor(unityCanvas) {
        this.unityCanvas = unityCanvas;
        this.isCameraStarted = false;
        this.isPaused = false;
        this.isFlipped = false;
        this.FOV = 60;
        this.videoWidth = 0;
        this.videoHeight = 0;
        this.orientation = window.matchMedia('(orientation: portrait)').matches
            ? 'PORTRAIT' : 'LANDSCAPE';
        this.lastOrientation = this.orientation;

        // Video texture callback (set by XR8CameraLib.jslib)
        this.updateUnityVideoTextureCallback = null;

        // Resize handling
        this.RESIZE_DELAY = 50;
        window.addEventListener('resize', this._onResize.bind(this), true);
    }

    // Start XR8 engine and camera
    // trackerBridge: optional XR8TrackerBridge for image tracking
    // config: optional combined config from XR8Manager (JSON object)
    start(trackerBridge, config) {
        var self = this;
        var canvas = this.unityCanvas;

        // Make canvas transparent for AR
        canvas.style.background = 'transparent';

        // Camera pipeline module — feeds each frame to Unity
        var unityPipelineModule = {
            name: 'unity-camera-bridge',
            onStart: function () {
                self.isCameraStarted = true;
                console.log('[XR8CameraBridge] Camera started');
            },
            onUpdate: function (e) {
                // Update video dimensions
                if (e.processCpuResult && e.processCpuResult.reality) {
                    var r = e.processCpuResult.reality;
                    if (r.intrinsics) {
                        // Compute vertical FOV from intrinsics
                        var fy = r.intrinsics[5]; // focal length Y in pixels
                        var h = r.intrinsics[7] * 2; // image height
                        if (fy > 0 && h > 0) {
                            self.FOV = 2 * Math.atan(h / (2 * fy)) * 180 / Math.PI;
                        }
                    }
                }

                // Push video texture to Unity if subscribed
                if (self.updateUnityVideoTextureCallback) {
                    self.updateUnityVideoTextureCallback();
                }
            },
            onCanvasSizeChange: function () {
                self._resizeCanvas();
            }
        };

        // Determine tracking settings from config or fallback
        var disableWorldTracking = true;
        var enableLighting = true;

        if (config) {
            // XR8Manager unified config
            disableWorldTracking = !config.enableWorldTracking;
            enableLighting = config.enableLighting !== false;
        } else if (trackerBridge) {
            // Legacy per-component config
            disableWorldTracking = trackerBridge.disableWorldTracking;
        }

        // Configure XR8
        var xrConfig = {
            disableWorldTracking: disableWorldTracking,
            enableLighting: enableLighting,
            scale: 'responsive'
        };

        // Add image targets if tracker is configured
        if (trackerBridge && trackerBridge.imageTargetData) {
            xrConfig.imageTargets = trackerBridge.imageTargetData;
        }

        XR8.XrController.configure(xrConfig);

        // Build pipeline modules
        var modules = [
            XR8.GlTextureRenderer.pipelineModule(),
            XR8.XrController.pipelineModule(),
            unityPipelineModule
        ];

        // Add image tracker pipeline module if present and enabled
        if (trackerBridge && (!config || config.enableImageTracking !== false)) {
            modules.push(trackerBridge.getPipelineModule());
        }

        // Add world tracking bridge if enabled
        if (config && config.enableWorldTracking && window.xr8World) {
            modules.push(window.xr8World.getPipelineModule());
        }

        XR8.addCameraPipelineModules(modules);

        // Run XR8 on the Unity canvas
        XR8.run({ canvas: canvas });

        // Get video dimensions after a short delay
        setTimeout(function () {
            self._updateVideoDims();
            self._resizeCanvas();

            // Notify Unity about camera start
            if (window.unityInstance) {
                window.unityInstance.SendMessage('XR8Camera', 'OnStartWebcamSuccess');
            }
        }, 500);
    }

    stop() {
        if (this.isCameraStarted) {
            try { XR8.stop(); } catch (e) { }
            this.isCameraStarted = false;
            console.log('[XR8CameraBridge] Camera stopped');
        }
    }

    pause() {
        if (!this.isPaused) {
            try { XR8.pause(); } catch (e) { }
            this.isPaused = true;
        }
    }

    unpause() {
        if (this.isPaused) {
            try { XR8.resume(); } catch (e) { }
            this.isPaused = false;
        }
    }

    getVideoDims() {
        this._updateVideoDims();
        return this.videoWidth + ',' + this.videoHeight;
    }

    _updateVideoDims() {
        // Try to get video element from XR8's GlTextureRenderer
        var video = document.querySelector('#xr8-video') ||
                    document.querySelector('video');
        if (video && video.videoWidth > 0) {
            this.videoWidth = video.videoWidth;
            this.videoHeight = video.videoHeight;
        }
    }

    _resizeCanvas() {
        this.unityCanvas.style.width = window.innerWidth + 'px';
        this.unityCanvas.style.height = window.innerHeight + 'px';

        var newOrientation = window.matchMedia('(orientation: portrait)').matches
            ? 'PORTRAIT' : 'LANDSCAPE';

        if (newOrientation !== this.lastOrientation) {
            this.lastOrientation = newOrientation;
            this.orientation = newOrientation;
            if (window.unityInstance) {
                window.unityInstance.SendMessage('XR8Camera', 'SetOrientationMessage', newOrientation);
            }
        }

        // Update FOV in Unity
        if (window.unityInstance && this.FOV > 0) {
            window.unityInstance.SendMessage('XR8Camera', 'SetCameraFov', this.FOV);
        }

        // Notify Unity about resize
        if (window.unityInstance && this.videoWidth > 0) {
            window.unityInstance.SendMessage(
                'XR8Camera', 'Resize',
                this.videoWidth + ',' + this.videoHeight
            );
        }
    }

    _onResize(event) {
        if (event && event.target !== window) return;
        var self = this;
        setTimeout(function () { self._resizeCanvas(); }, this.RESIZE_DELAY);
    }
}


// ============================================================================
// XR8TrackerBridge — Image target tracking
// ============================================================================
class XR8TrackerBridge {
    constructor() {
        this.trackerName = 'XR8ImageTracker';  // Unity GameObject name
        this.targetIds = [];
        this.isReady = false;
        this.disableWorldTracking = true;
        this.imageTargetData = null;  // Set from Unity settings
    }

    // Configure with target IDs and Unity object name
    configure(ids, unityObjectName) {
        this.targetIds = ids.split(',').map(function (s) { return s.trim(); });
        this.trackerName = unityObjectName;
        this.isReady = true;
        console.log('[XR8TrackerBridge] Configured targets:', this.targetIds,
                     'Unity object:', this.trackerName);
    }

    stop() {
        this.isReady = false;
        console.log('[XR8TrackerBridge] Stopped');
    }

    // Build XR8 pipeline module for image tracking events
    getPipelineModule() {
        var self = this;
        return {
            name: 'unity-image-tracker-bridge',
            listeners: [
                {
                    event: 'reality.imagefound',
                    process: function (e) { self._onImageFound(e); }
                },
                {
                    event: 'reality.imageupdated',
                    process: function (e) { self._onImageUpdated(e); }
                },
                {
                    event: 'reality.imagelost',
                    process: function (e) { self._onImageLost(e); }
                }
            ]
        };
    }

    // --- Event handlers ---

    _onImageFound(event) {
        var detail = event.detail || event;
        var name = detail.name;

        if (!this._isTrackedTarget(name)) return;

        console.log('[XR8TrackerBridge] Image FOUND:', name);

        // Send found event to Unity
        if (window.unityInstance) {
            window.unityInstance.SendMessage(this.trackerName, 'OnTrackingFound', name);
        }

        // Also send pose data
        this._sendPoseData(detail);
    }

    _onImageUpdated(event) {
        var detail = event.detail || event;
        var name = detail.name;

        if (!this._isTrackedTarget(name)) return;

        this._sendPoseData(detail);
    }

    _onImageLost(event) {
        var detail = event.detail || event;
        var name = detail.name;

        if (!this._isTrackedTarget(name)) return;

        console.log('[XR8TrackerBridge] Image LOST:', name);

        if (window.unityInstance) {
            window.unityInstance.SendMessage(this.trackerName, 'OnTrackingLost', name);
        }
    }

    // Convert XR8 pose data to CSV string and send to Unity
    _sendPoseData(detail) {
        if (!detail.position || !detail.rotation) return;

        var pos = detail.position;
        var rot = detail.rotation;

        // XR8 gives us position (x,y,z) and rotation as a quaternion (x,y,z,w)
        // We need to convert to the format Unity expects:
        // id, posX, posY, posZ, fwdX, fwdY, fwdZ, upX, upY, upZ, rightX, rightY, rightZ

        // Convert quaternion to direction vectors
        var qx = rot.x, qy = rot.y, qz = rot.z, qw = rot.w;

        // Forward vector (Z axis of rotation)
        var fwdX = 2 * (qx * qz + qw * qy);
        var fwdY = 2 * (qy * qz - qw * qx);
        var fwdZ = 1 - 2 * (qx * qx + qy * qy);

        // Up vector (Y axis of rotation)
        var upX = 2 * (qx * qy - qw * qz);
        var upY = 1 - 2 * (qx * qx + qz * qz);
        var upZ = 2 * (qy * qz + qw * qx);

        // Right vector (X axis of rotation)
        var rightX = 1 - 2 * (qy * qy + qz * qz);
        var rightY = 2 * (qx * qy + qw * qz);
        var rightZ = 2 * (qx * qz - qw * qy);

        var csv = detail.name + ',' +
            pos.x.toFixed(6) + ',' + pos.y.toFixed(6) + ',' + pos.z.toFixed(6) + ',' +
            fwdX.toFixed(6) + ',' + fwdY.toFixed(6) + ',' + fwdZ.toFixed(6) + ',' +
            upX.toFixed(6) + ',' + upY.toFixed(6) + ',' + upZ.toFixed(6) + ',' +
            rightX.toFixed(6) + ',' + rightY.toFixed(6) + ',' + rightZ.toFixed(6);

        if (window.unityInstance) {
            window.unityInstance.SendMessage(this.trackerName, 'OnTrack', csv);
        }
    }

    _isTrackedTarget(name) {
        // If no specific targets configured, track all
        if (this.targetIds.length === 0) return true;
        return this.targetIds.indexOf(name) >= 0;
    }
}


// ============================================================================
// XR8WorldBridge — World/SLAM tracking, surface detection, hit testing
// ============================================================================
class XR8WorldBridge {
    constructor() {
        this.trackerName = 'XR8WorldTracker'; // Unity GameObject name
        this.isReady = false;
        this.showMeshes = false;
        this.surfaces = {};  // Active surfaces by ID
    }

    configure(unityObjectName, showMeshes) {
        this.trackerName = unityObjectName || 'XR8WorldTracker';
        this.showMeshes = showMeshes || false;
        this.isReady = true;
        console.log('[XR8WorldBridge] Configured. Object:', this.trackerName);
    }

    getPipelineModule() {
        var self = this;
        return {
            name: 'unity-world-tracker-bridge',
            onUpdate: function(e) {
                self._onUpdate(e);
            },
            listeners: [
                {
                    event: 'reality.meshfound',
                    process: function(e) { self._onMeshFound(e); }
                },
                {
                    event: 'reality.meshupdated',
                    process: function(e) { self._onMeshUpdated(e); }
                },
                {
                    event: 'reality.meshlost',
                    process: function(e) { self._onMeshLost(e); }
                }
            ]
        };
    }

    // Perform a hit test from screen coordinates
    hitTest(screenX, screenY) {
        if (!XR8 || !XR8.XrController) return null;

        try {
            var hits = XR8.XrController.hitTest(screenX, screenY, ['FEATURE_POINT']);
            if (hits && hits.length > 0) {
                var hit = hits[0];
                // Send hit result to Unity as CSV: posX,posY,posZ,normalX,normalY,normalZ
                var csv = hit.position.x.toFixed(6) + ',' +
                          hit.position.y.toFixed(6) + ',' +
                          hit.position.z.toFixed(6) + ',' +
                          (hit.normal ? hit.normal.x.toFixed(6) : '0') + ',' +
                          (hit.normal ? hit.normal.y.toFixed(6) : '1') + ',' +
                          (hit.normal ? hit.normal.z.toFixed(6) : '0');

                if (window.unityInstance) {
                    window.unityInstance.SendMessage(self.trackerName, 'OnHitTestResult', csv);
                }
                return csv;
            }
        } catch(e) {
            console.warn('[XR8WorldBridge] hitTest failed:', e);
        }
        return null;
    }

    _onUpdate(e) {
        // Could forward 6DOF camera pose to Unity for world-space camera movement
        if (e.processCpuResult && e.processCpuResult.reality) {
            var r = e.processCpuResult.reality;
            if (r.rotation && r.position) {
                var pos = r.position;
                var rot = r.rotation;
                var csv = pos.x.toFixed(6) + ',' + pos.y.toFixed(6) + ',' + pos.z.toFixed(6) + ',' +
                          rot.x.toFixed(6) + ',' + rot.y.toFixed(6) + ',' + rot.z.toFixed(6) + ',' + rot.w.toFixed(6);

                if (window.unityInstance) {
                    window.unityInstance.SendMessage(this.trackerName, 'OnCameraPose', csv);
                }
            }
        }
    }

    _onMeshFound(event) {
        var detail = event.detail || event;
        var id = detail.id || 'surface-' + Object.keys(this.surfaces).length;
        this.surfaces[id] = detail;

        console.log('[XR8WorldBridge] Surface FOUND:', id);
        if (window.unityInstance) {
            var csv = id + ',' + detail.position.x.toFixed(6) + ',' +
                      detail.position.y.toFixed(6) + ',' + detail.position.z.toFixed(6);
            window.unityInstance.SendMessage(this.trackerName, 'OnSurfaceFound', csv);
        }
    }

    _onMeshUpdated(event) {
        var detail = event.detail || event;
        var id = detail.id || Object.keys(this.surfaces)[0];
        this.surfaces[id] = detail;

        if (window.unityInstance) {
            var csv = id + ',' + detail.position.x.toFixed(6) + ',' +
                      detail.position.y.toFixed(6) + ',' + detail.position.z.toFixed(6);
            window.unityInstance.SendMessage(this.trackerName, 'OnSurfaceUpdated', csv);
        }
    }

    _onMeshLost(event) {
        var detail = event.detail || event;
        var id = detail.id || Object.keys(this.surfaces)[0];
        delete this.surfaces[id];

        console.log('[XR8WorldBridge] Surface LOST:', id);
        if (window.unityInstance) {
            window.unityInstance.SendMessage(this.trackerName, 'OnSurfaceLost', id);
        }
    }
}


// ============================================================================
// Global initialization helpers (called from index.html)
// ============================================================================
window.XR8CameraBridge = XR8CameraBridge;
window.XR8TrackerBridge = XR8TrackerBridge;
window.XR8WorldBridge = XR8WorldBridge;
