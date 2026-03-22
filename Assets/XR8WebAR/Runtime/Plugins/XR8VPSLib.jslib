mergeInto(LibraryManager.library, {

    // ━━━ VPS — Visual Positioning System (8th Wall VPS for Web) ━━━
    // Bridges 8th Wall's VPS localization into Unity via SendMessage.

    WebGLStartVPS: function(configJsonPtr) {
        var configJson = UTF8ToString(configJsonPtr);
        var config = JSON.parse(configJson);
        console.log('[XR8VPSLib] Starting VPS:', config);

        if (!window.XR8) {
            console.error('[XR8VPSLib] XR8 not found!');
            return;
        }

        window._xr8VPSConfig = config;

        // Register VPS pipeline module
        window._xr8VPSModule = {
            name: 'xr8-vps-unity',

            onStart: function(e) {
                try {
                    window.unityInstance.SendMessage(
                        config.unityObjectName || 'XR8VPSTracker',
                        'OnVPSStarted', '1'
                    );
                } catch(ex) {}
            },

            onProcessCpu: function(e) {
                if (!e || !e.processCpuResult) return;

                var vpsResult = e.processCpuResult.vps || e.processCpuResult.lightship;
                if (!vpsResult) return;

                var objName = config.unityObjectName || 'XR8VPSTracker';

                // Handle localization results
                if (vpsResult.localization) {
                    var loc = vpsResult.localization;

                    if (loc.status === 'localized' && loc.wayspots) {
                        for (var i = 0; i < loc.wayspots.length; i++) {
                            var ws = loc.wayspots[i];
                            var pos = ws.position || { x: 0, y: 0, z: 0 };
                            var rot = ws.rotation || { x: 0, y: 0, z: 0, w: 1 };

                            var csv = ws.id + ',' +
                                      pos.x.toFixed(5) + ',' +
                                      pos.y.toFixed(5) + ',' +
                                      pos.z.toFixed(5) + ',' +
                                      rot.x.toFixed(5) + ',' +
                                      rot.y.toFixed(5) + ',' +
                                      rot.z.toFixed(5) + ',' +
                                      rot.w.toFixed(5);

                            try {
                                window.unityInstance.SendMessage(objName, 'OnWayspotLocalized', csv);
                            } catch(ex) {}
                        }
                    }

                    if (loc.status === 'failed') {
                        try {
                            window.unityInstance.SendMessage(objName, 'OnLocalizationFailedFromJS',
                                loc.reason || 'unknown');
                        } catch(ex) {}
                    }
                }

                // Handle wayspot tracking updates
                if (vpsResult.trackedWayspots) {
                    for (var i = 0; i < vpsResult.trackedWayspots.length; i++) {
                        var ws = vpsResult.trackedWayspots[i];
                        if (!ws.position || !ws.rotation) continue;

                        var csv = ws.id + ',' +
                                  ws.position.x.toFixed(5) + ',' +
                                  ws.position.y.toFixed(5) + ',' +
                                  ws.position.z.toFixed(5) + ',' +
                                  ws.rotation.x.toFixed(5) + ',' +
                                  ws.rotation.y.toFixed(5) + ',' +
                                  ws.rotation.z.toFixed(5) + ',' +
                                  ws.rotation.w.toFixed(5);

                        try {
                            window.unityInstance.SendMessage(objName, 'OnWayspotPoseUpdated', csv);
                        } catch(ex) {}
                    }
                }

                // Handle lost wayspots
                if (vpsResult.lostWayspots) {
                    for (var i = 0; i < vpsResult.lostWayspots.length; i++) {
                        try {
                            window.unityInstance.SendMessage(objName, 'OnWayspotLostFromJS',
                                vpsResult.lostWayspots[i]);
                        } catch(ex) {}
                    }
                }
            }
        };

        // Configure and enable VPS
        try {
            if (XR8.Lightship && XR8.Lightship.VpsCoachingOverlay) {
                // Use 8th Wall's VPS module with wayspot IDs
                XR8.Lightship.VpsCoachingOverlay.configure({
                    wayspotIds: config.wayspotIds || []
                });
            }
            XR8.addCameraPipelineModule(window._xr8VPSModule);
            console.log('[XR8VPSLib] VPS module registered with ' +
                (config.wayspotIds ? config.wayspotIds.length : 0) + ' wayspot(s)');
        } catch(ex) {
            console.error('[XR8VPSLib] Failed to register VPS module:', ex);
        }
    },

    WebGLStopVPS: function() {
        if (window._xr8VPSModule && window.XR8) {
            try {
                XR8.removeCameraPipelineModule(window._xr8VPSModule.name);
            } catch(ex) {}
        }
        window._xr8VPSModule = null;
    },
});
