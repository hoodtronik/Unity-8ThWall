var ConvaiBridgeLib = {
    // Initialize and connect to Convai character
    // configJsonPtr: JSON string with { apiKey, characterId, unityObjectName, enableLipsync, blendshapeFormat }
    Convai_Connect: function(configJsonPtr) {
        var configJson = UTF8ToString(configJsonPtr);
        if (window.convaiBridge) {
            window.convaiBridge.connect(configJson);
        } else {
            console.error('[ConvaiBridgeLib] ConvaiBridge not initialized. Check index.html script loading order.');
        }
    },

    // Disconnect from Convai
    Convai_Disconnect: function() {
        if (window.convaiBridge) {
            window.convaiBridge.disconnect();
        }
    },

    // Send text message to AI character
    Convai_SendText: function(textPtr) {
        var text = UTF8ToString(textPtr);
        if (window.convaiBridge) {
            window.convaiBridge.sendText(text);
        }
    },

    // Toggle microphone (push-to-talk or toggle mode)
    Convai_ToggleMic: function() {
        if (window.convaiBridge) {
            window.convaiBridge.toggleMic();
        }
    },

    // Start microphone
    Convai_StartMic: function() {
        if (window.convaiBridge) {
            window.convaiBridge.startMic();
        }
    },

    // Stop microphone
    Convai_StopMic: function() {
        if (window.convaiBridge) {
            window.convaiBridge.stopMic();
        }
    },

    // Send trigger message for narrative design events
    Convai_SendTrigger: function(triggerNamePtr, payloadPtr) {
        var triggerName = UTF8ToString(triggerNamePtr);
        var payload = UTF8ToString(payloadPtr);
        if (window.convaiBridge) {
            window.convaiBridge.sendTrigger(triggerName, payload);
        }
    },

    // Check if connected
    Convai_IsConnected: function() {
        return (window.convaiBridge && window.convaiBridge.isConnected) ? 1 : 0;
    },

    // Check if character is currently talking
    Convai_IsTalking: function() {
        return (window.convaiBridge && window.convaiBridge.isTalking) ? 1 : 0;
    }
};

mergeInto(LibraryManager.library, ConvaiBridgeLib);
