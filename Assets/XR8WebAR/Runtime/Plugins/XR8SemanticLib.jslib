mergeInto(LibraryManager.library, {

    // ━━━ Semantic Segmentation (8th Wall LayersController) ━━━
    // Bridges 8th Wall's sky/person segmentation layers into Unity via GL texture upload.

    WebGLStartSemanticSegmentation: function(configJsonPtr) {
        var configJson = UTF8ToString(configJsonPtr);
        var config = JSON.parse(configJson);
        console.log('[XR8SemanticLib] Starting segmentation:', config);

        if (!window.XR8) {
            console.error('[XR8SemanticLib] XR8 not found!');
            return;
        }

        // Store config for the pipeline module
        window._xr8SemanticConfig = config;

        // Register a custom pipeline module that captures segmentation masks
        window._xr8SemanticModule = {
            name: 'xr8-semantic-unity',

            onUpdate: function(e) {
                // LayersController provides segmentation data on processCpuResult
            },

            onProcessCpu: function(e) {
                if (!e || !e.reality) return;
                var reality = e.reality;

                // Sky mask — available via LayersController when sky layer is enabled
                if (reality.skyMask && window._xr8SemanticSkyEnabled) {
                    window._xr8LastSkyMask = reality.skyMask;
                    try {
                        window.unityInstance.SendMessage(
                            config.unityObjectName || 'XR8SemanticLayer',
                            'OnSkyMaskReady', '1'
                        );
                    } catch(ex) {}
                }

                // Person mask — available via LayersController when person layer is enabled
                if (reality.personMask && window._xr8SemanticPersonEnabled) {
                    window._xr8LastPersonMask = reality.personMask;
                    try {
                        window.unityInstance.SendMessage(
                            config.unityObjectName || 'XR8SemanticLayer',
                            'OnPersonMaskReady', '1'
                        );
                    } catch(ex) {}
                }

                // Lighting estimation enhancement (ambient intensity + color temperature)
                if (reality.lighting) {
                    var l = reality.lighting;
                    try {
                        window.unityInstance.SendMessage(
                            config.unityObjectName || 'XR8SemanticLayer',
                            'OnLightingEstimate',
                            (l.ambientIntensity || 1.0) + ',' +
                            (l.ambientColorTemperature || 6500) + ',' +
                            (l.directionalLightEstimate ? l.directionalLightEstimate.direction.x : 0) + ',' +
                            (l.directionalLightEstimate ? l.directionalLightEstimate.direction.y : -1) + ',' +
                            (l.directionalLightEstimate ? l.directionalLightEstimate.direction.z : 0)
                        );
                    } catch(ex) {}
                }
            },

            onRender: function(e) {
                // Upload sky mask to Unity GL texture if requested
                if (window._xr8SemanticSkyTextureId && window._xr8LastSkyMask) {
                    _uploadMaskToTexture(window._xr8SemanticSkyTextureId, window._xr8LastSkyMask);
                }

                // Upload person mask to Unity GL texture if requested
                if (window._xr8SemanticPersonTextureId && window._xr8LastPersonMask) {
                    _uploadMaskToTexture(window._xr8SemanticPersonTextureId, window._xr8LastPersonMask);
                }
            }
        };

        // Helper: upload a mask (ImageData/canvas) to a Unity GL texture
        window._uploadMaskToTexture = function(textureId, maskData) {
            try {
                var textureObj = GL.textures[textureId];
                if (!textureObj) return;

                GLctx.bindTexture(GLctx.TEXTURE_2D, textureObj);
                GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_WRAP_S, GLctx.CLAMP_TO_EDGE);
                GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_WRAP_T, GLctx.CLAMP_TO_EDGE);
                GLctx.texParameteri(GLctx.TEXTURE_2D, GLctx.TEXTURE_MIN_FILTER, GLctx.LINEAR);
                GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, true);

                if (maskData instanceof ImageData) {
                    GLctx.texImage2D(GLctx.TEXTURE_2D, 0, GLctx.RGBA,
                        GLctx.RGBA, GLctx.UNSIGNED_BYTE, maskData);
                } else if (maskData instanceof HTMLCanvasElement || maskData instanceof HTMLVideoElement) {
                    GLctx.texSubImage2D(GLctx.TEXTURE_2D, 0, 0, 0,
                        GLctx.RGBA, GLctx.UNSIGNED_BYTE, maskData);
                }

                GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, false);
            } catch(ex) {
                console.warn('[XR8SemanticLib] Texture upload failed:', ex);
            }
        };

        // Add the module to XR8 pipeline
        try {
            XR8.addCameraPipelineModule(window._xr8SemanticModule);
            console.log('[XR8SemanticLib] Semantic module registered');
        } catch(ex) {
            console.error('[XR8SemanticLib] Failed to register module:', ex);
        }
    },

    WebGLStopSemanticSegmentation: function() {
        if (window._xr8SemanticModule && window.XR8) {
            try {
                XR8.removeCameraPipelineModule(window._xr8SemanticModule.name);
            } catch(ex) {}
        }
        window._xr8SemanticModule = null;
        window._xr8LastSkyMask = null;
        window._xr8LastPersonMask = null;
    },

    WebGLEnableSemanticLayer: function(layerNamePtr, enable) {
        var layerName = UTF8ToString(layerNamePtr);
        console.log('[XR8SemanticLib] Layer ' + layerName + ': ' + (enable ? 'ON' : 'OFF'));

        if (layerName === 'sky') {
            window._xr8SemanticSkyEnabled = enable;
        } else if (layerName === 'person') {
            window._xr8SemanticPersonEnabled = enable;
        }

        // Configure LayersController if available
        if (window.XR8 && window.XR8.LayersController) {
            try {
                XR8.LayersController.configure({
                    [layerName]: { enabled: !!enable }
                });
            } catch(ex) {
                console.warn('[XR8SemanticLib] LayersController config failed:', ex);
            }
        }
    },

    WebGLSubscribeSemanticTexture: function(layerNamePtr, textureId) {
        var layerName = UTF8ToString(layerNamePtr);
        if (layerName === 'sky') {
            window._xr8SemanticSkyTextureId = textureId;
        } else if (layerName === 'person') {
            window._xr8SemanticPersonTextureId = textureId;
        }
        console.log('[XR8SemanticLib] Subscribed ' + layerName + ' to texture ' + textureId);
    },
});
