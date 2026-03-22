mergeInto(LibraryManager.library, {

    // ━━━ Depth Occlusion (8th Wall Depth Module) ━━━
    // Bridges 8th Wall's depth pipeline data into Unity for real-world occlusion.

    WebGLStartDepthOcclusion: function(configJsonPtr) {
        var configJson = UTF8ToString(configJsonPtr);
        var config = JSON.parse(configJson);
        console.log('[XR8DepthLib] Starting depth occlusion:', config);

        if (!window.XR8) {
            console.error('[XR8DepthLib] XR8 not found!');
            return;
        }

        window._xr8DepthConfig = config;

        // Register depth pipeline module
        window._xr8DepthModule = {
            name: 'xr8-depth-unity',

            onStart: function(e) {
                // Check if depth is available on this device
                var hasDepth = false;
                try {
                    // 8th Wall exposes depth availability after the pipeline starts
                    hasDepth = !!(e && e.cameraParameters && e.cameraParameters.depthIntrinsics);
                } catch(ex) {}

                if (!hasDepth) {
                    // Also check via the XR8 API
                    try {
                        hasDepth = XR8.XrController && XR8.XrController.pipelineModule &&
                                   XR8.XrController.pipelineModule().depthSupported;
                    } catch(ex) {}
                }

                try {
                    var objName = config.unityObjectName || 'XR8DepthOcclusion';
                    if (hasDepth) {
                        window.unityInstance.SendMessage(objName, 'OnDepthStart', '1');
                    } else {
                        window.unityInstance.SendMessage(objName, 'OnDepthNotSupported', '0');
                    }
                } catch(ex) {}
            },

            onProcessCpu: function(e) {
                if (!e || !e.reality) return;

                // Capture depth data from the pipeline
                if (e.reality.realityTexture && e.reality.realityTexture.depthTexture) {
                    window._xr8LastDepthFrame = e.reality.realityTexture.depthTexture;
                }

                // Depth confidence
                if (e.reality.trackingStatus) {
                    var confidence = e.reality.trackingStatus === 'TRACKING' ? 1.0 :
                                     e.reality.trackingStatus === 'LIMITED' ? 0.5 : 0.0;
                    try {
                        window.unityInstance.SendMessage(
                            config.unityObjectName || 'XR8DepthOcclusion',
                            'OnDepthConfidenceReceived',
                            confidence.toString()
                        );
                    } catch(ex) {}
                }
            },

            onRender: function(e) {
                // Upload depth frame to Unity GL texture
                if (window._xr8DepthTextureId && window._xr8LastDepthFrame) {
                    try {
                        var textureObj = GL.textures[window._xr8DepthTextureId];
                        if (!textureObj) return;

                        GLctx.bindTexture(GLctx.TEXTURE_2D, textureObj);
                        GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_WRAP_S, GLctx.CLAMP_TO_EDGE);
                        GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_WRAP_T, GLctx.CLAMP_TO_EDGE);
                        GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_MIN_FILTER, GLctx.LINEAR);
                        GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, true);

                        var depthData = window._xr8LastDepthFrame;
                        if (depthData instanceof HTMLCanvasElement ||
                            depthData instanceof HTMLVideoElement ||
                            depthData instanceof ImageData) {
                            GLctx.texSubImage2D(GLctx.TEXTURE_2D, 0, 0, 0,
                                GLctx.RGBA, GLctx.UNSIGNED_BYTE, depthData);
                        }

                        GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, false);
                    } catch(ex) {
                        console.warn('[XR8DepthLib] Depth texture upload failed:', ex);
                    }
                }
            }
        };

        try {
            XR8.addCameraPipelineModule(window._xr8DepthModule);
            console.log('[XR8DepthLib] Depth module registered');
        } catch(ex) {
            console.error('[XR8DepthLib] Failed to register depth module:', ex);
        }
    },

    WebGLStopDepthOcclusion: function() {
        if (window._xr8DepthModule && window.XR8) {
            try {
                XR8.removeCameraPipelineModule(window._xr8DepthModule.name);
            } catch(ex) {}
        }
        window._xr8DepthModule = null;
        window._xr8LastDepthFrame = null;
    },

    WebGLSubscribeDepthTexture: function(textureId) {
        window._xr8DepthTextureId = textureId;
        console.log('[XR8DepthLib] Subscribed depth to texture ' + textureId);
    },

    WebGLIsDepthSupported: function() {
        try {
            if (window.XR8 && XR8.XrController) {
                return XR8.XrController.pipelineModule().depthSupported ? 1 : 0;
            }
        } catch(ex) {}
        return 0;
    },
});
