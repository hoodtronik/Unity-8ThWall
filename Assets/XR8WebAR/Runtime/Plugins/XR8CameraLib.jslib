mergeInto(LibraryManager.library, {

    WebGLStartXR8: function() {
        if (!window.xr8Camera) {
            console.error('[XR8CameraLib] xr8Camera not found! Use the 8thWallTracker WebGL template.');
            throw new Error("xr8Camera bridge not found");
        }
        window.xr8Camera.start(window.xr8Tracker);
    },

    WebGLStartXR8WithConfig: function(configJsonPtr) {
        var configJson = UTF8ToString(configJsonPtr);
        console.log('[XR8CameraLib] StartWithConfig:', configJson);

        if (!window.xr8Camera) {
            console.error('[XR8CameraLib] xr8Camera not found! Use the 8thWallTracker WebGL template.');
            try {
                window.unityInstance.SendMessage('XR8Manager', 'OnXR8Error', 'xr8Camera bridge not found');
            } catch(e) {}
            return;
        }

        try {
            var config = JSON.parse(configJson);
            window.xr8ManagerConfig = config;

            // Start camera + tracker with combined config
            window.xr8Camera.start(window.xr8Tracker, config);

            // Notify Unity that engine is ready (after a tick for XR8 to init)
            setTimeout(function() {
                try {
                    window.unityInstance.SendMessage('XR8Manager', 'OnXR8Ready');
                } catch(e) {
                    console.error('[XR8CameraLib] Failed to send OnXR8Ready:', e);
                }
            }, 500);
        } catch(e) {
            console.error('[XR8CameraLib] Config parse error:', e);
            try {
                window.unityInstance.SendMessage('XR8Manager', 'OnXR8Error', e.toString());
            } catch(ex) {}
        }
    },

    WebGLStopXR8: function() {
        if (window.xr8Camera) {
            window.xr8Camera.stop();
        }
    },

    WebGLIsXR8Started: function() {
        return window.xr8Camera ? window.xr8Camera.isCameraStarted : false;
    },

    WebGLPauseXR8Camera: function() {
        if (window.xr8Camera) {
            window.xr8Camera.pause();
        }
    },

    WebGLUnpauseXR8Camera: function() {
        if (window.xr8Camera) {
            window.xr8Camera.unpause();
        }
    },

    WebGLGetXR8CameraFov: function() {
        return window.xr8Camera ? window.xr8Camera.FOV : 60;
    },

    WebGLGetXR8VideoDims: function() {
        var data = window.xr8Camera ? window.xr8Camera.getVideoDims() : "640,480";
        var bufferSize = lengthBytesUTF8(data) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(data, buffer, bufferSize);
        return buffer;
    },

    WebGLSubscribeXR8VideoTexturePtr: function(textureId) {
        if (!window.xr8Camera) return;

        window.xr8Camera.updateUnityVideoTextureCallback = function() {
            // Get the video element that XR8 uses for camera feed
            var videoEl = document.querySelector('video');
            if (!videoEl) return;

            var textureObj = GL.textures[textureId];
            if (!videoEl || !textureObj) return;

            GLctx.bindTexture(GLctx.TEXTURE_2D, textureObj);
            GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_WRAP_S, GLctx.CLAMP_TO_EDGE);
            GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_WRAP_T, GLctx.CLAMP_TO_EDGE);
            GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_MIN_FILTER, GLctx.LINEAR);
            GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, true);
            GLctx.texSubImage2D(GLctx.TEXTURE_2D, 0, 0, 0, GLctx.RGBA, GLctx.UNSIGNED_BYTE, videoEl);
            GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, false);
        };
    },

    WebGLIsWebcamPermissionGranted: function() {
        return window.xr8Camera ? window.xr8Camera.isCameraStarted : false;
    },
});
