var XR8LightEstimationLib = {

    $WGL_LE_State: {
        gameObjectName: null,
        active: false,
        frameCount: 0,
        analyzeInterval: 5  // Analyze every N frames for performance
    },

    WebGLStartLightEstimation: function(goNamePtr) {
        var goName = UTF8ToString(goNamePtr);
        WGL_LE_State.gameObjectName = goName;
        WGL_LE_State.active = true;
        WGL_LE_State.frameCount = 0;

        // Hook into 8th Wall pipeline for light estimation
        if (typeof XR8 !== 'undefined' && XR8.addCameraPipelineModule) {
            XR8.addCameraPipelineModule({
                name: 'xr8-light-estimation',
                onProcessCpu: function(args) {
                    if (!WGL_LE_State.active) return;
                    WGL_LE_State.frameCount++;
                    if (WGL_LE_State.frameCount % WGL_LE_State.analyzeInterval !== 0) return;

                    var result = args.processCpuResult;
                    
                    // Extract lighting data from 8th Wall
                    var intensity = 1.0;
                    var colorR = 1.0, colorG = 1.0, colorB = 1.0;
                    var dirX = 50.0, dirY = -30.0, dirZ = 0.0;
                    var ambient = 0.5;

                    // 8th Wall provides light estimation via reality module
                    if (result && result.reality && result.reality.lighting) {
                        var lighting = result.reality.lighting;
                        if (lighting.exposure !== undefined) {
                            intensity = Math.max(0.1, Math.min(3.0, lighting.exposure));
                        }
                        if (lighting.temperature !== undefined) {
                            // Convert color temperature (Kelvin) to RGB approximation
                            var temp = lighting.temperature;
                            var rgb = _kelvinToRGB(temp);
                            colorR = rgb[0];
                            colorG = rgb[1];
                            colorB = rgb[2];
                        }
                        if (lighting.direction) {
                            dirX = lighting.direction.x || 50.0;
                            dirY = lighting.direction.y || -30.0;
                            dirZ = lighting.direction.z || 0.0;
                        }
                        if (lighting.ambientIntensity !== undefined) {
                            ambient = lighting.ambientIntensity;
                        }
                    }

                    // Fallback: analyze camera frame brightness if no API data
                    if (result && result.reality && result.reality.intrinsics) {
                        // Use exposure compensation as intensity hint
                        if (result.reality.intrinsics.exposureCompensation !== undefined) {
                            var ev = result.reality.intrinsics.exposureCompensation;
                            intensity = Math.pow(2, ev) * 1.0;
                        }
                    }

                    // Send to Unity
                    var csv = intensity.toFixed(3) + ',' +
                              colorR.toFixed(3) + ',' + colorG.toFixed(3) + ',' + colorB.toFixed(3) + ',' +
                              dirX.toFixed(2) + ',' + dirY.toFixed(2) + ',' + dirZ.toFixed(2) + ',' +
                              ambient.toFixed(3);

                    if (typeof SendMessage !== 'undefined') {
                        SendMessage(WGL_LE_State.gameObjectName, 'OnLightEstimation', csv);
                    }
                }
            });
        }
    },

    WebGLStopLightEstimation: function() {
        WGL_LE_State.active = false;
        if (typeof XR8 !== 'undefined' && XR8.removeCameraPipelineModule) {
            XR8.removeCameraPipelineModule('xr8-light-estimation');
        }
    }
};

// Helper: approximate Kelvin to RGB (used internally)
function _kelvinToRGB(kelvin) {
    var temp = kelvin / 100.0;
    var r, g, b;

    if (temp <= 66) {
        r = 1.0;
        g = Math.max(0, Math.min(1, (0.3900815787 * Math.log(temp) - 0.6318414438)));
    } else {
        r = Math.max(0, Math.min(1, (1.2929362 * Math.pow(temp - 60, -0.1332047592))));
        g = Math.max(0, Math.min(1, (1.1298908609 * Math.pow(temp - 60, -0.0755148492))));
    }

    if (temp >= 66) {
        b = 1.0;
    } else if (temp <= 19) {
        b = 0.0;
    } else {
        b = Math.max(0, Math.min(1, (0.5432067891 * Math.log(temp - 10) - 1.1962540891)));
    }

    return [r, g, b];
}

autoAddDeps(XR8LightEstimationLib, '$WGL_LE_State');
mergeInto(LibraryManager.library, XR8LightEstimationLib);
