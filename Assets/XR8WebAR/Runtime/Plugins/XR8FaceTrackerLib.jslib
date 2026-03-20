var XR8FaceTrackerLib = {
    XR8Face_StartTracking: function(configJsonPtr, objNamePtr) {
        var config = UTF8ToString(configJsonPtr);
        var objName = UTF8ToString(objNamePtr);
        if (window.XR8FaceBridge) {
            window.XR8FaceBridge.start(JSON.parse(config), objName);
        } else {
            console.warn('[XR8FaceTrackerLib] XR8FaceBridge not initialized');
        }
    },

    XR8Face_StopTracking: function() {
        if (window.XR8FaceBridge) {
            window.XR8FaceBridge.stop();
        }
    }
};

mergeInto(LibraryManager.library, XR8FaceTrackerLib);
