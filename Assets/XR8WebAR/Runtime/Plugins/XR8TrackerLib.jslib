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
});
