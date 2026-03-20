mergeInto(LibraryManager.library, {

    WebGLXR8HitTest: function(screenX, screenY) {
        if (!window.xr8World) {
            console.warn('[XR8WorldTrackerLib] World bridge not initialized');
            return;
        }
        window.xr8World.hitTest(screenX, screenY);
    },

    WebGLConfigureWorldTracker: function(objectNamePtr, showMeshes) {
        var objectName = UTF8ToString(objectNamePtr);
        if (window.xr8World) {
            window.xr8World.configure(objectName, showMeshes);
        }
    },
});
