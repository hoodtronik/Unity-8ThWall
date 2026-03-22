mergeInto(LibraryManager.library, {

    // ━━━ Hand Tracking (8th Wall Hand Tracking Module) ━━━
    // Bridges 8th Wall's hand tracking landmarks into Unity via SendMessage.

    WebGLStartHandTracking: function(configJsonPtr) {
        var configJson = UTF8ToString(configJsonPtr);
        var config = JSON.parse(configJson);
        console.log('[XR8HandLib] Starting hand tracking:', config);

        if (!window.XR8) {
            console.error('[XR8HandLib] XR8 not found!');
            return;
        }

        window._xr8HandConfig = config;

        // Register hand tracking pipeline module
        window._xr8HandModule = {
            name: 'xr8-hand-unity',

            onProcessCpu: function(e) {
                if (!e || !e.processCpuResult || !e.processCpuResult.handTracking) return;

                var handData = e.processCpuResult.handTracking;
                var hands = handData.hands || [];
                var maxHands = config.maxHands || 1;
                var objName = config.unityObjectName || 'XR8HandTracker';

                for (var h = 0; h < Math.min(hands.length, maxHands); h++) {
                    var hand = hands[h];
                    if (!hand || !hand.landmarks) continue;

                    var confidence = hand.score || hand.confidence || 0.8;
                    var csv = h + ',' + confidence.toFixed(4);

                    // Append 21 landmark positions (x,y,z each)
                    var landmarks = hand.landmarks;
                    for (var i = 0; i < 21; i++) {
                        if (i < landmarks.length) {
                            var lm = landmarks[i];
                            csv += ',' + (lm.x || 0).toFixed(5) +
                                   ',' + (lm.y || 0).toFixed(5) +
                                   ',' + (lm.z || 0).toFixed(5);
                        } else {
                            csv += ',0,0,0';
                        }
                    }

                    try {
                        window.unityInstance.SendMessage(objName, 'OnHandData', csv);
                    } catch(ex) {}
                }

                // Send lost events for hands that disappeared
                if (window._xr8LastHandCount !== undefined) {
                    for (var h = hands.length; h < window._xr8LastHandCount; h++) {
                        try {
                            window.unityInstance.SendMessage(objName, 'OnHandLostFromJS', h.toString());
                        } catch(ex) {}
                    }
                }
                window._xr8LastHandCount = hands.length;
            }
        };

        // Enable 8th Wall hand tracking module
        try {
            if (XR8.HandController) {
                XR8.HandController.configure({
                    maxDetections: config.maxHands || 1,
                    coordinates: { axes: 'RIGHT_HANDED' }
                });
            }
            XR8.addCameraPipelineModule(window._xr8HandModule);
            console.log('[XR8HandLib] Hand tracking module registered');
        } catch(ex) {
            console.error('[XR8HandLib] Failed to register hand tracking:', ex);
        }
    },

    WebGLStopHandTracking: function() {
        if (window._xr8HandModule && window.XR8) {
            try {
                XR8.removeCameraPipelineModule(window._xr8HandModule.name);
            } catch(ex) {}
        }
        window._xr8HandModule = null;
    },
});
