mergeInto(LibraryManager.library, {

    WebGLSubscribeToGPS: function() {
        if (!navigator.geolocation) {
            console.error('[XR8GPSLib] Geolocation API not available');
            return;
        }

        var unityInstance = window.unityInstance || window.gameInstance;
        if (!unityInstance) {
            console.error('[XR8GPSLib] Unity instance not found');
            return;
        }

        window._gpsWatchId = navigator.geolocation.watchPosition(
            function(position) {
                var data = [
                    position.coords.accuracy || 0,
                    position.coords.altitude || 0,
                    position.coords.altitudeAccuracy || 0,
                    position.coords.heading || 0,
                    position.coords.latitude || 0,
                    position.coords.longitude || 0,
                    position.coords.speed || 0,
                    0 // alpha (device orientation)
                ].join(',');

                unityInstance.SendMessage('XR8GPSTracker', 'OnGPSPosition', data);
            },
            function(error) {
                unityInstance.SendMessage('XR8GPSTracker', 'OnGPSPositionError', error.message);
            },
            {
                enableHighAccuracy: true,
                maximumAge: 5000,
                timeout: 10000
            }
        );

        console.log('[XR8GPSLib] Subscribed to GPS updates');
    },

    WebGLUnsubscribeFromGPS: function() {
        if (window._gpsWatchId !== undefined) {
            navigator.geolocation.clearWatch(window._gpsWatchId);
            window._gpsWatchId = undefined;
            console.log('[XR8GPSLib] Unsubscribed from GPS updates');
        }
    }
});
