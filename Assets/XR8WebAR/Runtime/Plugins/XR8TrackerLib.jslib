mergeInto(LibraryManager.library, {

    StartXR8ImageTracker: function(ids, name) {
        if (!window.xr8Tracker) {
            console.error('[XR8TrackerLib] xr8Tracker not found! Use the 8thWallTracker WebGL template.');
            throw new Error("xr8Tracker bridge not found");
        }
        window.xr8Tracker.configure(UTF8ToString(ids), UTF8ToString(name));
    },

    StopXR8ImageTracker: function() {
        if (window.xr8Tracker) {
            window.xr8Tracker.stop();
        }
    },

    IsXR8TrackerReady: function() {
        return window.xr8Tracker ? window.xr8Tracker.isReady : false;
    },

    SetXR8TrackerSettings: function(settings) {
        if (!window.xr8Tracker) return;

        var parsed = JSON.parse(UTF8ToString(settings));

        // Apply settings to the bridge
        if (parsed.DISABLE_WORLD_TRACKING !== undefined) {
            window.xr8Tracker.disableWorldTracking = parsed.DISABLE_WORLD_TRACKING;
        }

        console.log('[XR8TrackerLib] Settings applied:', parsed);
    },

    // ━━━ World Tracker Bridge Functions ━━━

    // Hit test at normalized screen coordinates (0-1)
    WebGLXR8HitTest: function(screenX, screenY) {
        if (window.xr8World) {
            window.xr8World.hitTest(screenX, screenY);
        } else {
            console.warn('[XR8TrackerLib] World bridge not available for hit test');
        }
    },

    // Send viewport position to XR8 engine for SLAM-aware repositioning
    // Used by TapToReposition and TwoFingerPan
    WebGLSetViewportPos: function(vStr) {
        var str = UTF8ToString(vStr);
        if (window.xr8World && window.xr8World.setViewportPos) {
            window.xr8World.setViewportPos(str);
        } else {
            console.warn('[XR8TrackerLib] setViewportPos not available');
        }
    },

    // Place the world origin at the current camera position
    WebGLPlaceOrigin: function(camPosStr) {
        var str = UTF8ToString(camPosStr);
        if (window.xr8World && window.xr8World.placeOrigin) {
            window.xr8World.placeOrigin(str);
        } else {
            console.log('[XR8TrackerLib] placeOrigin: world bridge N/A (using Unity-side only)');
        }
    },

    // Reset world origin in the XR8 JS engine
    WebGLResetOrigin: function() {
        if (window.xr8World && window.xr8World.resetOrigin) {
            window.xr8World.resetOrigin();
        } else {
            console.log('[XR8TrackerLib] resetOrigin: world bridge N/A (using Unity-side only)');
        }
    },

    // Send world tracker settings (mode, arm length, smoothing, compass, etc.) to JS engine
    SetWebGLWorldTrackerSettings: function(settings) {
        var parsed = JSON.parse(UTF8ToString(settings));
        if (window.xr8World && window.xr8World.setTrackerSettings) {
            window.xr8World.setTrackerSettings(parsed);
        } else {
            // Store for later — the world bridge may not be initialized yet
            window._pendingWorldSettings = parsed;
        }
        console.log('[XR8TrackerLib] World tracker settings:', parsed);
    },

    // ━━━ Texture Extractor ━━━

    // Extract the de-warped (perspective-corrected) texture from a tracked image target
    // Writes the texture data into the GL texture with the given ID
    GetXR8WarpedTexture: function(targetId, textureId, resolution) {
        var id = UTF8ToString(targetId);
        if (window.xr8Tracker && window.xr8Tracker.getWarpedTexture) {
            window.xr8Tracker.getWarpedTexture(id, textureId, resolution);
        } else {
            // Silently skip if the feature isn't wired up yet
        }
    },
});
